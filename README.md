# 🛍️ NexusStore Pro - ProductManagement TP4 / TP5

Application web e-commerce moderne développée avec **ASP.NET Core MVC 9**, **Entity Framework Core**, **SQL Server** et **ASP.NET Core Identity**.

Ce projet intègre un **moteur de recommandation hybride**, un **chatbot assistant commercial intelligent** (avec recherche visuelle d'images et persistance en base de données), et un **tableau de bord analytique avancé**.

---

## 🚀 Fonctionnalités Clés

### 1. 🤖 Assistant Intelligent & Chatbot Hybride (NexusBot)
* **Recherche Visuelle par Image** : Identification automatique des produits à partir de photos envoyées par l'utilisateur (hachage perceptuel `dHash` 64 bits et histogrammes de couleurs RGB via `SixLabors.ImageSharp`).
* **Interrogation du Stock en Temps Réel** : Réponses instantanées sur les quantités, prix, alertes de rupture et stocks critiques.
* **Persistance en Base de Données** : Chaque conversation est stockée dans la table SQL Server `ChatMessages` avec intention détectée, produit associé et horodatage.
* **Conseiller de Vente Humain** : Gestion de la politesse, FAQ (livraison, paiement, garanties) et réponses contextuelles bienveillantes pour les demandes hors-catalogue.
* **Widget Flottant & Plein Écran** : Glisser-déposer d'images, collage depuis le presse-papier (`Ctrl+V`), et ajout au panier en 1 clic sans rechargement de page.
* **Dashboard Analytics & Historique** : Statistiques d'utilisation du bot, intentions les plus fréquentes et historique complet paginé.

### 2. 🧠 Moteur de Recommandation Hybride
* **Filtrage Basé sur le Contenu (Content-Based)** : Similarité de prix et affinité de catégorie.
* **Filtrage Collaboratif (Co-occurrences)** : Analyse des co-achats dans l'historique des commandes.
* **Recommandations Dynamiques pour le Panier** : Suggestions d'articles complémentaires lors de la commande.

### 3. 📦 Gestion du Catalogue & Panier
* **CRUD Produits & Catégories** avec upload et gestion des images.
* **Panier d'achat en session / singleton** avec calcul automatique des totaux.
* **Recherche textuelle et filtres dynamiques**.

### 4. 🔐 Sécurité & Rôles
* **ASP.NET Core Identity** (Authentification & Autorisation).
* Gestion des rôles (`Admin`, `User`) avec tableau de bord administratif dédié.

---

## 🛠️ Stack Technique

* **Framework** : .NET 9.0 (C# 13) / ASP.NET Core MVC
* **ORM** : Entity Framework Core 9
* **Base de données** : Microsoft SQL Server
* **Traitement d'images** : `SixLabors.ImageSharp`
* **Frontend** : Bootstrap 5, Bootstrap Icons, CSS personnalisé (Glassmorphism & thème moderne), Vanilla JS, jQuery
* **Visualisation** : Chart.js

---

## ⚙️ Installation et Démarrage

### Prérequis
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
* [SQL Server](https://www.microsoft.com/sql-server/) (LocalDB ou Express)

### 1. Cloner le dépôt
```bash
git clone https://github.com/VOTRE_UTILISATEUR/ProductManagement.git
cd ProductManagement
```

### 2. Configurer la chaîne de connexion
Modifier le fichier `appsettings.json` si nécessaire pour pointer vers votre instance SQL Server :
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ProductManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### 3. Appliquer les migrations de base de données
```bash
dotnet ef database update
```

### 4. Lancer l'application
```bash
dotnet run
```
L'application sera accessible sur `https://localhost:5001` ou `http://localhost:5000`.

---

## 📂 Architecture du Projet

```text
├── Controllers/         # Contrôleurs MVC (Product, Category, Chatbot, Admin, Panier...)
├── Models/              # Entités du modèle de données (Product, Category, ChatMessage...)
│   └── Repositories/    # Couche d'accès aux données (IProductRepository, etc.)
├── Services/            # Logique métier (ChatbotService, RecommendationService...)
├── ViewModels/          # Modèles de vue (ChatbotViewModel, Analytics, etc.)
├── Views/               # Vues Razor (Chatbot, Product, Category, Admin...)
├── Migrations/          # Historique des migrations Entity Framework Core
└── wwwroot/             # Fichiers statiques (CSS, JS, images produits)
```

---

## 👨‍💻 Auteur

Projet réalisé dans le cadre du **Master DSIR - 2ème Année** (Module .NET / ASP.NET Core).
