# ChangeLog

## [1.11.2] Store – 15 mai 2026

### Modifié

- Passe de revue qualité sur les couches Application, Infrastructure, Présentation et Shared (propagation de CancellationToken, injection de TimeProvider, désabonnements propres).
- Gestion de la fenêtre d'égaliseur extraite dans un service dédié.

### Corrigé

- Synchronisation des paroles : la dernière ligne s'affiche désormais et le retour en arrière fonctionne à nouveau.
- L'ajout d'un artiste à une playlist ajoutait par erreur l'album correspondant.
- Le filtre par date des playlists intelligentes utilise désormais l'heure UTC.
- Stabilité au démarrage et à la fermeture : flush de la télémétrie non bloquant, gestion des exceptions fatales au lancement, attente correcte du seeding initial.
- Annulations correctement filtrées dans les handlers de navigation pour éviter des crashs lors de changements de page rapides.
- Désabonnement systématique des messages au dispose (score de piste, messages transitoires, sessions de lecture).
- Correction de la source MusicBrainzID utilisée pour les artistes lors de l'import.
- Validateur « supérieur à zéro » corrigé.
- Chaînes de secours des menus contextuels converties en anglais.
- Initialisation du type de paroles déplacée hors du getter pour éviter des effets de bord.

--

## [1.11.1] Store – 11 mai 2026

### Ajouté

- Badge de paroles dans l'interface du lecteur.
- Enrichissement automatique des métadonnées depuis l'API lors de l'import.

### Corrigé

- Correction d'un crash lorsqu'une requête album ou artiste ne retourne aucune piste.
- Correction du téléchargement des photos d'artiste quand le dossier de destination n'existe pas.

--

## [1.11.0] Store – 7 mai 2026

### Ajouté

- Raccourcis clavier dans l'application avec une boîte de dialogue récapitulative accessible depuis les options.
- Intégration améliorée avec les contrôles média système Windows (SMTC).
- Paroles synchronisées en couleurs avec refonte de l'affichage en mode plein écran.
- Navigation automatique vers la bibliothèque après 100 albums importés avec notification d'import en arrière-plan.
- Validation du dossier musique avant import avec bandeau d'erreur contextuel à l'onboarding.

### Corrigé

- L'import s'arrête proprement à la fermeture de l'application (plus de crash sur les logs).
- L'état d'erreur à l'onboarding s'affiche correctement quand aucun dossier n'est configuré.
- Correction de la vérification de la bibliothèque musicale au démarrage.

--

## [1.10.3] Store – 29 avril 2026

### Ajouté

- Ajout d'une fonction d'import/export d'une playlist.

### Modifié

- La lecture d'un album conserve l'ordre des pistes de l'album.

### Corrigé

- La lecture des statistiques utilisait la mauvaise connection de base de données.
- Correction d'une erreur pouvant survenir lors l'affichage d'un album/artist/genre.

--

## [1.10.2] Store – 27 avril 2026

### Ajouté

- Extension du suivi de session et de navigation pour la télémétrie (#268).
- Mise en pause et reprise de la lecture lors d'un appel entrant (#267).

### Modifié

### Corrigé

- Mise à jour des données et des photos d'artiste depuis l'API (#270).
- L'import de la bibliothèque bloquait le thread UI (#269).

--

## [1.10.1] Store – 26 avril 2026

### Ajouté

- Amélioration du fondu enchaîné avec NAudio (#261).
- Ajout d'un identifiant de corrélation pour la télémétrie (#265).

### Modifié

- Meilleure gestion des erreurs lors de la lecture des tags audio (#255).
- Modification mineure du fichier de solution (#258).
- Ajout de la documentation d'architecture (#260).

### Corrigé

- L'URL de récupération des paroles était incorrecte (#263).

--

## [1.10.0] Store – 23 avril 2026  

### Ajouté

- Ajout d'un bouton Surprise pour lancer la lecture d'un album aléatoire dans la page des albums, artistes
- Ajout d'un bouton Surprise pour lancer la lecture d'un artiste aléatoire dans la page des artistes
- Gestion des commandes multimédia (Play, Pause, Next, Previous) via le clavier ou les périphériques externes.

### Modifié

- Modification de la structure de la solution

### Corrigé

- Remplacement de l'appel `ContinueWith` en fire-and-forget par une méthode async structurée lors de l'import (#256)
- Meilleure gestion des erreurs lors de la lecture des tags audio (#255)
- Protection contre le cas où `FindIndex` retourne -1 lors du démarrage de la lecture (#254)
- Correction du seuil de limitation : exactement `MaxMessagesBeforeThrottle` messages sont désormais autorisés avant le throttling


--


## [1.9.1] Store – 18 avril 2026  

### Ajouté

- Affichage d'un message lorsqu'il n'y a aucune piste dans une playlist intelligente.

### Corrigé

- Les titres d'une playlist intelligente sont triées par position.


--


## [1.9.0] Store – 16 avril 2026  

### Modifié

- Utilisation de la nouvelle API de télémétrie pour le suivi des événements et des erreurs. 


--


## [1.8.3] Preview – 16 avril 2026  

### Ajouté

- Ajout du minuteur de sommeil pour arrêter la lecture après un certain temps.

### Corrigé

- Correction d'un problème de lecture de certaines pistes avec le moteur NAudio.
- Correction d'un problème lorsque l'utilisateur en mets en pause la lecture pendant longtemps, la lecture ne reprenait pas correctement après la pause.

--


## [1.8.2] Preview – 13 avril 2026  

### Ajouté

- Utilisation du moteur NAudio.
- Ajout d'un equaliseur graphique avec 10 bandes.


--


## [1.8.1] Store – 06 avril 2026  

### Ajouté

- Ajout des traductions en Espagnol.
- Ajout d'un bouton vers le site officiel dans la page des options.
- Ajout d'un mécanisme de feeboack pour les utilisateurs.

### Modifié

- Correction de warnings divers
- Insights : changement du format du total d'écoutes pour les rendre plus lisibles.


--


## [1.8.0] Store – 28 mars 2026  

### Ajouté

- Ajout de la page d'insights avec des statistiques sur les artistes, albums, genres et pays les plus écoutés.

### Modifié  

- Amélioration de la page de bienvenue pour les nouveaux utilisateurs.


--


## [1.7.2] Store – 20 mars 2026  

### Corrigé

- Correction d'un problème de style sur le player en thème sombre.


--


## [1.7.1] Store – 20 mars 2026  

### Modifié

- Mise à jour des styles
- Unification des commandes API et Terminal

- 
--


## [1.7.0] Preview – 16 mars 2026  

### Ajouté

- Ajout d'une API permettant de contrôler la lecture à partir d'autres applications.
- Ajout de la possibilité de contrôler la lecture à partir de la ligne de comande.
- Ajout d'une recherche dans la page des albums, artistes et chansons.

### Modifié  

- Refonte du design system.

### Corrigé


--


## [1.6.0] StoreRelease – 09 mars 2026  

### Ajouté

- Possibilité de mettre en pause automatiquement la lecture en cas de réception d'un appel (Teams, Discord, Slack, Zoom).
- Utilisation de la couleur dominante de la pochette pour le thème d'un album dans la liste des albums, artistes et chansons.
- Import : Détection du type bestof ou live dans le nom de l'album
- Ajout de badge "Nouveau dans la liste des albums, artistes et chansons.

### Modifié  

### Corrigé

- Correction de l'erreur 'Database is locked' lors de la sauvegarde avant migration des données.


--


## [1.5.0] StoreRelease – 28 février 2026

### Ajouté

- Ajout du nombre d'élements (et durée) dans la liste des albums, artistes, et chansons (#183)
- Ajout du nombre d'éléments dans le titre des groupes dans la liste des albums, artistes et chansons (#185)
- Ajout d'un label si les filtres retournent aucune donnée (#184)

### Modifié
- Modification de la licence (#182)

### Corrigé


--


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

