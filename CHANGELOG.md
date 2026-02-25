# ChangeLog

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

## [1.3.0] – 8 février 2026

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

## [1.2.0] – 1 février 2026

### Ajouté
- Refonte du layout des infos piste (#144).
- Améliorations diverses de l’UI.

### Modifié
- Ajustements UI pour la bascule grid/list (#145).