"""Fill a Partner Center listing CSV export with the texts below.

Usage: python generate_store_listing.py <partner-center-export.csv> [output.csv]

Output defaults to store-listing.csv next to this script. Only ShortDescription,
Description, Feature1-20 and SearchTerm1-7 are rewritten for the four locales
(plus two one-off fixes); every other cell is kept byte for byte.
"""
import csv, io, os, sys

if len(sys.argv) < 2:
    print(__doc__); sys.exit(1)

SRC = sys.argv[1]
DST = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "store-listing.csv")

T = {}

T["en-us"] = {
    "ShortDescription": "A fast, modern player for your local music collection on Windows 11. Plays your MP3 and FLAC files, organizes big libraries automatically, and adds smart playlists, lyrics, internet radio and listening stats. Free, no account, no ads.",
    "Description": """Rok is a music player for people who own their music.

Point Rok at your Music folder and it builds a beautiful library from your MP3 and FLAC files: albums, artists, genres and covers, enriched automatically. It stays fast with collections of 100,000 tracks and more.

WHY ROK
• Made for local files: your collection, on your disk, no streaming subscription needed.
• Native Windows 11 app: Fluent design, light and dark themes, media keys and Windows media controls.
• Free, no ads, no account.

LISTEN
• Smooth crossfade, sleep timer, queue editing by song, album, artist or genre.
• Synced lyrics, including lyrics embedded in your files.
• Internet radio: search thousands of stations or play any stream URL.
• Automatically pauses during Teams calls.

ORGANIZE
• Smart playlists that update themselves (genre, year, play count, rating…).
• Import and export playlists.
• Edit tags, artists and albums; missing covers and metadata retrieved automatically.

REDISCOVER
• Listening stats per album and artist, album anniversaries, "Surprise me" picks.
• Rok companion: control playback from your phone's browser on the same network.
• Discord Rich Presence: show friends what you are listening to.

Supported formats: MP3 and FLAC.""",
    "Features": [
        "Plays your local MP3 and FLAC collection, no streaming subscription needed",
        "Stays fast with huge libraries (100,000+ tracks)",
        "Album covers and metadata retrieved automatically",
        "Smart playlists that update themselves",
        "Synced lyrics, including lyrics embedded in your files",
        "Internet radio with thousands of stations",
        "Smooth crossfade and sleep timer",
        "Listening stats and album anniversaries",
        "Web companion: control playback from your phone",
        "Automatic pause during Teams calls",
        "Discord Rich Presence",
        "Media keys and Windows media controls",
        "Compact mode and party mode",
        "Native Windows 11 design, light and dark themes",
        "Free, no ads, no account",
    ],
    "SearchTerms": ["local music player", "FLAC player", "MP3 player", "music library", "offline music", "smart playlists", "internet radio"],
}

T["fr-fr"] = {
    "ShortDescription": "Un lecteur rapide et moderne pour votre collection de musique locale sous Windows 11. Lit vos fichiers MP3 et FLAC, organise automatiquement les grandes bibliothèques, et ajoute playlists intelligentes, paroles, radios internet et statistiques d’écoute. Gratuit, sans compte, sans pub.",
    "Description": """Rok est un lecteur pour ceux qui possèdent leur musique.

Indiquez votre dossier Musique : Rok construit une belle bibliothèque à partir de vos fichiers MP3 et FLAC — albums, artistes, genres et pochettes, enrichis automatiquement. Il reste rapide avec 100 000 titres et plus.

POURQUOI ROK
• Pensé pour les fichiers locaux : votre collection, sur votre disque, sans abonnement de streaming.
• Application Windows 11 native : design Fluent, thèmes clair et sombre, touches multimédia et contrôles multimédia de Windows.
• Gratuit, sans pub, sans compte.

ÉCOUTER
• Fondu enchaîné, minuterie de sommeil, file d’attente modifiable par titre, album, artiste ou genre.
• Paroles synchronisées, y compris celles intégrées à vos fichiers.
• Radios internet : des milliers de stations ou n’importe quelle URL de flux.
• Pause automatique pendant les appels Teams.

ORGANISER
• Playlists intelligentes qui se mettent à jour seules (genre, année, nombre d’écoutes, note…).
• Import et export de playlists.
• Édition des tags, artistes et albums ; pochettes et métadonnées manquantes récupérées automatiquement.

REDÉCOUVRIR
• Statistiques d’écoute par album et artiste, anniversaires d’albums, « Surprends-moi ».
• Rok companion : pilotez la lecture depuis le navigateur de votre téléphone, sur le même réseau.
• Discord Rich Presence : montrez à vos amis ce que vous écoutez.

Formats pris en charge : MP3 et FLAC.""",
    "Features": [
        "Lit votre collection locale MP3 et FLAC, sans abonnement de streaming",
        "Reste rapide avec de très grandes bibliothèques (100 000 titres et plus)",
        "Pochettes et métadonnées récupérées automatiquement",
        "Playlists intelligentes qui se mettent à jour seules",
        "Paroles synchronisées, y compris celles intégrées à vos fichiers",
        "Radios internet : des milliers de stations",
        "Fondu enchaîné et minuterie de sommeil",
        "Statistiques d’écoute et anniversaires d’albums",
        "Companion web : pilotez la lecture depuis votre téléphone",
        "Pause automatique pendant les appels Teams",
        "Discord Rich Presence",
        "Touches multimédia et contrôles multimédia de Windows",
        "Mode compact et mode soirée",
        "Design Windows 11 natif, thèmes clair et sombre",
        "Gratuit, sans pub, sans compte",
    ],
    "SearchTerms": ["lecteur de musique", "lecteur FLAC", "lecteur MP3", "bibliothèque musicale", "musique hors ligne", "playlists intelligentes", "radio internet"],
}

T["es-es"] = {
    "ShortDescription": "Un reproductor rápido y moderno para tu colección de música local en Windows 11. Reproduce tus archivos MP3 y FLAC, organiza bibliotecas grandes automáticamente y añade listas inteligentes, letras, radio por internet y estadísticas de escucha. Gratis, sin cuenta y sin anuncios.",
    "Description": """Rok es un reproductor para quienes tienen su propia música.

Elige tu carpeta de Música y Rok crea una biblioteca a partir de tus archivos MP3 y FLAC: álbumes, artistas, géneros y carátulas, enriquecidos automáticamente. Sigue siendo rápido con 100 000 canciones o más.

POR QUÉ ROK
• Hecho para archivos locales: tu colección, en tu disco, sin suscripción de streaming.
• App nativa de Windows 11: diseño Fluent, temas claro y oscuro, teclas multimedia y controles multimedia de Windows.
• Gratis, sin anuncios, sin cuenta.

ESCUCHAR
• Fundido cruzado, temporizador de apagado, cola editable por canción, álbum, artista o género.
• Letras sincronizadas, incluidas las integradas en tus archivos.
• Radio por internet: miles de emisoras o cualquier URL de streaming.
• Pausa automática durante las llamadas de Teams.

ORGANIZAR
• Listas inteligentes que se actualizan solas (género, año, reproducciones, valoración…).
• Importar y exportar listas de reproducción.
• Edición de etiquetas, artistas y álbumes; carátulas y metadatos que faltan, recuperados automáticamente.

REDESCUBRIR
• Estadísticas de escucha por álbum y artista, aniversarios de álbumes, «Sorpréndeme».
• Rok companion: controla la reproducción desde el navegador de tu móvil, en la misma red.
• Discord Rich Presence: muestra a tus amigos lo que escuchas.

Formatos compatibles: MP3 y FLAC.""",
    "Features": [
        "Reproduce tu colección local MP3 y FLAC, sin suscripción de streaming",
        "Sigue siendo rápido con bibliotecas enormes (más de 100 000 canciones)",
        "Carátulas y metadatos recuperados automáticamente",
        "Listas inteligentes que se actualizan solas",
        "Letras sincronizadas, incluidas las integradas en tus archivos",
        "Radio por internet con miles de emisoras",
        "Fundido cruzado y temporizador de apagado",
        "Estadísticas de escucha y aniversarios de álbumes",
        "Companion web: controla la reproducción desde tu móvil",
        "Pausa automática durante las llamadas de Teams",
        "Discord Rich Presence",
        "Teclas multimedia y controles multimedia de Windows",
        "Modo compacto y modo fiesta",
        "Diseño nativo de Windows 11, temas claro y oscuro",
        "Gratis, sin anuncios, sin cuenta",
    ],
    "SearchTerms": ["reproductor de música", "reproductor FLAC", "reproductor MP3", "biblioteca musical", "música sin conexión", "listas inteligentes", "radio por internet"],
}

T["uk-ua"] = {
    "ShortDescription": "Швидкий сучасний плеєр для вашої локальної музичної колекції у Windows 11. Відтворює файли MP3 і FLAC, автоматично впорядковує великі бібліотеки та додає розумні плейлисти, тексти пісень, інтернет-радіо й статистику прослуховувань. Безкоштовно, без облікового запису, без реклами.",
    "Description": """Rok — музичний плеєр для тих, хто має власну музику.

Вкажіть папку «Музика», і Rok створить гарну бібліотеку з ваших файлів MP3 і FLAC: альбоми, виконавці, жанри та обкладинки, автоматично доповнені. Він залишається швидким навіть із колекціями від 100 000 треків.

ЧОМУ ROK
• Створений для локальних файлів: ваша колекція на вашому диску, без підписки на стрімінг.
• Нативний застосунок для Windows 11: дизайн Fluent, світла й темна теми, мультимедійні клавіші та системні елементи керування.
• Безкоштовно, без реклами, без облікового запису.

СЛУХАЙТЕ
• Плавний перехід між треками, таймер сну, редагування черги за піснею, альбомом, виконавцем чи жанром.
• Синхронізовані тексти пісень, зокрема вбудовані у файли.
• Інтернет-радіо: тисячі станцій або будь-яка URL-адреса потоку.
• Автоматична пауза під час дзвінків у Teams.

ВПОРЯДКОВУЙТЕ
• Розумні плейлисти, що оновлюються самі (жанр, рік, кількість прослуховувань, оцінка…).
• Імпорт і експорт плейлистів.
• Редагування тегів, виконавців і альбомів; відсутні обкладинки та метадані завантажуються автоматично.

ВІДКРИВАЙТЕ ЗАНОВО
• Статистика прослуховувань за альбомами й виконавцями, річниці альбомів, «Здивуй мене».
• Rok companion: керуйте відтворенням із браузера телефона в тій самій мережі.
• Discord Rich Presence: покажіть друзям, що ви слухаєте.

Підтримувані формати: MP3 і FLAC.""",
    "Features": [
        "Відтворює вашу локальну колекцію MP3 і FLAC без підписки на стрімінг",
        "Залишається швидким із величезними бібліотеками (понад 100 000 треків)",
        "Обкладинки та метадані завантажуються автоматично",
        "Розумні плейлисти, що оновлюються самі",
        "Синхронізовані тексти пісень, зокрема вбудовані у файли",
        "Інтернет-радіо з тисячами станцій",
        "Плавний перехід між треками й таймер сну",
        "Статистика прослуховувань і річниці альбомів",
        "Вебкомпаньйон: керуйте відтворенням із телефона",
        "Автоматична пауза під час дзвінків у Teams",
        "Discord Rich Presence",
        "Мультимедійні клавіші та системні елементи керування Windows",
        "Компактний режим і режим вечірки",
        "Нативний дизайн Windows 11, світла й темна теми",
        "Безкоштовно, без реклами, без облікового запису",
    ],
    "SearchTerms": ["музичний плеєр", "FLAC плеєр", "MP3 плеєр", "музична бібліотека", "local music player", "розумні плейлисти", "інтернет-радіо"],
}

EN_RELEASE_FIX = ("- Información mensual sobre tus hábitos de escucha", "- Monthly insights into your listening habits")

raw = open(SRC, encoding="utf-8-sig", newline="").read()
rows = list(csv.reader(io.StringIO(raw)))
header = rows[0]
col = {name: i for i, name in enumerate(header)}
by_field = {r[0]: r for r in rows[1:] if r}

errors = []
for loc, data in T.items():
    c = col[loc]
    by_field["ShortDescription"][c] = data["ShortDescription"]
    by_field["Description"][c] = data["Description"]
    for i in range(20):
        by_field[f"Feature{i + 1}"][c] = data["Features"][i] if i < len(data["Features"]) else ""
    for i in range(7):
        by_field[f"SearchTerm{i + 1}"][c] = data["SearchTerms"][i]
    if len(data["ShortDescription"]) > 1000: errors.append(f"{loc} short > 1000")
    if len(data["Description"]) > 10000: errors.append(f"{loc} desc > 10000")
    errors += [f"{loc} feature > 200: {f}" for f in data["Features"] if len(f) > 200]
    errors += [f"{loc} term > 30: {t}" for t in data["SearchTerms"] if len(t) > 30]
    words = sum(len(t.split()) for t in data["SearchTerms"])
    if words > 21: errors.append(f"{loc} search words {words} > 21")

en = col["en-us"]
by_field["ReleaseNotes"][en] = by_field["ReleaseNotes"][en].replace(*EN_RELEASE_FIX)
es = col["es-es"]
for f in ("DevStudio", "CopyrightTrademarkInformation"):
    if not by_field[f][es]:
        by_field[f][es] = by_field[f][en]

if errors:
    print("\n".join(errors)); sys.exit(1)

def field(v):
    if any(ch in v for ch in ',"\n\r') or v != v.strip(" "):
        return '"' + v.replace('"', '""') + '"'
    return v

def serialize(rs):
    return "\r\n".join(",".join(field(v) for v in r) for r in rs)

original_rows = list(csv.reader(io.StringIO(raw)))
if serialize(original_rows) != raw:
    print("roundtrip mismatch, aborting"); sys.exit(2)

open(DST, "w", encoding="utf-8-sig", newline="").write(serialize(rows))

changed = [(r[0], header[i]) for r, o in zip(rows, original_rows) for i in range(len(r)) if r[i] != o[i]]
print("written", DST, len(rows), "rows,", len(changed), "cells changed")
print(sorted({f for f, _ in changed}))
