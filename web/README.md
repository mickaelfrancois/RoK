# Rok Companion

Télécommande web pour Rok. L'application tourne dans un navigateur (PC pro, téléphone, tablette) et
**pilote l'instance de Rok qui tourne sur le PC où est la musique**. Aucun flux audio ne transite : le son
sort toujours des enceintes du PC hôte.

Le companion est volontairement séparé du code de Rok :

- il n'est **pas** dans `Rok.slnx` et ne part donc jamais avec le paquet publié sur le Store ;
- il ne référence que `src/Rok.WebApi.Contracts`, un projet de records purs sans dépendance ;
- il consomme l'API HTTP déjà exposée par Rok (`PlayerWebApiService`), enrichie de routes `/api/…`.

## Construire et déployer

```bash
# Publier le companion
dotnet publish web/Rok.Companion -c Release

# Copier le résultat dans le dossier que Rok sert
#   <LocalState> = %LOCALAPPDATA%\Packages\dev-Rokapp.Rokmusicplayer_k3w9s3grwk0dt\LocalState
robocopy web/Rok.Companion/bin/Release/net10.0/publish/wwwroot "<LocalState>\webapp" /MIR
```

Rok sert ensuite le companion à la racine de son port (`http://localhost:5075/` par défaut).
Le dossier est configurable par l'option `WebAppRoot` ; vide, Rok utilise `<LocalState>\webapp`.

## Servir le companion dans Docker

Rok sait déjà servir le companion lui-même ; le conteneur est utile quand tu veux une adresse stable
et allumée en permanence — un NAS, une petite VM — pendant que le PC de musique, lui, va et vient.
Le conteneur ne joue aucun son : il sert la télécommande et relaie ses appels vers Rok.

```bash
cd web
docker compose up -d --build
# puis http://<ip-de-la-machine-docker>:8080
```

`ROK_API` (dans `docker-compose.yml`) dit où joindre Rok **depuis le conteneur** : `http://host.docker.internal:5075`
si Docker tourne sur le PC de musique, `http://192.168.1.50:5075` s'il tourne ailleurs. Pas de barre
finale. La variable est appliquée au démarrage du conteneur, pas au build : changer de PC hôte ne
demande pas de reconstruire l'image.

nginx sert les fichiers publiés **et** relaie `/api/` et `/current/` vers Rok. Le navigateur ne voit
donc qu'une seule origine : ni CORS, ni `ApiBaseAddress` figé dans l'image.

Un seul prérequis côté Rok : `EnableWebApi: true` dans `settings.json`. `WebApiAllowLan` n'est **pas**
nécessaire, et la réservation `netsh` non plus.

C'est le relais qui s'en charge, par une ligne à connaître : `proxy_set_header Host localhost:$proxy_port`.
Sans `WebApiAllowLan`, Rok écoute sur `http://localhost:5075/`, et **http.sys sélectionne le prefix sur
l'en-tête `Host`, pas sur l'adresse de l'appelant** : relayer `Host: 192.168.1.20` vaut un `400 Bad Request`
avant que le code de Rok ne voie la requête, alors que `Host: localhost` entre. Le port de Rok reste donc
lié au loopback et le port du conteneur devient la seule chose que le réseau peut joindre — surface plus
étroite qu'en ouvrant `WebApiAllowLan`, qui exposerait le 5075 lui-même à tout le LAN.

> **Joins le conteneur par son adresse IP, pas par son nom d'hôte.** L'en-tête `Origin`, lui, n'est pas
> réécrit : Rok refuse toute requête modifiante dont l'origine n'est ni loopback ni une IP privée
> (`WebApiOriginGuard`). Sur `http://192.168.1.50:8080` tout fonctionne ; sur `http://mon-nas:8080`, ou
> derrière un reverse proxy qui termine un nom de domaine, l'affichage se remplit normalement mais chaque
> commande répond 403 — la panne est à moitié invisible. Réécrire `Origin` lèverait le blocage et avec lui
> la protection qui empêche n'importe quelle page web de piloter le lecteur : le proxy deviendrait un
> client de confiance pour tout le monde. C'est pourquoi celui-là est transmis tel quel.

Le conteneur ne sert qu'en HTTP et n'ajoute aucune authentification : il reste un service de réseau
local, à ne jamais publier sur Internet.

## Joindre Rok depuis une autre machine

Par défaut l'API écoute uniquement en loopback : **rien n'est exposé au réseau tant que tu ne
l'actives pas**. Dans `<LocalState>\settings.json` :

```json
{
  "WebApiPort": 5075,
  "EnableWebApi": true,
  "WebApiAllowLan": true
}
```

Écouter sur toutes les interfaces demande une réservation d'URL. À passer **une seule fois** dans une
invite de commandes administrateur :

```
netsh http add urlacl url=http://+:5075/ user=%USERDOMAIN%\%USERNAME%
netsh advfirewall firewall add rule name="Rok Companion" dir=in action=allow protocol=TCP localport=5075 profile=private
```

Sans cette réservation, Rok journalise un avertissement contenant la commande exacte et retombe
automatiquement sur le loopback — l'application continue de fonctionner normalement.

Depuis le PC pro : `http://<ip-du-pc-perso>:5075`.

> **Sécurité.** L'API n'a aucune authentification. Une fois `WebApiAllowLan` activé, toute machine du
> réseau local peut piloter la lecture et lister les playlists et la file. La règle de pare-feu
> ci-dessus est limitée au profil réseau *privé* ; n'ouvre jamais ce port sur Internet et ne crée pas
> de redirection de port vers lui sur ta box.
>
> Rok refuse en revanche toute requête modifiante venue d'un autre site : une page web ouverte
> dans ton navigateur ne peut pas piloter le lecteur, même en visant `localhost`. Les appels
> scriptés (curl, PowerShell) passent toujours, eux n'envoient pas d'en-tête `Origin`.

## Développer

Le serveur de développement tourne sur son propre port et parle à Rok via `ApiBaseAddress`, défini
dans `wwwroot/appsettings.Development.json` :

```bash
dotnet watch --project web/Rok.Companion
```

Rok renvoie les en-têtes CORS uniquement aux origines loopback ou en IP privée, ce qui débloque ce
scénario sans exposer l'API aux sites web publics. En production le companion est servi par Rok
lui-même : même origine, aucun CORS en jeu.

## Contrainte à connaître : le trimming

Le companion est publié *trimmé*. La sérialisation JSON par réflexion compile sans broncher puis
échoue à l’exécution : les membres des records sont retirés, la désérialisation lève, et la page
meurt avant son premier rendu — écran figé, aucune playlist, bouton qui ne bascule jamais.

Toute forme traversée par le réseau doit donc être déclarée dans `RokJsonContext`, **listes comprises**,
et les appels doivent passer les métadonnées (`RokJsonContext.Default.<Type>`) plutôt que des
`JsonSerializerOptions`. Blazor masque ces avertissements par défaut ; le projet met
`SuppressTrimAnalysisWarnings=false`, ce qui transforme `IL2026` en erreur de build.

## Routes utilisées

Les routes historiques (`/status`, `/current`, `/queue`, `/play`, `/listen/...`) restent inchangées
pour le companion terminal. Le companion web utilise l'espace `/api/` :

| Méthode | Route | Rôle |
| --- | --- | --- |
| GET | `/api/player/status` | état complet du lecteur et piste courante |
| GET | `/api/player/queue` | file complète, la piste jouée est marquée `isCurrent` |
| POST | `/api/player/play` `pause` `toggle` `next` `previous` `mute` `shuffle` `loop` | commandes |
| POST | `/api/player/volume/{0-100}` | volume |
| POST | `/api/player/seek/{secondes}` | position de lecture |
| POST | `/api/player/queue/{trackId}/play` | reprendre la lecture sur une piste de la file |
| GET | `/api/playlists` | liste des playlists |
| GET | `/api/playlists/{id}/tracks` | pistes d'une playlist |
| POST | `/api/playlists/{id}/play` | charger et lancer une playlist |
| POST | `/api/tracks/{id}/score/{0-5}` | noter une piste (0 efface la note) |
| POST | `/api/surprise/album` | tire un album au hasard et le joue dans l’ordre des pistes |
| POST | `/api/surprise/artist` | tire un artiste au hasard et joue son catalogue mélangé |
| GET | `/current/album-cover` | pochette de la piste courante |

Les deux routes `surprise` répondent avec ce qui a été tiré (`kind`, `name`, `artistName`,
`trackCount`) et reproduisent exactement la commande du bureau : un album garde son ordre de pistes,
un artiste est mélangé. Un tirage tombant sur une entrée sans piste en retente une autre, puis
renvoie 404 si la bibliothèque n’offre rien de jouable.

Les mutations sont en `POST` : un navigateur qui précharge un lien ne doit pas pouvoir passer au
titre suivant. Durées et positions sont exprimées en secondes.