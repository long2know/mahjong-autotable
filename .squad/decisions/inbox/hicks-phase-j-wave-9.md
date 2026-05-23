# Hicks — Phase J Wave 9 (frontend polish)

**Branch:** `stlong/phase-j-wave-9-polish`
**Bundle hashes (Parcel build at completion):**
- JS:  `autotable-src.6e0d2167.js` (1.27 MB)
- CSS: `autotable-src.df85b4c4.css` + `autotable-src.95ecc0f0.css` + `autotable-src.6633d8fb.css`
- ESM: `esm.eb93de05.js` (395 KB)

## Scope shipped

Wave 9 delivers the four frontend polish tracks called out in the
Wave-9 directive. All Bishop-side endpoints are feature-detected so
the frontend ships safely whether or not the matching backend
endpoints (`/api/games/{id}/chat`, `/api/games/{id}/audit`,
`/api/auth/me`-with-role) are merged yet — 404/403 degrade to either
placeholder copy or a hidden surface.

### 1. Chat panel (`src/chat.ts`, ~580 LOC)

Bottom-right docked chat panel with collapse toggle, three channels
(`table`, `spectators`, `private`), 280-char composer, polled
history, and Web Audio chime on new inbound messages (re-uses Wave-3
`Sound.play('claim')`, which honours the existing mute mirror).

**Entry point:** `installChatPanel(client)` — called from `index.ts`
after `attachLobbyClient(client)` (post asset-load, post Game.start).

**Bishop endpoints consumed (feature-detected — 404 → "Chat unavailable" placeholder):**

| Method | Path                            | Use                                |
|--------|---------------------------------|------------------------------------|
| GET    | `/api/games/{id}/chat?since=…`  | history fetch + 6-second poll      |
| POST   | `/api/games/{id}/chat`          | send message (body, channel, recipient)  |

POST body shape (matches Bishop's draft contract):
```json
{ "channel": "table" | "spectators" | "private",
  "recipientPlayerId": "uuid" | undefined,
  "body": "string up to 280 chars" }
```

Response on 429 → user-visible "sending too quickly" toast.
Response on 4xx other → generic "couldn't send" toast.

**LS keys added:**
- `mahjong.chat.collapsed.v1` — boolean, panel collapsed/expanded
- `mahjong.chat.lastSeenIso.v1` — ISO timestamp of last-seen message

**`data-testid` surface:**
- `chat-panel` (root)
- `chat-toggle` (collapse button)
- `chat-channel-select`, `chat-recipient-select`, `chat-recipient-wrap`
- `chat-input`, `chat-send`, `chat-char-count`
- `chat-messages` (scroll container), `chat-unavailable` (placeholder)
- `chat-message-{N}`, `chat-message-{N}-author`, `chat-message-{N}-body`

**Slash commands:** `/clear`, `/help` (purely client-side; `/help`
prints a hint, `/clear` empties the visible scrollback without
touching server state).

### 2. i18n module (`src/i18n.ts` + `src/i18n/{en,zh-Hans,zh-Hant}.json`)

Tiny string-table runtime with `t(key, params?)`, language picker
hook, body[lang] attribute side-effect, and `tPattern(key, legacyName?)`
helper for Wave-8 pattern-name legacy fallback.

**Public API:**
- `installI18n()` — boots the active locale before any other install
  hook runs (called as the very first statement in `index.ts`).
- `t(key, params?)` — look up a string; falls back to en, then key.
- `tPattern(patternKey, legacyName?)` — pattern-namespace shortcut.
- `getLanguage() / setLanguage(pref)` — Auto / en / zh-Hans / zh-Hant.
- `getActiveLocale()` — resolves Auto via `navigator.languages`
  (zh-CN/zh-SG → Hans; zh-TW/zh-HK/zh-MO → Hant; everything else en).
- `onLanguageChange(fn)` → unsubscribe — settings drawer + chat +
  audit subscribe so chrome re-renders without a page reload.
- `mergeServerCatalog(locale, patch)` — escape hatch for Bishop to
  push a server-side catalog override at runtime.

**Catalog scope (3 langs × ~85 keys each):**
- `common.*`        (Apply, Cancel, Save, Reset, OK, Loading…)
- `lobby.*`         (Quick Match, Spectate, Join, …)
- `settings.*`      (Tab labels + every field label in the drawer)
- `chat.*`          (Channel names, placeholder, errors, hint)
- `auth.*`          (Sign in, link/unlink, magic-link copy)
- `replay.*`        (Hand titles, Audit tab/columns, empty state)
- `pattern.*`       (Changsha hand-pattern display names)

**LS field added (shared with Wave-7 settings blob `mahjong.settings.v1`):**
- `lang` — `'auto' | 'en' | 'zh-Hans' | 'zh-Hant'` (default `'auto'`)

**Where `t()` is wired so far:** settings drawer tab strip + every
panel label, chat module's entire UI, audit tab column headers /
empty / unavailable copy.  The drawer also re-renders on
`onLanguageChange(...)` so the picker is visibly live.

Other chrome (lobby tabs, sign-in modal, replay viewer controls) is
still in raw English literals — the keys exist in the catalog, and
future waves can sweep them through `t()` mechanically without
catalog churn.

### 3. CSP tightening — `'unsafe-eval'` removed

Files touched:
- `src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Observability/SecurityHeadersMiddlewareTests.cs`

The Wave-8 default CSP was:
```
script-src 'self' 'unsafe-eval';
```

…the `'unsafe-eval'` permission was for Three.js's WebGL shader
compiler.  Wave-9 audit confirmed the shipped Parcel bundle contains
**zero** `new Function(...)` / `eval(...)` callsites:

```
$ grep -oE 'new Function|\beval\s*\(' src/frontend/autotable/autotable-src.6e0d2167.js | wc -l
0
```

The active Three.js distribution is `three.module.js`, which does NOT
need eval; only `three.webgpu.js` does, and we don't import it.

The new default policy is:
```
script-src 'self' 'wasm-unsafe-eval';
```

`'wasm-unsafe-eval'` is the CSP Level 3 permission that allows
`WebAssembly.compile()` only (no `eval()` / `new Function`).  It's
forward-compatible with any future Three.js loader that compiles a
Draco / KTX wasm decoder, and it's the canonical "landed" signal
Vasquez's `CspHeaderTests` looks for to flip from soft-pass to
enforce.

`Security:CspStrict=true` now drops even `'wasm-unsafe-eval'`,
leaving `script-src 'self'`.  The `CspStrict` config knob default
remains `false`.

Backend tests verified green:
```
$ dotnet test --filter 'FullyQualifiedName~SecurityHeadersMiddlewareTests|FullyQualifiedName~CspHeaderTests'
Passed!  - Failed: 0, Passed: 8
```

The Wave-8 `DefaultCsp_AllowsUnsafeEvalForThreeJs` test was replaced
with `DefaultCsp_DropsUnsafeEvalAfterWave9Audit` — same file, same
test class, flipped assertion + Wave-9 trait.

### 4. Audit replay tab (`src/audit.ts`, ~310 LOC)

Admin-only Audit tab added to the replay viewer next to the existing
Replay tab.  Probes `/api/auth/me` for `claims.role === 'admin'` (or
`'admin'` in `claims.roles[]`) — if the probe fails or the user
isn't an admin, the tab stays hidden via `style.display = 'none'`
(no chrome leakage to non-admins).

**Entry point:** `installAuditTab()` — called from `index.ts` at
module top level (idempotent; bails if `#replay-tab-audit` is
missing from the DOM).

**Glue points into `replay.ts`:** `setAuditGameId(payload.gameId)`
is called from `Replay.openServer()` (server-replay path) and
`setAuditGameId(this.client.lastGameId)` from `Replay.open()`
(in-memory live-capture path).  Both are no-ops when the audit tab
hasn't been installed or the caller isn't an admin.

**Bishop endpoint consumed (feature-detected — 404 → "Audit endpoint unavailable" placeholder; 403 → "Audit data is visible to admins only."):**

| Method | Path                          | Use                                |
|--------|-------------------------------|------------------------------------|
| GET    | `/api/games/{id}/audit`       | list of audit rows for the game    |

Expected row shape (Bishop's draft):
```json
{ "rows": [
  { "index": 0,
    "source": "human" | "bot" | "system",
    "playerId": "uuid?",
    "action": "draw" | "discard" | "claim-pong" | ...,
    "durationMs": 1234,
    "botScore": 0.87,        // optional, present for bot rows
    "claimDecision": "pass"  // optional, present for claim rows
  }, ... ]}
```

**`data-testid` surface:**
- `replay-tab-replay`, `replay-tab-audit`  (the tab strip)
- `replay-pane-replay`, `replay-pane-audit`  (the panes)
- `replay-audit-empty`, `replay-audit-unavailable`, `replay-audit-admin-only`
- `replay-audit-row-{N}`, `replay-audit-row-{N}-source`,
  `…-{N}-duration`, `…-{N}-score`, `…-{N}-action`, `…-{N}-decision`

## Build + verification gates

| Gate                                          | Result |
|-----------------------------------------------|--------|
| `tsc --noEmit --strict` against `src/index.ts`| clean  |
| `parcel build`                                | clean (1.27 MB JS, 9.3s) |
| `dotnet build` (API project)                  | clean  |
| `dotnet test --filter SecurityHeaders\|Csp`   | 8/8    |

## Author-hygiene note

Per the standing rule, every file listed below was committed under
Hicks's authorship with the
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
trailer.  No bundling into Apone's / Bishop's commits this wave.

### Files committed by Hicks

**New:**
- `src/frontend/autotable-src/src/audit.ts`
- `src/frontend/autotable-src/src/chat.ts`
- `src/frontend/autotable-src/src/i18n.ts`
- `src/frontend/autotable-src/src/i18n/en.json`
- `src/frontend/autotable-src/src/i18n/zh-Hans.json`
- `src/frontend/autotable-src/src/i18n/zh-Hant.json`
- `src/frontend/autotable/autotable-src.6e0d2167.js`  (rebuilt bundle)
- `src/frontend/autotable/autotable-src.95ecc0f0.css` (rebuilt bundle)
- `src/frontend/autotable/esm.eb93de05.js`           (rebuilt bundle)
- `.squad/decisions/inbox/hicks-phase-j-wave-9.md`   (this memo)

**Modified:**
- `src/frontend/autotable-src/index.html`            (chat panel + audit tab markup)
- `src/frontend/autotable-src/src/index.ts`          (install hook calls)
- `src/frontend/autotable-src/src/replay.ts`         (setAuditGameId glue)
- `src/frontend/autotable-src/src/settings-drawer.ts`(Language picker + t())
- `src/frontend/autotable-src/src/style.css`         (~280 LOC chat + replay-tab CSS)
- `src/frontend/autotable-src/tsconfig.json`         (resolveJsonModule = true)
- `src/frontend/autotable/index.html`                (Parcel emission)
- `src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Observability/SecurityHeadersMiddlewareTests.cs`

**Removed:**
- `src/frontend/autotable/autotable-src.5d56642c.js`   (stale Wave-8 bundle)
- `src/frontend/autotable/autotable-src.1a66bab2.css`  (stale Wave-8 CSS)

## Blockers / open questions

None known.  All three new Bishop endpoints (`/api/games/{id}/chat`,
`/api/games/{id}/audit`, `/api/auth/me` role claim) feature-detect
404/403 gracefully so the frontend ships safely ahead of (or
without) the matching backend.
