using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ProductManagement.Models
{
    public static class DbInitializer
    {
        public static void Seed(AppDbContext context)
        {
            // Ensure database is created/migrated
            context.Database.Migrate();

            // 1. Categories
            var categories = new[]
            {
                new Category { CategoryName = "Informatique" },
                new Category { CategoryName = "Smartphones" },
                new Category { CategoryName = "Audio & Son" },
                new Category { CategoryName = "Accessoires" }
            };

            foreach (var cat in categories)
            {
                if (!context.Categories.Any(c => c.CategoryName == cat.CategoryName))
                {
                    context.Categories.Add(cat);
                }
            }
            context.SaveChanges();

            var catInfo = context.Categories.FirstOrDefault(c => c.CategoryName == "Informatique")?.CategoryId
                          ?? context.Categories.First().CategoryId;
            var catPhone = context.Categories.FirstOrDefault(c => c.CategoryName == "Smartphones")?.CategoryId
                           ?? catInfo;
            var catAudio = context.Categories.FirstOrDefault(c => c.CategoryName == "Audio & Son")?.CategoryId
                           ?? catInfo;
            var catAccess = context.Categories.FirstOrDefault(c => c.CategoryName == "Accessoires")?.CategoryId
                            ?? catInfo;

            // 2. Products with Images
            var sampleProducts = new[]
            {
                new Product
                {
                    Name = "Dell XPS 15 Pro Laptop",
                    Price = 2850.00f,
                    QteStock = 12,
                    CategoryId = catInfo,
                    Image = "laptop_dell.jpg"
                },
                new Product
                {
                    Name = "Smartphone Galaxy Ultra Pro",
                    Price = 2490.00f,
                    QteStock = 8,
                    CategoryId = catPhone,
                    Image = "smartphone_pro.jpg"
                },
                new Product
                {
                    Name = "Casque Sans Fil Noise Canceling",
                    Price = 790.00f,
                    QteStock = 18,
                    CategoryId = catAudio,
                    Image = "casque_audio.jpg"
                },
                new Product
                {
                    Name = "Montre Connectee Titanium Pro",
                    Price = 540.00f,
                    QteStock = 22,
                    CategoryId = catAccess,
                    Image = "smartwatch.jpg"
                },
                new Product
                {
                    Name = "Souris Gaming Sans Fil Ultra",
                    Price = 190.00f,
                    QteStock = 35,
                    CategoryId = catInfo,
                    Image = "souris_gamer.jpg"
                },
                new Product
                {
                    Name = "Clavier Mecanique RGB Pro",
                    Price = 260.00f,
                    QteStock = 15,
                    CategoryId = catInfo,
                    Image = "38b7df20-02b7-4f45-ad31-877eadca8493_clavier.png"
                }
            };

            foreach (var p in sampleProducts)
            {
                var existing = context.Products.FirstOrDefault(prod => prod.Name == p.Name);
                if (existing == null)
                {
                    context.Products.Add(p);
                }
                else
                {
                    // Update image and details if needed
                    existing.Image = p.Image;
                    existing.Price = p.Price;
                    existing.CategoryId = p.CategoryId;
                }
            }
            context.SaveChanges();

            // 3. Sample Orders (To power recommendation collaborative filtering & Admin Dashboard charts)
            if (!context.Orders.Any())
            {
                var order1 = new Order
                {
                    CustomerName = "Karim Ben Ali",
                    Email = "karim.benali@gmail.com",
                    Address = "15 Avenue Habib Bourguiba, Tunis",
                    OrderDate = DateTime.Now.AddDays(-5),
                    TotalAmount = 3040.00f,
                    Items = new List<OrderItem>
                    {
                        new OrderItem { ProductName = "Dell XPS 15 Pro Laptop", Quantity = 1, Price = 2850.00f },
                        new OrderItem { ProductName = "Souris Gaming Sans Fil Ultra", Quantity = 1, Price = 190.00f }
                    }
                };

                var order2 = new Order
                {
                    CustomerName = "Sarra Mansour",
                    Email = "sarra.m@yahoo.fr",
                    Address = "24 Rue de la Liberté, Sousse",
                    OrderDate = DateTime.Now.AddDays(-3),
                    TotalAmount = 3030.00f,
                    Items = new List<OrderItem>
                    {
                        new OrderItem { ProductName = "Smartphone Galaxy Ultra Pro", Quantity = 1, Price = 2490.00f },
                        new OrderItem { ProductName = "Montre Connectee Titanium Pro", Quantity = 1, Price = 540.00f }
                    }
                };

                var order3 = new Order
                {
                    CustomerName = "Mehdi Trabelsi",
                    Email = "mehdi.t@outlook.com",
                    Address = "8 Rue Ibn Khaldoun, Sfax",
                    OrderDate = DateTime.Now.AddDays(-1),
                    TotalAmount = 3830.00f,
                    Items = new List<OrderItem>
                    {
                        new OrderItem { ProductName = "Dell XPS 15 Pro Laptop", Quantity = 1, Price = 2850.00f },
                        new OrderItem { ProductName = "Casque Sans Fil Noise Canceling", Quantity = 1, Price = 790.00f },
                        new OrderItem { ProductName = "Souris Gaming Sans Fil Ultra", Quantity = 1, Price = 190.00f }
                    }
                };

                context.Orders.AddRange(order1, order2, order3);
                context.SaveChanges();
            }
        }
    }
}
