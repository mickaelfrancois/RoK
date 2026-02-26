[🇬🇧 English](README.md) | [🇫🇷 Français](README.fr.md)

![WinUI 3](https://img.shields.io/badge/Made%20with-WinUI%203-0078D4?style=for-the-badge&logo=windows)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)

# 🎵 Rok

**Rok** est un lecteur de musique moderne pour Windows, conçu avec les dernières technologies Microsoft.

## 📖 À propos

Rok est une application de bureau Windows permettant de gérer et d'écouter votre collection musicale locale. Développée avec .NET 10 et WinUI 3, elle offre une interface fluide et moderne qui s'intègre parfaitement à Windows 11.

## ✨ Fonctionnalités

- 🎵 **Lecture audio** - Lecteur complet avec gestion de la file d'attente
- 📚 **Gestion de bibliothèque** - Navigation par albums, artistes, genres et playlists
- 🔍 **Recherche** - Recherche rapide dans toute votre collection
- ✏️ **Édition de métadonnées** - Modification des tags, pochettes et informations des pistes
- 📝 **Playlists** - Création et gestion de playlists personnalisées
- 🎮 **Intégration Discord** - Affichage de votre écoute en cours sur Discord
- 🌓 **Thèmes** - Support des modes clair et sombre
- 🎯 **Mode compact** - Vue minimale du lecteur

## 🛠️ Technologies

### Stack technique

- **.NET 10.0** - Framework moderne et performant
- **C# 13.0** - Dernières fonctionnalités du langage
- **WinUI 3** - Framework d'interface utilisateur natif Windows
- **Windows App SDK 1.8** - APIs de la plateforme Windows
- **SQLite** - Base de données locale
- **Dapper** - Micro-ORM haute performance
- **TagLibSharp** - Lecture/écriture des métadonnées audio
- **Serilog** - Journalisation structurée

### Architecture

Le projet suit les principes de **Clean Architecture** avec une séparation claire des responsabilités :

#### Couches et responsabilités

**🎨 Presentation**
- Interface utilisateur WinUI 3 avec XAML
- ViewModels implémentant le pattern MVVM
- Binding de données bidirectionnel
- Navigation entre les pages
- Gestion des thèmes et styles

**💼 Application (Core/Rok.Application)**
- Use cases métier (Commands et Queries CQRS)
- Orchestration de la logique applicative
- DTOs pour le transfert de données
- Messages pour la communication découplée (Mediator Pattern)
- Interfaces de services

**🏛️ Domain (Core/Rok.Domain)**
- Entités métier (Album, Artist, Track, Playlist, Genre)
- Règles métier et validations
- Interfaces de repositories
- Modèle de domaine indépendant de l'infrastructure

**📦 Infrastructure (Infrastructure/Rok.Infrastructure)**
- Implémentation des repositories avec Dapper
- Accès à la base de données SQLite
- Lecture/écriture de métadonnées avec TagLibSharp
- Services de fichiers et images
- Logging avec Serilog
- Intégration Discord

**Patterns utilisés :**
- **MVVM** (Model-View-ViewModel) - Séparation UI/logique
- **CQRS** (Command Query Responsibility Segregation) - Séparation lecture/écriture
- **Dependency Injection** - Inversion de contrôle avec Microsoft.Extensions.DependencyInjection
- **Mediator Pattern** - Communication découplée entre composants
- **Repository Pattern** - Abstraction de l'accès aux données
- **Unit of Work** - Gestion transactionnelle des opérations

## 📋 Prérequis

- Windows 10 version 1809 (build 17763) ou supérieur
- Windows 11 recommandé pour une expérience optimale

## 📧 Contact

Mickaël François - [@mickaelfrancois](https://github.com/mickaelfrancois)

⭐ Si vous aimez ce projet, n'hésitez pas à lui donner une étoile sur GitHub !