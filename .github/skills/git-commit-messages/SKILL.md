---
name: git-commit-messages
description: "Rules and patterns for writing standard Git commit messages following the Conventional Commits specification. Activate when the user asks to write, review, or improve a Git commit message, or when generating a commit summary for staged changes. Covers type selection, scope, subject line, body, breaking changes, and multi-commit scenarios. DO NOT USE FOR: branch naming, PR descriptions, changelogs (unless derived from commits), or non-Git version control systems."
---

# Git Commit Message Standard

All commits in this repository must follow the **Conventional Commits** specification.
Each entry below describes the rule, why it matters, and a BAD→GOOD transformation.

---

## Format

```
<type>(<scope>): <subject>

[optional body]

[optional footer(s)]
```

- **type**: mandatory, lowercase
- **scope**: optional, lowercase, in parentheses — represents the module or layer affected
- **subject**: mandatory, imperative mood, no capital first letter, no period at end, max 72 chars
- **body**: optional, wrapped at 72 chars, explains *what* and *why* (not *how*)
- **footer**: optional — used for breaking changes or issue references

---

## CM-01: Choosing the Right Type

**Smell**: Using `fix` for everything, or using `update`/`change` which are not valid types.

**Why it's bad**: Types drive changelogs, semantic versioning, and filtering. Wrong types hide intent.

**Valid types:**

| Type       | When to use |
|------------|-------------|
| `feat`     | A new feature visible to the user |
| `fix`      | A bug fix |
| `refactor` | Code change that is neither a fix nor a feature |
| `perf`     | A change that improves performance |
| `test`     | Adding or correcting tests |
| `docs`     | Documentation only changes |
| `style`    | Formatting, whitespace, no logic change |
| `build`    | Changes to build system or dependencies (`.csproj`, NuGet) |
| `ci`       | Changes to CI/CD configuration files |
| `chore`    | Other changes that don't modify src or test files |
| `revert`   | Reverts a previous commit |

```
# BAD
git commit -m "update lyrics service"
git commit -m "changed the player bug"

# GOOD
git commit -m "fix(lyrics): handle null response from lyrics provider"
git commit -m "refactor(player): extract volume control into dedicated service"
```

---

## CM-02: Subject Line Rules

**Smell**: Subject starting with a capital letter, ending with a period, or describing *how* instead of *what*.

**Why it's bad**: Inconsistent subjects break tooling (changelog generators, search) and reduce readability.

**Rules:**
- Use imperative mood: `add`, `fix`, `remove`, `update` — not `added`, `fixes`, `removing`
- No capital first letter after the colon
- No period at the end
- Max 72 characters

```
# BAD
feat(lyrics): Added support for synced lyrics.
fix(player): Fixing the crash when skipping track

# GOOD
feat(lyrics): add support for synced lyrics
fix(player): prevent crash when skipping track
```

---

## CM-03: Scope Selection for This Project

**Smell**: No scope, or a scope too vague (`app`, `code`, `misc`).

**Why it's bad**: Scope helps filter commits per layer or feature area.

**Recommended scopes for Rok:**

| Scope         | Layer / Area |
|---------------|--------------|
| `player`      | Audio playback engine (NAudio) |
| `lyrics`      | Lyrics fetch, display, sync |
| `library`     | Music library, import, scan |
| `album`       | Album management |
| `artist`      | Artist management |
| `track`       | Track management |
| `playlist`    | Playlist management |
| `ui`          | WinUI3 views, controls |
| `vm`          | ViewModels |
| `options`     | Application settings/options |
| `api`         | Web API feature |
| `cli`         | Command-line interface |
| `stats`       | Statistics feature |
| `db`          | Data access / SQLite |
| `telemetry`   | Telemetry / Serilog |
| `discord`     | Discord Rich Presence |
| `build`       | Project files, NuGet packages |
| `ci`          | GitHub Actions workflows |

```
# BAD
feat: add lyrics
fix(app): null ref

# GOOD
feat(lyrics): add fallback to secondary lyrics provider
fix(lyrics): resolve null reference when track has no duration
```

---

## CM-04: Body — When and How to Write One

**Smell**: No body for complex or non-obvious changes.

**Why it's bad**: Future maintainers (and yourself) need context: *why* was this changed, not just *what*.

**Rules:**
- Separate body from subject with a blank line
- Wrap at 72 characters
- Explain the motivation and context, not the implementation details

```
# BAD
fix(lyrics): handle api timeout

# GOOD
fix(lyrics): handle api timeout when fetching synced lyrics

The lyrics provider sometimes returns a 504 after 30 seconds on tracks
with no cached result. Added a 10 s timeout with a fallback to the raw
lyrics endpoint to avoid blocking the UI thread.
```

---

## CM-05: Breaking Changes

**Smell**: Breaking change buried in subject or not declared at all.

**Why it's bad**: Breaking changes must be explicit for semantic versioning and consumers.

**Rules:**
- Add `BREAKING CHANGE:` in the footer (separated by blank line)
- Or append `!` after the type/scope: `feat(api)!:`

```
# BAD
feat(api): change /status response format

# GOOD
feat(api)!: change /status response format

BREAKING CHANGE: the `state` field is renamed to `playbackState` in the
/api/status response. Clients must update their integration accordingly.
```

---

## CM-06: Referencing Issues

**Smell**: No issue reference for bug fixes or feature work tracked in GitHub.

**Why it's bad**: Traceability is lost — impossible to link a commit to its original issue automatically.

```
# BAD
fix(player): prevent double play on startup

# GOOD
fix(player): prevent double play on startup

Closes #142
```

---

## CM-07: Avoid WIP / Noise Commits on Main Branches

**Smell**: Commits like `wip`, `temp`, `asdf`, `fix fix`, `test` pushed to `main` or `develop`.

**Why it's bad**: Pollutes history, breaks changelog generation, makes bisect harder.

**Rule**: Squash or reword before merging. Use `git rebase -i` to clean up.

```
# BAD (sequence of commits on a feature branch before merging)
wip
fix
test again
final fix for real

# GOOD (after squash/reword)
feat(lyrics): add synchronized lyrics support with fallback
```

---

## Quick Reference Card

```
feat(scope): add something new
fix(scope): correct a bug
refactor(scope): restructure without behavior change
perf(scope): improve performance
test(scope): add or fix tests
docs(scope): update documentation
style(scope): formatting only
build(scope): dependency or project file change
ci(scope): pipeline configuration
chore(scope): maintenance task
revert(scope): revert a previous commit
```
