# Store listing & acquisition kit

Source of truth for the Microsoft Store listing (Partner Center → Store listings) and for
third-party directory submissions. The Store fields are applied through Partner Center's
listing CSV export/import (en-us, fr-fr, es-es, uk-ua columns); limits are the Partner Center ones.

- `store-listing.csv` — ready-to-import listing (Partner Center → Store listings → Import).
- `generate_store_listing.py` — rebuilds it from a fresh export:
  `python docs/marketing/generate_store_listing.py <export.csv>`. Edit the texts in the
  script, not in the CSV; it aborts if it cannot round-trip the export byte for byte.

Baseline (2026-09-28, telemetry + Store API):
- ~25 installs / week, 11.5 % come back on day 2, 44 % end with an empty library.
- 0 ratings on the Store.
- Current listing never says "local files", "MP3" or "FLAC": it attracts streaming users
  who have no local collection, then leave.

Positioning: **a player for people who own their music** — local MP3/FLAC/ALAC collections,
from a few hundred to 100k+ tracks. Say it in the first sentence of every field.

---

## 1. Store listing — English (en-US, default)

### Product name

Keep `Rok musicplayer` (reserved name). Optional: reserve and switch to
`Rok – Local Music Player (FLAC, ALAC, MP3)` — descriptive names are accepted, keyword lists are not.

### Short description (≤ 1000 chars)

A fast, modern player for your local music collection on Windows 11. Plays your MP3, FLAC, ALAC/M4A, WAV and WMA
files, organises big libraries automatically, and adds smart playlists, lyrics, internet radio
and listening stats. Free, no account, no ads.

### Description (≤ 10000 chars)

Rok is a music player for people who own their music.

Point Rok at your Music folder and it builds a beautiful library from your MP3, FLAC, ALAC/M4A, WAV and WMA files:
albums, artists, genres and covers, enriched automatically. It stays fast with collections of
100,000 tracks and more.

WHY ROK
• Made for local files — your collection, on your disk, no streaming account needed.
• Native Windows 11 app — Fluent design, light/dark theme, media keys and system media controls.
• Free, no ads, no account.

LISTEN
• Smooth crossfade, sleep timer, queue editing by song, album, artist or genre.
• Synced lyrics, including lyrics embedded in your files.
• Internet radio: search thousands of stations or play any stream URL.
• Automatically pauses during Teams and other calls.

ORGANISE
• Smart playlists that update themselves (genre, year, play count, rating…).
• Import and export playlists.
• Edit album and artist details in Rok without ever touching your files; missing covers and metadata retrieved automatically.

REDISCOVER
• Listening stats per album and artist, album anniversaries, "Surprise me" picks.
• Local API: control playback from your own scripts and automation tools.
• Discord Rich Presence: show what you are listening to.

Supported formats: MP3, FLAC, M4A (AAC and ALAC), WAV, WMA and AIFF.

### Features (≤ 20 items, ≤ 200 chars each)

1. Plays your local MP3, FLAC and ALAC collection — no streaming account needed
2. Fast with huge libraries (100,000+ tracks)
3. Automatic album covers and metadata
4. Smart playlists that update themselves
5. Synced and embedded lyrics
6. Internet radio with thousands of stations
7. Crossfade and sleep timer
8. Listening stats and album anniversaries
9. Local API: control playback from scripts and automation tools
10. Auto-pause during Teams calls
11. Discord Rich Presence
12. Media keys and Windows media controls
13. Light and dark themes, native Windows 11 design
14. Free, no ads, no account

### Search terms (7 max, ≤ 30 chars each, 21 words total)

`local music player` · `FLAC player` · `MP3 player` · `music library` ·
`ALAC player` · `smart playlists` · `internet radio`

No competitor names in search terms (Store policy risk); keep them for AlternativeTo.

### Screenshots

Order matters (first 3 show in search results):
1. Albums grid with covers (the "wow").
2. Now playing with lyrics.
3. Album page with stats panel.
4. Smart playlist editor.
5. Radios.
6. Options page with the local API settings.

Add a one-line caption on each: "Your FLAC, ALAC & MP3 library, beautifully organised", etc.

---

## 2. Store listing — Français (fr-FR)

### Description courte

Un lecteur rapide et moderne pour votre collection de musique locale sous Windows 11. Lit vos
fichiers MP3, FLAC, ALAC/M4A, WAV et WMA, organise automatiquement les grandes bibliothèques, et ajoute playlists
intelligentes, paroles, radios internet et statistiques d'écoute. Gratuit, sans compte, sans pub.

### Description

Rok est un lecteur pour ceux qui possèdent leur musique.

Indiquez votre dossier Musique : Rok construit une belle bibliothèque à partir de vos fichiers
MP3, FLAC, ALAC/M4A, WAV et WMA — albums, artistes, genres et pochettes, enrichis automatiquement. Il reste rapide
avec 100 000 titres et plus.

POURQUOI ROK
• Pensé pour les fichiers locaux — votre collection, sur votre disque, sans abonnement.
• Application Windows 11 native — design Fluent, thème clair/sombre, touches multimédia.
• Gratuit, sans pub, sans compte.

ÉCOUTER
• Fondu enchaîné, minuterie de sommeil, file d'attente modifiable par titre, album, artiste ou genre.
• Paroles synchronisées, y compris celles intégrées aux fichiers.
• Radios internet : des milliers de stations ou n'importe quelle URL de flux.
• Pause automatique pendant les appels Teams.

ORGANISER
• Playlists intelligentes qui se mettent à jour seules (genre, année, écoutes, note…).
• Import et export de playlists.
• Modifiez les fiches albums et artistes dans Rok, sans jamais toucher à vos fichiers ; pochettes et métadonnées manquantes récupérées automatiquement.

REDÉCOUVRIR
• Statistiques d'écoute par album et artiste, anniversaires d'albums, « Surprends-moi ».
• API locale : pilotez la lecture depuis vos scripts et outils d’automatisation.
• Discord Rich Presence.

Formats pris en charge : MP3, FLAC, M4A (AAC et ALAC), WAV, WMA et AIFF.

### Termes de recherche

`lecteur de musique` · `lecteur FLAC` · `lecteur MP3` · `bibliothèque musicale` ·
`lecteur ALAC` · `playlists intelligentes` · `radio internet`

---

## 3. Store listing — Español (es-ES, also served to es-MX)

Neutral Spanish on purpose: es-MX is the 3rd language of new installs.

### Descripción breve

Un reproductor rápido y moderno para tu colección de música local en Windows 11. Reproduce tus
archivos MP3, FLAC, ALAC/M4A, WAV y WMA, organiza bibliotecas grandes automáticamente y añade listas inteligentes,
letras, radio por internet y estadísticas de escucha. Gratis, sin cuenta y sin anuncios.

### Descripción

Rok es un reproductor para quienes tienen su propia música.

Elige tu carpeta de Música y Rok crea una biblioteca a partir de tus archivos MP3, FLAC, ALAC/M4A, WAV y WMA:
álbumes, artistas, géneros y carátulas, enriquecidos automáticamente. Sigue siendo rápido con
100 000 canciones o más.

POR QUÉ ROK
• Hecho para archivos locales: tu colección, en tu disco, sin suscripción.
• App nativa de Windows 11: diseño Fluent, tema claro/oscuro, teclas multimedia.
• Gratis, sin anuncios, sin cuenta.

ESCUCHAR
• Fundido cruzado, temporizador de apagado, cola editable por canción, álbum, artista o género.
• Letras sincronizadas, incluidas las integradas en los archivos.
• Radio por internet: miles de emisoras o cualquier URL de streaming.
• Pausa automática durante las llamadas de Teams.

ORGANIZAR
• Listas inteligentes que se actualizan solas (género, año, reproducciones, valoración…).
• Importar y exportar listas.
• Edita los datos de álbumes y artistas en Rok sin modificar nunca tus archivos; carátulas y metadatos automáticos.

REDESCUBRIR
• Estadísticas de escucha por álbum y artista, aniversarios de álbumes, «Sorpréndeme».
• API local: controla la reproducción desde tus scripts y herramientas de automatización.
• Discord Rich Presence.

Formatos compatibles: MP3, FLAC, M4A (AAC y ALAC), WAV, WMA y AIFF.

### Términos de búsqueda

`reproductor de música` · `reproductor FLAC` · `reproductor MP3` · `biblioteca musical` ·
`reproductor ALAC` · `listas inteligentes` · `radio por internet`

---

## 4. Campaign IDs (`cid`)

Every outbound link carries its own `cid`. Partner Center → Analytics → Acquisitions →
"Campaign" then shows installs per channel.

Base URL: `https://apps.microsoft.com/detail/9NX19R28Q92S?cid=<cid>`

| Channel | cid |
|---|---|
| rok-site (hero + CTA buttons) | `rok-site` |
| AlternativeTo | `alternativeto` |
| Reddit (one per sub) | `reddit-musichoarder`, `reddit-windows11`, `reddit-software` |
| Hacker News | `hn` |
| Product Hunt | `producthunt` |
| Discord Rich Presence button | `discord-rpc` |
| GitHub README | `github` |
| Press / download sites | `press-<site>` (e.g. `press-neowin`) |

The site currently uses `cid=DevShareMCLPCS` (Partner Center default share link) on all three
buttons: replace with `cid=rok-site`.

---

## 5. AlternativeTo submission

- **Name**: Rok
- **URL**: https://rok.fpc-france.com
- **Platforms**: Windows
- **License**: Free, Open Source (GPL-3.0) — the repo is public
- **Category**: Music & Audio → Music Player
- **Tagline**: Modern Windows 11 player for your local FLAC, ALAC & MP3 collection
- **Description**: reuse the English short description above.
- **Alternative to**: MusicBee, foobar2000, Dopamine, AIMP, MediaMonkey, Groove Music, iTunes
- **Tags**: `local-music`, `flac`, `alac`, `mp3`, `music-library`, `smart-playlists`, `lyrics`,
  `internet-radio`, `windows-11`, `fluent-design`, `ad-free`
- **Features to tick**: Ad-free, Lightweight, Dark mode, Lyrics, Smart playlists,
  Internet radio, Crossfade, Discord integration.
- **Do not tick**: Tag editor, Gapless playback — Rok never writes to your files and crossfades instead of gapless.
- **Screenshots**: same order as the Store.

After approval: ask the first retained users (or friends) to "like" it and add Rok as an
alternative on each listed app page — ranking follows likes.
