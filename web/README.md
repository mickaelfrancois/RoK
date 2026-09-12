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
| GET | `/current/album-cover` | pochette de la piste courante |

Les mutations sont en `POST` : un navigateur qui précharge un lien ne doit pas pouvoir passer au
titre suivant. Durées et positions sont exprimées en secondes.