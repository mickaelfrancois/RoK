# Launch posts (step 3)

> **Publish after the release that ships GH#400** (M4A/ALAC, WAV, WMA and AIFF support): the texts below already list these formats.

Ready-to-post texts for the promotion wave that follows release 1.18.4 (onboarding, radio
suggestions, rating prompt). Every link carries its `cid` (see `store-listing.md` §4) so
Partner Center shows installs per channel.

Store: `https://apps.microsoft.com/detail/9NX19R28Q92S?cid=<cid>`
Site: `https://rok.fpc-france.com` · Code: `https://github.com/mickaelfrancois/RoK`

## Schedule

One channel per day, never the same day: you must be available for 3–4 hours after each
post to answer every comment — the first hour decides the ranking everywhere.

| Day | Channel | When | cid |
|---|---|---|---|
| D1 (Tue) | r/musichoarder | 15:00–16:00 Paris (morning US East) | `reddit-musichoarder` |
| D3 (Thu) | Show HN | 14:00–16:00 Paris (8–10 am US East) | `hn` |
| D6 (Tue) | Product Hunt | 09:01 Paris (00:01 PT, the day resets) | `producthunt` |
| D8+ | r/software, r/Windows11 (self-promo thread only) | same slot as D1 | `reddit-software`, `reddit-windows11` |

Before D1:
- [ ] AlternativeTo submitted and approved (it is the page people google after the posts).
- [ ] 1 GIF (10–15 s: open the library, scroll albums, play, lyrics) + 4 screenshots.
- [ ] Read each subreddit's rules the same day; r/musichoarder accepts software posts from
      the developer if they are honest and answered — never post the link without text.
- [ ] Reddit account with some history (comment in the sub for a week before posting).

Rules of thumb: write as the developer, in the first person; say what Rok does **not** do
before someone else does; no superlatives; never ask for upvotes (Reddit and HN penalise it).

---

## 1. r/musichoarder

**Title**

> I built a free, open-source Windows 11 player for big local FLAC/ALAC/MP3 libraries — it never rewrites your tags

**Body**

> Hi all, I'm the developer of Rok. I've been building it for my own collection (tens of
> thousands of FLAC and MP3 files) because I wanted a native Windows 11 player that stays fast
> on a big library and never rewrites my tags.
>
> What it does:
>
> - Scans your folders and builds the library from your tags: albums, artists, genres, covers.
>   Stays responsive past 100k tracks.
> - **Your audio files are never modified.** Rok never writes tags: edits you make to albums or
>   artists (names, years, genres) live in Rok's own database.
> - Missing covers, artist pictures and lyrics are fetched automatically — one switch turns it
>   off. When it does, it saves them as sidecar files (`cover.jpg`, `artist.jpg`, `.lrc`)
>   next to your music, and never overwrites one that already exists.
> - Smart playlists (genre, year, play count, rating, last played…), M3U import/export.
> - Synced lyrics, including lyrics embedded in the tags, or a `.lrc` next to the file.
> - Per-album and per-artist listening stats, album anniversaries.
> - Gapless playback and crossfade, ReplayGain (track/album/auto), 10-band EQ, sleep timer, auto-pause during Teams/Zoom/Discord calls.
> - An optional local HTTP API: drive playback, read the current track and queue, start
>   playlists or rate tracks from your own scripts, a Stream Deck or Home Assistant.
> - Free, no account, no ads. GPL-3.0.
>
> What it does **not** do (yet), so you don't waste your time:
>
> - Formats: MP3, FLAC, ALAC/M4A, WAV, WMA and AIFF. No Opus/OGG or DSD for now — Rok tells you how
>   many unsupported files it found so I can see what to add first.
> - No tag editor yet.
> - Windows 11 24H2 or later only, from the Microsoft Store.
>
> Store: https://apps.microsoft.com/detail/9NX19R28Q92S?cid=reddit-musichoarder
> Source: https://github.com/mickaelfrancois/RoK
>
> I'd love feedback from people with serious libraries: what breaks, what's slow, and which
> format you'd need before you could switch.

**Prepared answers** (the questions this sub always asks)

- *foobar2000 / MusicBee do all this.* — They do more, and I use them too. Rok is for people who
  want a native Fluent UI and a library view built around album covers, without plugins or skins.
- *Why the Store?* — Signed package, automatic updates, clean uninstall. The code is on GitHub
  if you prefer to build it.
- *Telemetry?* — Anonymous usage events (start, pages, crash reports) to find bugs; one switch
  in Options turns it off. The client code is in `src/Rok.Infrastructure/Telemetry`.
- *Does it write into my music folders?* — Only sidecars it fetched or extracted (`cover.jpg`,
  `artist.jpg`, `.lrc`/`.txt`), only when missing, and never the audio files. Turn off data
  retrieval in Options and it writes nothing.
- *Gapless?* — Yes. Consecutive tracks of an album always play without gaps (live albums,
  classical), even with crossfade on; crossfade only kicks in when the album changes.

---

## 2. Show HN

**Title** (≤ 80 chars)

> Show HN: Rok – a native Windows 11 music player for local FLAC/ALAC/MP3 libraries

**URL**: `https://github.com/mickaelfrancois/RoK` (HN prefers the code over a store page).

**First comment** (post it right after submitting)

> Hi HN, I'm the author. Rok is a free, GPL-3.0 music player for people who still own their
> music as files. I started it because the native Windows options stopped at Groove, and the
> powerful ones (foobar2000, MusicBee) are WinForms-era UIs.
>
> Some technical notes, since that's usually what's interesting here:
>
> - .NET 10 + WinUI 3 (Windows App SDK 2.5), packaged as MSIX. Clean architecture: Domain /
>   Application / Infrastructure / Presentation, CQRS handlers dispatched by a source-generated
>   mediator (no reflection).
> - Playback on NAudio 3 with Media Foundation for decoding; crossfade and a 10-band EQ are
>   done in the sample pipeline.
> - Library in SQLite through Dapper, with hand-written migrations. The import runs off the UI
>   thread and streams albums into virtualised lists so a first scan of 100k tracks stays
>   usable. Most of my crash fixes were WinUI virtualisation pitfalls (a list nested in a
>   ScrollViewer silently loses virtualisation, for example).
> - It never writes tags: user edits are stored in the database and overlaid on file metadata.
>   The only files it creates are missing sidecars (covers, `.lrc` lyrics).
> - An optional local HTTP API (off by default, LAN access opt-in) exposes playback, status,
>   queue, playlists and ratings, so Rok can be scripted or wired into home automation.
> - Call detection: it pauses when Teams/Zoom/Discord open an audio session, found by walking
>   the process tree (new Teams plays call audio from a WebView2 child process).
>
> Limits: no Opus/OGG or DSD for now, Windows 11 24H2+.
>
> Store link if you just want to try it:
> https://apps.microsoft.com/detail/9NX19R28Q92S?cid=hn
>
> Happy to answer anything about WinUI 3 in 2026 — the good parts and the rest.

Do not ask anyone to upvote; do not share the direct HN link in groups (the ring detector
kills the post). If it does not take off, HN allows one more try a few weeks later with a
different angle (e.g. "What I learned shipping a WinUI 3 app to the Store").

---

## 3. Product Hunt

- **Name**: Rok
- **Tagline** (≤ 60 chars): `A native Windows 11 player for your FLAC & ALAC library`
- **Description** (≤ 260 chars):
  > Rok turns your local FLAC, ALAC and MP3 folders into a beautiful, fast library: covers, smart
  > playlists, synced lyrics, listening stats, internet radio and a local API. Free, open
  > source, no account, and it never modifies your audio files.
- **Topics**: Music, Windows, Open Source
- **Link**: `https://apps.microsoft.com/detail/9NX19R28Q92S?cid=producthunt`
- **Gallery**: GIF first, then albums grid, now playing + lyrics, album stats, smart playlists
  (1270×760).
- **Pricing**: Free.

**Maker comment**

> Hi Product Hunt! I'm Mickaël, I build Rok on my evenings.
>
> Streaming won, but a lot of us still have years of carefully tagged MP3 and FLAC files — and
> on Windows 11 the built-in player treats them as an afterthought. Rok is my answer: a native
> Fluent app that makes a big local collection feel like a modern music service, without an
> account, ads, or rewriting your tags.
>
> Things I'm proud of: it stays fast past 100k tracks, smart playlists update themselves, and
> the album page shows your own listening history for that album.
>
> What would make you switch from your current player? I read every comment.

Launching alone gives few votes; the value is the permanent PH page (backlink, SEO) and the
comments. Tell the people who already use Rok (Discord, friends) the launch date in advance,
without asking for upvotes.

---

## 4. r/software / r/Windows11

Short version of §1: title `Rok – free, open-source music player for local FLAC/ALAC/MP3 files (Windows 11)`,
keep the "what it does / does not do" lists, drop the hoarder-specific questions, swap the cid.
r/Windows11 only allows it in the weekly self-promotion thread: check the pinned post.

---

## After each post

- Note in this file: date, link, upvotes/points after 24 h, comments, installs for the `cid`
  in Partner Center after 7 days.
- Turn every recurring request into a GitHub issue and answer the commenter with its link.
