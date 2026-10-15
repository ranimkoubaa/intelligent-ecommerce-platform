using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProductManagement.Models;
using ProductManagement.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ProductManagement.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IRecommendationService _recommendationService;

        public ChatbotService(
            AppDbContext context,
            IWebHostEnvironment env,
            IRecommendationService recommendationService)
        {
            _context = context;
            _env = env;
            _recommendationService = recommendationService;
        }

        public async Task<ChatbotResponseViewModel> ProcessMessageAsync(
            string? message,
            IFormFile? imageFile,
            string? sessionId = null,
            string? userName = null)
        {
            string query = (message ?? "").Trim();
            string lowerQuery = query.ToLowerInvariant();
            string? attachedImageUrl = null;
            ChatbotResponseViewModel response;

            // 1. Image recognition path
            if (imageFile != null && imageFile.Length > 0)
            {
                response = await HandleImageRecognitionAsync(imageFile, query);
                response.DetectedIntent = "image_search";
                attachedImageUrl = response.IdentifiedProduct?.ImageUrl;
                await PersistChatMessageAsync(sessionId, userName,
                    $"[Image] {imageFile.FileName}", response, attachedImageUrl);
                return response;
            }

            // 2. Welcome / empty query — don't persist
            if (string.IsNullOrWhiteSpace(query))
            {
                response = new ChatbotResponseViewModel
                {
                    Reply = "👋 Bonjour ! Je suis **NexusBot**, votre conseiller commercial chez NexusStore. Comment puis-je vous aider aujourd'hui ? 😊\n\nVous pouvez me demander la disponibilité d'un produit, vérifier les prix, ou m'envoyer une photo pour que je retrouve l'article instantanément !",
                    DetectedIntent = "greeting",
                    QuickReplies = new List<string>
                    {
                        "📦 Disponibilité du stock",
                        "💻 Ordinateurs & Laptops",
                        "💰 Produits à moins de 500 DT",
                        "⚠️ Articles en rupture ou stock faible"
                    }
                };
                return response;
            }

            // 3. Conversational FAQs & Politeness
            var conversationalReply = await HandleConversationalQueryAsync(lowerQuery);
            if (conversationalReply != null)
            {
                conversationalReply.DetectedIntent ??= "faq";
                await PersistChatMessageAsync(sessionId, userName, query, conversationalReply, null);
                return conversationalReply;
            }

            // 4. Stock Inquiries
            if (IsStockQuery(lowerQuery))
            {
                response = await HandleStockQueryAsync(lowerQuery, query);
                response.DetectedIntent = "stock_query";
                await PersistChatMessageAsync(sessionId, userName, query, response, null);
                return response;
            }

            // 5. Price & Budget Inquiries
            if (IsPriceQuery(lowerQuery))
            {
                response = await HandlePriceQueryAsync(lowerQuery);
                response.DetectedIntent = "price_query";
                await PersistChatMessageAsync(sessionId, userName, query, response, null);
                return response;
            }

            // 6. Advice & Recommendations
            if (IsAdviceQuery(lowerQuery))
            {
                response = await HandleAdviceQueryAsync();
                response.DetectedIntent = "advice";
                await PersistChatMessageAsync(sessionId, userName, query, response, null);
                return response;
            }

            // 7. Category Inquiries
            if (IsCategoryQuery(lowerQuery, out string matchedCategory))
            {
                response = await HandleCategoryQueryAsync(matchedCategory);
                response.DetectedIntent = $"category:{matchedCategory}";
                await PersistChatMessageAsync(sessionId, userName, query, response, null);
                return response;
            }

            // 8. Direct Product Search by Name/Keywords
            var matchingProducts = await FindProductsByKeywordsAsync(lowerQuery);
            if (matchingProducts.Any())
            {
                response = BuildProductFoundResponse(matchingProducts, query);
                response.DetectedIntent = "product_search";
                response.MatchedProductId = matchingProducts.First().ProductId;
                await PersistChatMessageAsync(sessionId, userName, query, response, null);
                return response;
            }

            // 9. Human-like Fallback for off-catalog items
            response = await HandleHumanAssistantFallbackAsync(query, lowerQuery);
            response.DetectedIntent = "fallback";
            await PersistChatMessageAsync(sessionId, userName, query, response, null);
            return response;
        }

        #region Image Recognition & Visual Search

        private async Task<ChatbotResponseViewModel> HandleImageRecognitionAsync(IFormFile imageFile, string userQuery)
        {
            var response = new ChatbotResponseViewModel
            {
                HasImageMatch = true
            };

            // Retrieve all catalog products with an image
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => !string.IsNullOrEmpty(p.Image))
                .ToListAsync();

            if (!products.Any())
            {
                response.Reply = "Je n'ai pas pu trouver de produits avec photo dans notre base de données pour effectuer la comparaison visuelle.";
                response.QuickReplies = new List<string> { "📦 Voir tous les produits" };
                return response;
            }

            // Extract visual signature of uploaded image
            ulong uploadedHash = 0;
            float[] uploadedHist = new float[16];
            bool hashComputed = false;

            try
            {
                using var stream = imageFile.OpenReadStream();
                using var image = await Image.LoadAsync<Rgba32>(stream);
                uploadedHash = ComputeDHash(image);
                uploadedHist = ComputeColorHistogram(image);
                hashComputed = true;
            }
            catch (Exception)
            {
                hashComputed = false;
            }

            string uploadedFileName = Path.GetFileNameWithoutExtension(imageFile.FileName).ToLowerInvariant();

            // Score each product
            var scoredList = new List<(Product Product, double Score, string Reason)>();

            foreach (var p in products)
            {
                double score = 0.0;
                string reason = "";

                // Visual comparison if image file exists on disk
                if (hashComputed && !string.IsNullOrEmpty(p.Image))
                {
                    string diskPath = Path.Combine(_env.WebRootPath, "images", p.Image);
                    if (File.Exists(diskPath))
                    {
                        try
                        {
                            using var catImg = await Image.LoadAsync<Rgba32>(diskPath);
                            ulong catHash = ComputeDHash(catImg);
                            float[] catHist = ComputeColorHistogram(catImg);

                            int hamming = HammingDistance(uploadedHash, catHash);
                            double visualScore = Math.Max(0.0, 1.0 - (hamming / 32.0)); // 0..1
                            double colorScore = CosineSimilarity(uploadedHist, catHist); // 0..1

                            double combinedVisual = (visualScore * 0.65) + (colorScore * 0.35);
                            score += combinedVisual * 70.0; // Up to 70 pts
                            reason += $"similarité visuelle ({(int)(combinedVisual * 100)}%)";
                        }
                        catch
                        {
                            // Skip visual comparison on image load error
                        }
                    }
                }

                // Filename & keyword hint heuristic (if user uploaded e.g. "laptop.jpg" or "dell.png")
                var prodKeywords = (p.Name + " " + (p.Category?.CategoryName ?? "") + " " + p.Image).ToLowerInvariant();
                var tokens = Regex.Split(uploadedFileName, @"[\s_\-\.\d]+").Where(t => t.Length >= 3);
                int tokenMatches = tokens.Count(t => prodKeywords.Contains(t));

                if (tokenMatches > 0)
                {
                    score += tokenMatches * 20.0;
                    reason += (reason.Length > 0 ? ", " : "") + "mots-clés fichier";
                }

                // If user wrote a query alongside the image (e.g. "is this laptop in stock?")
                if (!string.IsNullOrWhiteSpace(userQuery))
                {
                    var queryTokens = Regex.Split(userQuery.ToLowerInvariant(), @"[\s_\-\.\,]+").Where(t => t.Length >= 3);
                    int qMatches = queryTokens.Count(t => prodKeywords.Contains(t));
                    score += qMatches * 15.0;
                }

                scoredList.Add((p, score, reason));
            }

            var bestMatches = scoredList.OrderByDescending(x => x.Score).ToList();
            var topMatch = bestMatches.FirstOrDefault();

            if (topMatch.Product != null && topMatch.Score >= 25.0)
            {
                var matched = topMatch.Product;
                int confidence = Math.Min(98, Math.Max(60, (int)topMatch.Score));

                var dto = MapToDto(matched, confidence);
                response.IdentifiedProduct = dto;
                response.Products.Add(dto);

                string stockMsg = matched.QteStock switch
                {
                    > 5 => $"🟢 **En stock : {matched.QteStock} unités disponibles** immédiatement !",
                    > 0 => $"🟠 **Stock limité : plus que {matched.QteStock} unités restantes !**",
                    _ => "🔴 **Actuellement en rupture de stock.**"
                };

                response.Reply = $"🎯 **Produit identifié avec succès !** (Indice de confiance : **{confidence}%**)\n\n" +
                                 $"Il s'agit de **{matched.Name}** dans la catégorie *{matched.Category?.CategoryName ?? "Général"}*.\n\n" +
                                 $"💰 **Prix :** {matched.Price:N2} DT\n" +
                                 $"📦 **Disponibilité :** {stockMsg}\n\n" +
                                 $"Souhaitez-vous l'ajouter à votre panier ou voir des accessoires recommandés ?";

                // Add recommendations
                var similar = await _recommendationService.GetSimilarProductsAsync(matched.ProductId, 2);
                foreach (var s in similar)
                {
                    if (s.ProductId != matched.ProductId && !response.Products.Any(p => p.ProductId == s.ProductId))
                    {
                        response.Products.Add(MapToDto(s));
                    }
                }

                response.QuickReplies = new List<string>
                {
                    $"🛒 Ajouter '{matched.Name}' au panier",
                    $"ℹ️ Détails de '{matched.Name}'",
                    "📦 Vérifier un autre produit",
                    "📷 Envoyer une autre photo"
                };
            }
            else
            {
                // Fallback: Show top 3 catalog suggestions
                var suggestions = products.Take(3).ToList();
                response.Products = suggestions.Select(p => MapToDto(p)).ToList();

                response.Reply = "🔎 J'ai analysé l'image transmise, mais je n'ai pas trouvé de correspondance exacte à 100% dans notre stock.\n\n" +
                                 "Voici les articles les plus proches disponibles dans notre catalogue :";

                response.QuickReplies = new List<string>
                {
                    "📦 Voir tout le stock disponible",
                    "🔍 Rechercher par mot-clé",
                    "📷 Réessayer avec une autre photo"
                };
            }

            return response;
        }

        #endregion

        #region Text Handlers (Stock, Price, Category, Keywords)

        private bool IsStockQuery(string q)
        {
            return q.Contains("stock") || q.Contains("disponible") || q.Contains("dispo") ||
                   q.Contains("reste") || q.Contains("quantité") || q.Contains("quantite") ||
                   q.Contains("rupture") || q.Contains("épuisé") || q.Contains("epuise") ||
                   q.Contains("combien");
        }

        private async Task<ChatbotResponseViewModel> HandleStockQueryAsync(string lowerQuery, string rawQuery)
        {
            var response = new ChatbotResponseViewModel();

            // 1. Out of stock query
            if (lowerQuery.Contains("rupture") || lowerQuery.Contains("épuisé") || lowerQuery.Contains("epuise"))
            {
                var outOfStock = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.QteStock <= 0)
                    .ToListAsync();

                if (!outOfStock.Any())
                {
                    response.Reply = "🎉 **Excellente nouvelle !** Aucun produit n'est en rupture de stock en ce moment. Tous nos articles sont actuellement disponibles à la commande.";
                    response.Products = (await _context.Products.Include(p => p.Category).Take(3).ToListAsync())
                        .Select(p => MapToDto(p)).ToList();
                }
                else
                {
                    response.Reply = $"⚠️ Il y a actuellement **{outOfStock.Count} produit(s) en rupture de stock** :";
                    response.Products = outOfStock.Select(p => MapToDto(p)).ToList();
                }

                response.QuickReplies = new List<string> { "📦 Voir les produits en stock", "⚠️ Stock faible (< 5)", "🔝 Meilleures ventes" };
                return response;
            }

            // 2. Low stock alert (< 5)
            if (lowerQuery.Contains("faible") || lowerQuery.Contains("alerte") || lowerQuery.Contains("critique"))
            {
                var lowStock = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.QteStock > 0 && p.QteStock <= 5)
                    .OrderBy(p => p.QteStock)
                    .ToListAsync();

                if (!lowStock.Any())
                {
                    response.Reply = "✅ Aucun article n'est actuellement en situation de stock critique. Tous nos stocks sont au-dessus de 5 unités !";
                }
                else
                {
                    response.Reply = $"⚡ **Alerte stock faible !** Voici les articles dont les quantités restantes sont limitées (dépêchez-vous !) :";
                    response.Products = lowStock.Select(p => MapToDto(p)).ToList();
                }

                response.QuickReplies = new List<string> { "📦 Voir tous les produits", "💰 Produits moins chers" };
                return response;
            }

            // 3. Stock of a specific product mentioned in text
            var allProds = await _context.Products.Include(p => p.Category).ToListAsync();
            var targetProduct = allProds.FirstOrDefault(p =>
                lowerQuery.Contains(p.Name.ToLowerInvariant()) ||
                p.Name.ToLowerInvariant().Split(' ').Any(word => word.Length >= 4 && lowerQuery.Contains(word)));

            if (targetProduct != null)
            {
                var dto = MapToDto(targetProduct);
                response.Products.Add(dto);

                string statusText = targetProduct.QteStock switch
                {
                    > 5 => $"🟢 **En stock !** Il reste actuellement **{targetProduct.QteStock} unités** de *{targetProduct.Name}* au prix de **{targetProduct.Price:N2} DT**.",
                    > 0 => $"🟠 **Stock presque épuisé !** Plus que **{targetProduct.QteStock} unité(s)** disponible(s) pour *{targetProduct.Name}* ({targetProduct.Price:N2} DT).",
                    _ => $"🔴 **Rupture de stock !** *{targetProduct.Name}* n'est plus disponible pour le moment."
                };

                response.Reply = $"📦 **Information Stock Produit :**\n\n{statusText}";
                response.QuickReplies = new List<string>
                {
                    $"🛒 Ajouter au panier",
                    "🔍 Voir d'autres produits",
                    "📦 État global du stock"
                };
                return response;
            }

            // 4. Overall stock summary
            int totalItems = allProds.Sum(p => p.QteStock);
            int inStockCount = allProds.Count(p => p.QteStock > 0);
            double totalValuation = allProds.Sum(p => (double)p.Price * p.QteStock);

            response.Reply = $"📊 **Synthèse globale de l'inventaire NexusStore :**\n\n" +
                             $"- 🏷️ **Nombre de références au catalogue :** {allProds.Count} produits\n" +
                             $"- 📦 **Total d'unités en réserve :** {totalItems} articles\n" +
                             $"- 🟢 **Disponibilité :** {inStockCount}/{allProds.Count} produits en stock (100% disponibles)\n" +
                             $"- 💎 **Valeur totale de l'inventaire :** {totalValuation:N2} DT\n\n" +
                             $"Voici nos articles les plus populaires en stock :";

            response.Products = allProds.OrderByDescending(p => p.QteStock).Take(4).Select(p => MapToDto(p)).ToList();
            response.QuickReplies = new List<string>
            {
                "💻 Ordinateurs & Laptops",
                "📱 Smartphones",
                "🎧 Audio & Son",
                "⚡ Articles en stock faible"
            };
            return response;
        }

        private bool IsPriceQuery(string q)
        {
            return q.Contains("prix") || q.Contains("coute") || q.Contains("coûte") ||
                   q.Contains("cher") || q.Contains("tarif") || q.Contains("budget") ||
                   q.Contains("dinar") || q.Contains("dt");
        }

        private async Task<ChatbotResponseViewModel> HandlePriceQueryAsync(string lowerQuery)
        {
            var response = new ChatbotResponseViewModel();
            var all = await _context.Products.Include(p => p.Category).ToListAsync();

            if (lowerQuery.Contains("moins cher"))
            {
                var cheapest = all.OrderBy(p => p.Price).FirstOrDefault();
                if (cheapest != null)
                {
                    response.Reply = $"🏷️ Le produit le plus abordable de notre boutique est **{cheapest.Name}** à seulement **{cheapest.Price:N2} DT** (Stock : {cheapest.QteStock} unités restantes).";
                    response.Products.Add(MapToDto(cheapest));
                }
            }
            else if (lowerQuery.Contains("plus cher"))
            {
                var mostExpensive = all.OrderByDescending(p => p.Price).FirstOrDefault();
                if (mostExpensive != null)
                {
                    response.Reply = $"💎 Notre produit le plus haut de gamme est **{mostExpensive.Name}** à **{mostExpensive.Price:N2} DT** (Stock : {mostExpensive.QteStock} unités).";
                    response.Products.Add(MapToDto(mostExpensive));
                }
            }
            else
            {
                // Check if user specified a budget number (e.g., "moins de 500", "moins de 1000")
                var match = Regex.Match(lowerQuery, @"\d+");
                if (match.Success && float.TryParse(match.Value, out float budget))
                {
                    var filtered = all.Where(p => p.Price <= budget).OrderBy(p => p.Price).ToList();
                    if (filtered.Any())
                    {
                        response.Reply = $"💰 Voici les produits correspondant à votre budget (≤ **{budget} DT**) :";
                        response.Products = filtered.Select(p => MapToDto(p)).ToList();
                    }
                    else
                    {
                        response.Reply = $"Je n'ai pas trouvé d'articles en dessous de {budget} DT. Le produit le plus accessible commence à {all.Min(p => p.Price):N2} DT.";
                    }
                }
                else
                {
                    response.Reply = "Voici la liste de nos produits avec leurs tarifs et disponibilités :";
                    response.Products = all.Take(4).Select(p => MapToDto(p)).ToList();
                }
            }

            response.QuickReplies = new List<string> { "🏷️ Moins de 300 DT", "🏷️ Moins de 1000 DT", "📦 Vérifier le stock" };
            return response;
        }

        private bool IsCategoryQuery(string q, out string matchedCat)
        {
            matchedCat = "";
            if (q.Contains("informatique") || q.Contains("laptop") || q.Contains("ordinateur") || q.Contains("pc") || q.Contains("dell") || q.Contains("clavier") || q.Contains("souris"))
            {
                matchedCat = "Informatique";
                return true;
            }
            if (q.Contains("smartphone") || q.Contains("telephone") || q.Contains("téléphone") || q.Contains("galaxy") || q.Contains("mobile"))
            {
                matchedCat = "Smartphones";
                return true;
            }
            if (q.Contains("audio") || q.Contains("casque") || q.Contains("son") || q.Contains("ecouteur") || q.Contains("écouteur"))
            {
                matchedCat = "Audio & Son";
                return true;
            }
            if (q.Contains("accessoire") || q.Contains("montre") || q.Contains("smartwatch"))
            {
                matchedCat = "Accessoires";
                return true;
            }
            return false;
        }

        private async Task<ChatbotResponseViewModel> HandleCategoryQueryAsync(string categoryName)
        {
            var response = new ChatbotResponseViewModel();
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Category != null && p.Category.CategoryName == categoryName)
                .ToListAsync();

            if (!products.Any())
            {
                response.Reply = $"Nous n'avons actuellement aucun produit dans le rayon **{categoryName}**.";
            }
            else
            {
                response.Reply = $"📁 Voici nos produits disponibles dans la catégorie **{categoryName}** :";
                response.Products = products.Select(p => MapToDto(p)).ToList();
            }

            response.QuickReplies = new List<string> { "📦 Tout le catalogue", "💰 Moins de 500 DT", "⚠️ Stock faible" };
            return response;
        }

        private async Task<List<Product>> FindProductsByKeywordsAsync(string lowerQuery)
        {
            var words = Regex.Split(lowerQuery, @"[\s_\-\.\,]+").Where(w => w.Length >= 3).ToList();
            if (!words.Any()) return new List<Product>();

            var all = await _context.Products.Include(p => p.Category).ToListAsync();
            return all.Where(p =>
                words.Any(w => p.Name.ToLowerInvariant().Contains(w) ||
                              (p.Category?.CategoryName ?? "").ToLowerInvariant().Contains(w)))
                .ToList();
        }

        private ChatbotResponseViewModel BuildProductFoundResponse(List<Product> products, string rawQuery)
        {
            var response = new ChatbotResponseViewModel();
            response.Reply = $"🔍 J'ai trouvé **{products.Count} produit(s)** correspondant à votre recherche *\"{rawQuery}\"* :";
            response.Products = products.Select(p => MapToDto(p)).ToList();

            var first = products.First();
            response.QuickReplies = new List<string>
            {
                $"🛒 Ajouter '{first.Name}' au panier",
                $"📦 Stock de '{first.Name}'",
                "📷 Identifier par photo",
                "🔝 Voir d'autres produits"
            };
            return response;
        }

        private async Task<ChatbotResponseViewModel?> HandleConversationalQueryAsync(string lowerQuery)
        {
            var response = new ChatbotResponseViewModel();

            // Thanks & Politeness
            if (Regex.IsMatch(lowerQuery, @"\b(merci|merci beaucoup|parfait|super|top|chokran|c'est gentil|cool|magnifique)\b"))
            {
                response.Reply = "Avec grand plaisir ! C'est toujours un plaisir de vous aider. 😊\n\n" +
                                 "Avez-vous besoin d'une autre information ? Je peux vérifier un stock, vous conseiller sur des accessoires ou vous guider pour finaliser votre commande.";
                response.QuickReplies = new List<string> { "📦 Voir les produits en stock", "🛒 Voir mon panier", "🔝 Meilleures ventes" };
                return response;
            }

            // Goodbyes
            if (Regex.IsMatch(lowerQuery, @"\b(au revoir|bye|a bientôt|à bientôt|bonne journée|bonne soiree|bonne soirée|ciao|a plus)\b"))
            {
                response.Reply = "Merci beaucoup pour votre visite chez **NexusStore** ! ✨\n\nPassez une excellente journée et revenez nous voir dès que vous le souhaitez. À très bientôt ! 👋";
                response.QuickReplies = new List<string> { "📦 Voir le catalogue", "🛒 Mon panier" };
                return response;
            }

            // Greetings
            if (Regex.IsMatch(lowerQuery, @"\b(bonjour|salut|coucou|hello|hi|bonsoir|salam)\b"))
            {
                response.Reply = "👋 Bonjour et bienvenue chez **NexusStore** ! Je suis **NexusBot**, votre conseiller de vente virtuel.\n\n" +
                                 "Je suis à votre écoute pour vous renseigner exactement comme en magasin :\n" +
                                 "- 📦 **Disponibilité en temps réel** de nos produits en stock\n" +
                                 "- 💰 **Tarifs, promotions et conseils par budget**\n" +
                                 "- 📷 **Reconnaissance photo** (envoyez-moi une image pour retrouver un produit)\n\n" +
                                 "Que recherchez-vous aujourd'hui ?";
                response.QuickReplies = new List<string>
                {
                    "📦 Quels sont les produits en stock ?",
                    "💻 PC & Laptops",
                    "📱 Smartphones",
                    "🎧 Casques & Audio"
                };
                return response;
            }

            // Identity
            if (lowerQuery.Contains("qui es-tu") || lowerQuery.Contains("tu es qui") || lowerQuery.Contains("t'es qui") || lowerQuery.Contains("comment tu t'appelles") || lowerQuery.Contains("ton nom") || lowerQuery.Contains("c'est quoi ce bot"))
            {
                response.Reply = "🤖 Je suis **NexusBot**, l'assistant intelligent et conseiller commercial de la boutique **NexusStore** !\n\n" +
                                 "Mon rôle est de vous guider, vérifier les quantités en stock en direct dans nos entrepôts, vous orienter vers les meilleurs équipements high-tech et reconnaître vos produits en photo. N'hésitez pas à me poser vos questions !";
                response.QuickReplies = new List<string> { "📦 État des stocks", "💡 Me conseiller un produit", "📷 Scanner une photo" };
                return response;
            }

            // Delivery & Shipping FAQ
            if (lowerQuery.Contains("livraison") || lowerQuery.Contains("livrez") || lowerQuery.Contains("frais") || lowerQuery.Contains("délai") || lowerQuery.Contains("delai") || lowerQuery.Contains("transport"))
            {
                response.Reply = "🚚 **Informations sur la Livraison :**\n\n" +
                                 "- ⏱️ **Délais :** Livraison express sous **24h à 48h ouvrées**.\n" +
                                 "- 📍 **Zones couvertes :** Partout en Tunisie (Grand Tunis, Sousse, Sfax, Nabeul, Bizerte, etc.).\n" +
                                 "- 📦 **Sécurité :** Emballage soigné et vérification du colis à la réception.\n\n" +
                                 "Souhaitez-vous voir nos produits disponibles immédiatement pour une expédition rapide ?";
                response.QuickReplies = new List<string> { "📦 Produits prêts à l'expédition", "💳 Modes de paiement" };
                return response;
            }

            // Payment FAQ
            if (lowerQuery.Contains("paiement") || lowerQuery.Contains("payer") || lowerQuery.Contains("carte") || lowerQuery.Contains("espèces") || lowerQuery.Contains("especes") || lowerQuery.Contains("cash"))
            {
                response.Reply = "💳 **Moyens de Paiement Disponibles :**\n\n" +
                                 "1. 💵 **Paiement à la livraison (Espèces)** : Vous ne réglez qu'à la réception de votre colis auprès du livreur.\n" +
                                 "2. 💳 **Paiement en ligne sécurisé** par carte bancaire lors du checkout.\n\n" +
                                 "Toutes les transactions sont 100% sécurisées !";
                response.QuickReplies = new List<string> { "🛒 Voir mon panier", "📦 Continuer mes achats" };
                return response;
            }

            // Location FAQ
            if (lowerQuery.Contains("adresse") || lowerQuery.Contains("boutique") || lowerQuery.Contains("magasin") || lowerQuery.Contains("où êtes-vous") || lowerQuery.Contains("ou etes vous") || lowerQuery.Contains("localis"))
            {
                response.Reply = "📍 **À propos de NexusStore :**\n\n" +
                                 "Notre hub logistique et showroom sont basés à **Tunis**, et notre boutique en ligne dessert l'ensemble du territoire tunisien avec expédition à domicile.\n\n" +
                                 "Vous pouvez commander 7j/7 et 24h/24 en direct sur notre plateforme !";
                response.QuickReplies = new List<string> { "📦 Voir les produits en stock", "🚚 Infos livraison" };
                return response;
            }

            // Warranty FAQ
            if (lowerQuery.Contains("garantie") || lowerQuery.Contains("retour") || lowerQuery.Contains("échange") || lowerQuery.Contains("authentique"))
            {
                response.Reply = "🛡️ **Garantie & Authenticité Constructeur :**\n\n" +
                                 "Tous nos articles sont neufs, scellés dans leur emballage d'origine, et bénéficient d'une **garantie officielle de 12 à 24 mois**.\n\n" +
                                 "En cas de souci technique, notre SAV prend en charge la réparation ou le remplacement rapide.";
                response.QuickReplies = new List<string> { "📦 Voir les produits garantis", "🔝 Meilleures ventes" };
                return response;
            }

            return null;
        }

        private bool IsAdviceQuery(string q)
        {
            return q.Contains("conseille") || q.Contains("conseil") || q.Contains("recommand") ||
                   q.Contains("meilleur") || q.Contains("populaire") || q.Contains("top vente") ||
                   q.Contains("propose") || q.Contains("que choisir") || q.Contains("aide moi");
        }

        private async Task<ChatbotResponseViewModel> HandleAdviceQueryAsync()
        {
            var response = new ChatbotResponseViewModel();
            var topProducts = await _context.Products.Include(p => p.Category).OrderByDescending(p => p.QteStock).Take(3).ToListAsync();

            response.Reply = "🌟 **Voici mes recommandations coups de cœur du moment !** 😊\n\n" +
                             "Ces équipements sont nos meilleures ventes, actuellement disponibles en stock dans nos rayons :";
            response.Products = topProducts.Select(p => MapToDto(p)).ToList();
            response.QuickReplies = new List<string>
            {
                "💻 PC Portables",
                "🎧 Casques & Audio",
                "📱 Smartphones",
                "💰 Moins de 500 DT"
            };
            return response;
        }

        private async Task<ChatbotResponseViewModel> HandleHumanAssistantFallbackAsync(string rawQuery, string lowerQuery)
        {
            var response = new ChatbotResponseViewModel();
            var popular = await _context.Products.Include(p => p.Category).Take(3).ToListAsync();

            // Try to extract queried off-catalog item (e.g., "et lunettes ?", "vous avez des lunettes", "et les vêtements ?", "des chaussures ?")
            string? requestedItem = ExtractOffCatalogItem(lowerQuery);

            if (!string.IsNullOrWhiteSpace(requestedItem))
            {
                response.Reply = $"Ah, malheureusement nous ne proposons pas de **{requestedItem}** pour le moment ! 😊\n\n" +
                                 $"Notre boutique **NexusStore** est spécialisée dans les équipements **high-tech et informatiques** :\n" +
                                 $"- 💻 **PC portables & Laptops** (Dell XPS 15 Pro, pour le travail, les études et la création)\n" +
                                 $"- 📱 **Smartphones nouvelle génération** (Galaxy Ultra Pro...)\n" +
                                 $"- 🎧 **Audio & Casques sans fil** (réduction de bruit active haute fidélité)\n" +
                                 $"- ⌚ **Accessoires connectés & Gaming** (montres connectées sport & pro, souris gaming, claviers mécaniques)\n\n" +
                                 $"En revanche, si vous cherchez un accessoire connecté ou un équipement technologique, je serais ravi de vous orienter ! Souhaitez-vous découvrir nos articles les plus demandés en stock ?";

                response.Products = popular.Select(p => MapToDto(p)).ToList();
                response.QuickReplies = new List<string>
                {
                    "⌚ Montres connectées",
                    "🎧 Casques Audio",
                    "💻 PC Portables",
                    "📦 Tout le stock disponible"
                };
                return response;
            }

            // General natural language conversational response
            response.Reply = $"Je n'ai pas d'article correspondant exactement à *\"{rawQuery}\"* dans notre inventaire actuel, mais je suis là pour vous conseiller comme en magasin ! 😊\n\n" +
                             $"💡 Dites-moi ce que vous aimeriez trouver :\n" +
                             $"- Un **ordinateur portable** performant pour travailler ou coder\n" +
                             $"- Un **smartphone** haute définition\n" +
                             $"- Un **casque audio** immersif ou une **montre connectée**\n" +
                             $"- Ou vous pouvez m'envoyer une **photo d'un produit** 📷 via l'icône appareil photo pour que je l'identifie !";

            response.Products = popular.Select(p => MapToDto(p)).ToList();
            response.QuickReplies = new List<string>
            {
                "📦 Produits en stock",
                "💻 Ordinateurs",
                "📱 Smartphones",
                "🎧 Audio & Son"
            };
            return response;
        }

        private static string? ExtractOffCatalogItem(string q)
        {
            // Pattern 1: "et lunettes ?", "et des lunettes", "et pour les lunettes", "avez-vous des lunettes", "vous vendez des lunettes", etc.
            var m1 = Regex.Match(q, @"(?:et|et pour|qu'en est-il de|avez[- ]vous|vous avez|vous vendez|vous proposez|y a[- ]t[- ]il|cherche)\s+(?:des\s+|du\s+|de la\s+|les\s+|un\s+|une\s+|d')?([a-zA-ZÀ-ÿ\s]{3,30})\??", RegexOptions.IgnoreCase);
            if (m1.Success)
            {
                string item = m1.Groups[1].Value.Trim().TrimEnd('?').Trim();
                if (item.Length >= 3 && !item.Contains("stock") && !item.Contains("prix"))
                {
                    return item;
                }
            }

            // Direct check for common off-catalog product words
            var commonItems = new[]
            {
                "lunette", "lunettes", "lunette de soleil", "lunettes de soleil", "lunettes vr",
                "vêtement", "vêtements", "vetement", "vetements", "habit", "habits", "robe", "pantalon", "chemise", "t-shirt",
                "chaussure", "chaussures", "basket", "baskets", "sneakers",
                "parfum", "parfums", "maquillage", "cosmétique",
                "sac", "sacs", "valise", "sac à dos",
                "télé", "television", "télévision", "tv", "écran tv",
                "meuble", "meubles", "chaise", "bureau", "table",
                "voiture", "moto", "vélo",
                "livre", "livres", "roman", "manga",
                "café", "nourriture", "pizza", "chocolat"
            };

            foreach (var item in commonItems)
            {
                if (q.Contains(item))
                {
                    return item;
                }
            }

            // Pattern 2: Short query like "lunettes ?", "veste ?"
            var m2 = Regex.Match(q.Trim(), @"^([a-zA-ZÀ-ÿ]{3,20})\s*\??$");
            if (m2.Success)
            {
                string word = m2.Groups[1].Value;
                if (!word.Contains("stock") && !word.Contains("prix") && !word.Contains("aide") && !word.Contains("bonjour"))
                {
                    return word;
                }
            }

            return null;
        }

        #endregion

        #region Helper Methods (Image dHash, Color Histogram, DTO Mapping)

        private static ChatbotProductDto MapToDto(Product p, int? confidence = null)
        {
            string status;
            string badgeClass;

            if (p.QteStock <= 0)
            {
                status = "Rupture de stock";
                badgeClass = "danger";
            }
            else if (p.QteStock <= 5)
            {
                status = $"Stock faible ({p.QteStock} restants)";
                badgeClass = "warning";
            }
            else
            {
                status = $"En stock ({p.QteStock} unités)";
                badgeClass = "success";
            }

            return new ChatbotProductDto
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Price = p.Price,
                QteStock = p.QteStock,
                StockStatus = status,
                StockBadgeClass = badgeClass,
                ImageUrl = string.IsNullOrEmpty(p.Image) ? "/images/laptop_dell.jpg" : $"/images/{p.Image}",
                CategoryName = p.Category?.CategoryName ?? "Général",
                ConfidencePercent = confidence
            };
        }

        /// <summary>
        /// Computes 64-bit difference hash (dHash) for visual image comparison.
        /// Resizes image to 9x8, compares horizontal adjacent pixels luminosity.
        /// </summary>
        private static ulong ComputeDHash(Image<Rgba32> original)
        {
            using var clone = original.Clone(ctx => ctx.Resize(9, 8).Grayscale());
            ulong hash = 0;
            int bitIndex = 0;

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    byte left = clone[x, y].R;
                    byte right = clone[x + 1, y].R;

                    if (left > right)
                    {
                        hash |= (1UL << bitIndex);
                    }
                    bitIndex++;
                }
            }

            return hash;
        }

        /// <summary>
        /// Computes normalized 16-bin color histogram (RGB) for color tone matching.
        /// </summary>
        private static float[] ComputeColorHistogram(Image<Rgba32> original)
        {
            using var small = original.Clone(ctx => ctx.Resize(32, 32));
            float[] hist = new float[16]; // 5 R, 5 G, 6 B
            int totalPixels = small.Width * small.Height;

            for (int y = 0; y < small.Height; y++)
            {
                for (int x = 0; x < small.Width; x++)
                {
                    var p = small[x, y];
                    int rBin = Math.Min(4, p.R / 52);
                    int gBin = Math.Min(4, p.G / 52) + 5;
                    int bBin = Math.Min(5, p.B / 43) + 10;

                    hist[rBin]++;
                    hist[gBin]++;
                    hist[bBin]++;
                }
            }

            // Normalize
            if (totalPixels > 0)
            {
                for (int i = 0; i < hist.Length; i++)
                {
                    hist[i] /= (totalPixels * 3.0f);
                }
            }

            return hist;
        }

        private static int HammingDistance(ulong hash1, ulong hash2)
        {
            ulong x = hash1 ^ hash2;
            int dist = 0;
            while (x > 0)
            {
                dist += (int)(x & 1);
                x >>= 1;
            }
            return dist;
        }

        private static double CosineSimilarity(float[] v1, float[] v2)
        {
            if (v1 == null || v2 == null || v1.Length != v2.Length) return 0.0;
            double dot = 0.0;
            double mag1 = 0.0;
            double mag2 = 0.0;

            for (int i = 0; i < v1.Length; i++)
            {
                dot += v1[i] * v2[i];
                mag1 += v1[i] * v1[i];
                mag2 += v2[i] * v2[i];
            }

            if (mag1 <= 0 || mag2 <= 0) return 0.0;
            return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
        }

        #endregion

        #region DB Persistence & Analytics

        private async Task PersistChatMessageAsync(
            string? sessionId,
            string? userName,
            string userMessage,
            ChatbotResponseViewModel response,
            string? attachedImageUrl)
        {
            try
            {
                var record = new ChatMessage
                {
                    SessionId = sessionId,
                    UserName = userName,
                    UserMessage = userMessage,
                    BotResponse = response.Reply,
                    AttachedImageUrl = attachedImageUrl,
                    DetectedIntent = response.DetectedIntent,
                    MatchedProductId = response.MatchedProductId,
                    CreatedAt = DateTime.Now
                };
                _context.ChatMessages.Add(record);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Non-blocking: DB write failure should never crash the chatbot
            }
        }

        public async Task<ChatbotHistoryViewModel> GetChatHistoryAsync(int page = 1, int pageSize = 20)
        {
            int total = await _context.ChatMessages.CountAsync();

            var messages = await _context.ChatMessages
                .Include(m => m.MatchedProduct)
                    .ThenInclude(p => p == null ? null : p.Category)
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new ChatMessageViewModel
                {
                    Id = m.Id,
                    SessionId = m.SessionId,
                    UserName = m.UserName,
                    UserMessage = m.UserMessage,
                    BotResponse = m.BotResponse,
                    AttachedImageUrl = m.AttachedImageUrl,
                    DetectedIntent = m.DetectedIntent,
                    MatchedProductName = m.MatchedProduct != null ? m.MatchedProduct.Name : null,
                    MatchedProductImage = m.MatchedProduct != null ? m.MatchedProduct.Image : null,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return new ChatbotHistoryViewModel
            {
                Messages = messages,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<ChatbotAnalyticsViewModel> GetChatAnalyticsAsync()
        {
            var today = DateTime.Today;

            int total = await _context.ChatMessages.CountAsync();
            int todayCount = await _context.ChatMessages
                .CountAsync(m => m.CreatedAt >= today);
            int imageSearches = await _context.ChatMessages
                .CountAsync(m => m.DetectedIntent == "image_search");
            int sessions = await _context.ChatMessages
                .Where(m => m.SessionId != null)
                .Select(m => m.SessionId)
                .Distinct()
                .CountAsync();

            // Top intents
            var intentIconMap = new Dictionary<string, string>
            {
                { "stock_query",   "bi-box-seam" },
                { "price_query",   "bi-currency-dollar" },
                { "product_search","bi-search" },
                { "image_search",  "bi-image" },
                { "faq",           "bi-question-circle" },
                { "advice",        "bi-stars" },
                { "fallback",      "bi-chat-dots" },
                { "greeting",      "bi-emoji-smile" }
            };

            var topIntents = await _context.ChatMessages
                .Where(m => m.DetectedIntent != null)
                .GroupBy(m => m.DetectedIntent!)
                .Select(g => new { Intent = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToListAsync();

            var intentStats = topIntents.Select(x => new IntentStatDto
            {
                Intent = x.Intent,
                Count = x.Count,
                Icon = intentIconMap.TryGetValue(x.Intent, out var icon) ? icon : "bi-chat"
            }).ToList();

            // Top mentioned products
            var topProducts = await _context.ChatMessages
                .Where(m => m.MatchedProductId != null)
                .GroupBy(m => m.MatchedProductId!.Value)
                .Select(g => new { ProductId = g.Key, Mentions = g.Count() })
                .OrderByDescending(x => x.Mentions)
                .Take(5)
                .ToListAsync();

            var topProductStats = new List<TopProductStatDto>();
            foreach (var tp in topProducts)
            {
                var prod = await _context.Products.FindAsync(tp.ProductId);
                if (prod != null)
                {
                    topProductStats.Add(new TopProductStatDto
                    {
                        ProductName = prod.Name,
                        ProductImage = prod.Image,
                        Mentions = tp.Mentions
                    });
                }
            }

            // Recent messages
            var recent = await _context.ChatMessages
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .Select(m => new ChatMessageViewModel
                {
                    Id = m.Id,
                    SessionId = m.SessionId,
                    UserName = m.UserName,
                    UserMessage = m.UserMessage,
                    BotResponse = m.BotResponse,
                    DetectedIntent = m.DetectedIntent,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return new ChatbotAnalyticsViewModel
            {
                TotalConversations = total,
                TotalSessions = sessions,
                ImageSearches = imageSearches,
                TodayConversations = todayCount,
                TopIntents = intentStats,
                TopProducts = topProductStats,
                RecentMessages = recent
            };
        }

        #endregion
    }
}
