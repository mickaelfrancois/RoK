# ChangeLog

## [1.5.0] StoreRelease – 28 février 2026

### Ajouté

- Ajout du nombre d'élements (et durée) dans la liste des albums, artistes, et chansons (#183)
- Ajout du nombre d'éléments dans le titre des groupes dans la liste des albums, artistes et chansons (#185)
- Ajout d'un label si les filtres retournent aucune donnée (#184)

### Modifié
- Modification de la licence (#182)

### Corrigé


## [1.4.0] StoreRelease – 25 février 2026

### Ajouté

- Mémorisation de la position de la fenêtre et du mode compact (#172).
- Le clic sur un titre d'un album démarre la lecture de cet album à partir de la piste cliquée (#171).
- Le clic sur un titre d'une playlist démarre la lecture de cette playlist à partir de la piste cliquée (#171).

### Modifié

- Le clic sur un titre de la liste d'écoute ouvre désormais les infos de la piste au lieu de démarrer la lecture (#169).

### Corrigé

- Le choix 'Tous' dans les filtres des genres réinitialisait tous les filtres au lieu de réinitialiser uniquement le filtre sur les genres (#174).
- L'affichage d'une grille sans groupement ne fonctionnait pas correctement (#173).

 
--


## [1.3.3] – 25 février 2026

### Ajouté

- Limitation du chargement des playlists à 100 pistes mélangées (#166).
- Ajout de tests unitaires (#164).
- Ajout du template UI pour les listes (#165).
- Ajout de l’auto‑scroll vers la piste en cours lors de l’ouverture de la page (#162).

### Modifié

- Mise à jour du menu de filtre de genre pour utiliser FilterByGenreCommand (#174).
- Refactor des mises à jour d’ItemsSource dans les pages Albums/Artistes (#173).
- Restauration de la position/taille de la fenêtre lors du passage en mode compact (#172).
- Amélioration de la lecture spécifique à une piste dans les playlists (#171).
- Mise à jour du binding de ListenCommand (#170).
- Clic sur le titre dans la vue d’écoute ouvre désormais les infos de la piste (#169).
- Déplacement des filtres vers la couche application (#168).
- Déplacement de PlayerService vers la couche application (#163).

### Corrigé

- Divers correctifs liés au binding et au comportement des playlists (#171).
- Ajustements mineurs dans la gestion des filtres (#168).

 
--


## [1.3.2] – 14 février 2026

### Ajouté

- Ajout d’interfaces pour les services d’images albums/artistes (#159).
- Mise à jour de la logique GetLyricsLastAttempt avant l’appel API (#158).
- Ajout des statistiques du projet et d’un bouton de support dans la page Options (#156).

### Modifié

- Nettoyage du code et réorganisation des namespaces (#155, #160).
- Mise à jour de tous les packages (#157).
- Amélioration du shuffle pour alterner les artistes (#154).
- Mise à jour du README (#153).

### Corrigé

- Ajustements mineurs dans la logique API et les bindings (#158, #159).

 
--


## [1.3.0] StoreRelease – 8 février 2026

### Ajouté

- Ajout du throttling et de la progression UI pour l’import (#150).
- Ajout de l’option “Current Listening” dans le menu contextuel (#148).
- Ajout des suggestions de recherche avec debounce (#147).

### Modifié

- Refactor de la gestion des albums de compilation + mise à jour du layout (#152).
- Remplacement du toggle grid/list par un bouton stylé (#145).
- Amélioration de la mise en page des infos piste (rating aligné au titre) (#144).

### Corrigé

- Suppression d’un fichier inutile (#149).

 
--


## [1.2.0] – 1 février 2026

### Ajouté

- Refonte du layout des infos piste (#144).
- Améliorations diverses de l’UI.

### Modifié

- Ajustements UI pour la bascule grid/list (#145).


--  


## [1.1.22] – 24 janvier 2026  

### Ajouté  

- Aucun ajout spécifique.

### Modifié  

- Refactor du binding `IsFavorite` et de l’injection de dépendances pour les handlers album/artiste/genre (#139).

### Corrigé  

- Aucun correctif spécifique.

 
--  


## [1.1.21] – 21 janvier 2026  

### Ajouté  

- Aucun ajout spécifique.

### Modifié  

- Amélioration de la suppression de pistes : transaction plus robuste et meilleure gestion de l’annulation (#137).

### Corrigé  

- Correction de dépendances manquantes ou incorrectes (#136).


--  


## [1.1.20] – 20 janvier 2026  

### Ajouté  

- Affichage du sous‑titre dans la vue Playlist (#135).  
- Ajout du double‑tap pour basculer en mode compact dans le player control (#135).

### Modifié  

- Migration complète vers MVVM.Toolkit dans tous les ViewModels (#134).  

### Corrigé  

- Aucun correctif spécifique.

 
--  


## [1.1.18] – 18 janvier 2026  

### Ajouté  

- Persistance de la préférence `IsGridView` pour Albums, Artistes et Playlists (#130).

### Modifié  

- Refonte du chargement des playlists pour utiliser uniquement des objets `Track` (#131).  

### Corrigé  

- Aucun correctif spécifique.

 
--  


## [1.1.17] – 18 janvier 2026

### Ajouté

- Ajout du toggle grid/list pour la vue Artistes (#128).
- Ajout du toggle grid/list pour la vue Playlists (#126).

### Modifié

- Mise à jour de l’affichage des albums (PR #127).

### Corrigé

- Aucun correctif spécifique identifié.


--


## [1.1.16] – 17 janvier 2026

### Ajouté

- Ajout du mode WAL, du backup et des étapes de maintenance pour la base SQLite (#125).

### Modifié

- Aucun changement majeur.

### Corrigé

- - Aucun correctif spécifique.


--


## [1.1.15] – 16 janvier 2026

### Ajouté

- Ajout du bouton Shuffle et de la commande associée dans la page Playlist (#124).
- Ajout du tri dynamique des colonnes de pistes (#123).
- Ajout de la migration 5 pour mettre à jour `trackCount` (#122).

### Modifié

- Mise à jour du menu dynamique des playlists et du nom du package dev (#120).

### Corrigé

- Aucun correctif spécifique.


--


## [1.1.14] – 12 janvier 2026

### Ajouté

- Ajout des statistiques de paroles dans la vue Statistiques (#115).

### Modifié

- Mise à jour de l’icône du bouton pour les états Checked (#116).
- Suppression de l’ancien affichage des paroles et mise à jour du manifeste (#114).

### Corrigé

- Ajout de gestion d’exception lors de la récupération et sauvegarde des paroles (#119).
- Ajout de `IF NOT EXISTS` dans la création des tables et index SQL (#118).


--


## [1.1.13] – 9 janvier 2026

### Ajouté

- Ajout de la page Genre, navigation et support des favoris (#112).

### Modifié

- Aucun changement majeur.

### Corrigé

- Aucun correctif spécifique.


--


## [1.1.12] – 7 janvier 2026

### Ajouté

- Ajout du label d’unité pour les champs “jours” dans PlaylistGroupFilter (#111).

### Modifié

- Aucun changement majeur.

### Corrigé

- Aucun correctif spécifique.


--


## [1.1.11] – 5 janvier 2026
### Ajouté

- Ajout du support du drag & drop pour réordonner les pistes dans les playlists (#103).
- Ajout de l’ajout d’album/artiste directement dans une playlist (#102).

### Modifié

- Aucun changement majeur.

### Corrigé

- Aucun correctif spécifique.


--


## [1.1.10] – 30 décembre 2025
### Ajouté

- Ajout de champs modifiables et métadonnées pour les albums, amélioration de la boîte de dialogue d’édition (#100).

### Modifié

- Aucun changement majeur.

### Corrigé

- Aucun correctif spécifique.


--


## [1.1.9] – 28 décembre 2025

### Ajouté

- - Ajout des liens vers les réseaux sociaux (#98).

### Modifié

- Mise à jour du manifeste : bump version 1.1.9.0 (#99).

### Corrigé

- Correction de la clé API MusicData (PR #97).


--


## [1.1.8] – 27 décembre 2025

### Ajouté

- Ajout de la configuration de la clé API dans `MusicDataApiOptions` (#96).

### Modifié

- Aucun changement majeur.

### Corrigé

- Aucun correctif spécifique.


--


## [1.1.2] – 17 décembre 2025

### Ajouté

- Ajout de la colonne d’index de piste et amélioration du style de la liste des playlists (#109).
- Ajout de la colonne “country name” et amélioration des filtres pays (#108).
- Amélioration de l’ordre des pistes et de l’édition du nombre max dans les smart playlists (#107).

### Modifié

- Mise à jour des labels des champs dans les filtres de playlist (#106).

### Corrigé

- Correction du `DisplayMemberPath` du ComboBox pour utiliser la propriété `Value` (#105).

