# Frontend routing contract

> Authoring lane: Hicks (Frontend lead). Updates land alongside any
> changes to `src/frontend/autotable-src/src/action-router.ts` or
> the `manifest.webmanifest` `shortcuts[]` array. Cross-reference
> from `docs/frontend-pwa-audit.md` (manifest schema) and
> `src/frontend/autotable-src/tests/selectors.md` (selector
> contract for the producer side).

## §1 — Why a router?

The autotable frontend has historically used three flavours of
client-side routing:

| Flavour | Owner | Trigger | Example |
|---------|-------|---------|---------|
| **Pathname routes** | Apone (backend) | Initial server-rendered HTML or hard navigation | `/lobby`, `/tournament/list` |
| **Hash routes** | Hicks (frontend) | `window.location.hash` parsing in `lobby-app.ts` (W6) | `#/spectate/<tableId>` |
| **Game-bootstrap query** | Hicks (frontend) | `window.location.search !== ''` in `index.ts` (W2) | `?game=<tableId>` |

Wave 11 introduces a fourth flavour: **PWA action shortcuts**.
These are short, human-friendly query keywords used by the
manifest `shortcuts[]` array (and any future deep-links from
operating-system task switchers, store listings, voice
assistants, etc.):

```
/?action=new-game
/?action=spectate
/?action=tournament
```

The W2 game-bootstrap guard treats *any* non-empty
`window.location.search` as a game-bootstrap trigger, imports
the heavy `three-renderer-big` chunk, and tries to enter a
non-existent table when the query string is actually an
`?action=*` shortcut. The action router resolves the conflict
by intercepting the action keyword first, dispatching the
side-effect, stripping the param from the URL, and returning
`true` to the boot sequence so the renderer import is skipped.

## §2 — Module surface

The contract lives in **`src/action-router.ts`** and is the
sole owner of `?action=*` interpretation. The public surface:

```ts
export function parseActionFromUrl(): string | null;
export function clearActionParam(): void;
export function handlePwaActionFromUrl(): boolean;
```

### `parseActionFromUrl()`

Reads `window.location.search`, extracts the `action` parameter,
trims + lowercases it, and returns the **canonical** action
keyword. Returns `null` for:

- Missing `?action=`
- Empty/whitespace value
- Unsupported keyword

The plural alias `tournaments` (W10 manifest typo) is normalised
to the canonical singular `tournament`. The router stays
permissive on the input form so legacy installed-PWA shortcuts
continue to work without forcing users to re-install.

Does **not** modify the URL — pure parse.

### `clearActionParam()`

Removes the `action` query parameter from the URL via
`history.replaceState`, preserving any other query parameters
the caller put on the URL (e.g. UTM tracking tags, A/B test
flags, query-string-passed auth tokens for the SPA Apone
considered in W4). No-op on browsers without the History API.

### `handlePwaActionFromUrl()`

Top-level dispatch. Calls `parseActionFromUrl()`, routes the
side-effect, strips the param, returns:

- `true` if a recognised action was handled — caller skips
  game-bootstrap import.
- `false` if no action / unrecognised action — caller proceeds
  with normal boot (game-bootstrap guard re-evaluates other
  query params like `?game=<tableId>`).

This is the **only** function the boot sequence in
`src/index.ts` should call. The other two exports are utility
hooks for future tests / future shortcuts.

### `?action=replay` (W12) — co-parameter contract

`replay` is the first action keyword that depends on a
co-parameter on the same URL. The wire shape is:

```
/?action=replay&replayId=<guid>
```

`replayId` MUST appear alongside `action=replay`; the router
reads `replayId` from the same `URLSearchParams` instance after
identifying the action. Missing / empty / whitespace-only
`replayId` is treated as a fetch failure (same "Replay not
found" toast as a 404), to keep the user-visible error path
uniform across configuration mistakes vs. real misses.

Dispatch sequence (W12):

1. `parseActionFromUrl()` returns `"replay"`.
2. `handlePwaActionFromUrl()` switches to the `replay` case.
3. `dispatchReplay()`:
   a. Reads `replayId` from the URL.
   b. Strips BOTH `action` and `replayId` from the URL via
      `history.replaceState` so a mid-flight refresh doesn't
      re-fire the shortcut.
   c. `fetch("/api/replays/{replayId}")` (Bishop W12 endpoint).
   d. On success: rewrites the URL to `/replay/{replayId}`
      and hands the JSON payload to
      `replay-launcher.ts:openReplayPayload()`. The launcher
      module is lazy-imported on this code path so the
      `?action=replay` cold launch doesn't pay the replay-
      viewer bundle cost up-front.
   e. On 404 / 5xx / network error / JSON parse failure: shows
      the toast "Replay not found" via the W3 shared toast
      helper.

Why no fallback to `/api/games/{gameId}/replay`?
`?action=replay` exposes a NEW id space (Bishop W12 introduces
the `Replay.Id` primary key as a separate addressable id, not
the `GameId` foreign key the W7 endpoint uses). Falling back to
the game-id endpoint would silently hide configuration drift —
the W12 contract is "the URL gives us a replay id, period." If
a future wave unifies the two id spaces, this fallback can be
revisited.

## §3 — Supported actions

| Keyword | Aliases | Side-effect | URL after dispatch |
|---------|---------|-------------|--------------------|
| `new-game` | — | Click `[data-action="new-game"]` (the lobby's "New game" button) once DOM is ready. | `/` (param stripped, no path change) |
| `spectate` | — | Activate `#lobby-public-games-tab`. | `/spectate` (path rewritten, no reload) |
| `tournament` | `tournaments` | Activate `#lobby-tournaments-tab`. | `/tournament/list` (path rewritten, no reload) |
| `replay` (W12) | — | Reads `replayId=<guid>` co-param, fetches `/api/replays/{replayId}` (Bishop W12), feeds the payload to the replay viewer. On error → "Replay not found" toast. | `/replay/<replayId>` (path rewritten on success) / `/` (path bare on failure) |
| `spectate` + `gameId` (W13) | — | When `?action=spectate&gameId=<guid>` is present, POSTs `/api/spectator/handoff` (Bishop W12) with `{gameId}` (cookie-auth) → on 200 mints a spectator JWT and bootstraps the spectator livestream for the requested table. On 401 → redirect to `/` so `installAuthUi()` can mount the sign-in modal. On 404/error → "Game not found" toast. | `/spectate/<gameId>?token=<jwt>#/spectate/<gameId>` (path rewritten with hash so the W6 `installSpectatorRoute()` hashchange listener fires) / `/` (path bare on auth failure) |

### §3.1 — `?action=spectate&gameId=<guid>` flow (W13)

The W13 wave extends the W11 `spectate` keyword with a
single-shot deep link that bypasses the public-games lobby
and drops the viewer straight into a named table.

Wire sequence:

1. Boot-sequence calls `handlePwaActionFromUrl()` which calls
   `parseActionFromUrl()`.
2. The parser detects `?action=spectate&gameId=<guid>` and
   returns `{ action: "spectate", gameId }`.
3. `dispatchSpectate()` branches on `gameId` — if present it
   delegates to `dispatchSpectateWithGameId(gameId)`; if
   absent, falls through to the W11 lobby-tab behaviour.
4. `dispatchSpectateWithGameId()` calls
   `fetchHandoffAndOpenSpectator(gameId)`:
   - **POST** `/api/spectator/handoff` body
     `{ "gameId": "<guid>" }`, credentials-included so the
     existing session cookie authenticates the call.
   - **200** → `{ token, expiresAt, scope: "spectator:<gameId>", ttlSeconds }`.
     The router rewrites the URL to
     `/spectate/<gameId>?token=<jwt>#/spectate/<gameId>` via
     `history.replaceState` (hash component triggers the W6
     `installSpectatorRoute()` hashchange listener) AND
     directly invokes `openSpectatorLivestream({ tableId: gameId })`
     because `replaceState` with a combined path+hash does
     not emit a `hashchange` event.
   - **401** → `redirectToLobbyForSignIn()` rewrites the URL
     to `/` and reloads so `installAuthUi()` (auth.ts) can
     mount the sign-in modal at boot. We do NOT preserve the
     original `?action=spectate&gameId=*` for post-login
     resumption (the JWT contract is short-lived; users
     should re-click the link after authenticating).
   - **404 / 500 / network** → `showGameNotFoundToast()`
     fires a transient "Game not found" toast and rewrites
     the URL to `/spectate` so the user lands at the
     public-games lobby instead of staring at a broken deep
     link.
5. The URL param is stripped via `clearActionParam()` after
   dispatch so refresh doesn't re-fire the handoff.

Failure surface (and where it lives in the codebase):

| HTTP status | Client behaviour                       | Code path                             |
|-------------|----------------------------------------|---------------------------------------|
| 200         | Bootstrap livestream + rewrite URL.    | `fetchHandoffAndOpenSpectator()`      |
| 401         | Redirect to `/` for sign-in.           | `redirectToLobbyForSignIn()`          |
| 404         | "Game not found" toast.                 | `showGameNotFoundToast()`             |
| 5xx / fetch | Same toast as 404 (intentionally lumped — the user has the same recovery in both cases). | `showGameNotFoundToast()`             |

Contract owner: **Bishop** (server-side
`SpectatorHandoffController`, W12). The W13 client matches
the W12 wire shape exactly; any future change to the JWT
scope shape or `ttlSeconds` semantics needs a Bishop+Hicks
co-bump.


Any keyword not in this table returns `null` from
`parseActionFromUrl()` and is treated as no-action by
`handlePwaActionFromUrl()`. The caller falls through to the
W2 game-bootstrap guard, which itself ignores `?action=*` (it
only fires on `?game=*`).

### Why path rewrites?

For `spectate` and `tournament` we rewrite the URL via
`history.replaceState` to the canonical path (`/spectate`,
`/tournament/list`) so the address bar reflects the user's
actual location post-dispatch. This matters for three reasons:

1. **Share-link symmetry** — copying the URL from a PWA shortcut
   should yield a re-shareable canonical link, not the
   `?action=*` form (which is intentionally hidden from users).
2. **Browser history** — back/forward navigation lands at the
   canonical path, not the shortcut form.
3. **Analytics** — the URL the analytics layer reads after
   dispatch matches what a non-shortcut visitor sees on the
   same screen.

For `new-game` there's no canonical path (it's an in-modal
action on the lobby), so we just strip the query param and
leave the path at `/`.

## §4 — Producer-side contract (manifest)

The `manifest.webmanifest` `shortcuts[]` array is the producer.
Wave 11 schema:

```json
"shortcuts": [
  {
    "name": "New Game",
    "short_name": "New",
    "description": "Start a new mahjong game",
    "url": "/?action=new-game",
    "icons": [{ "src": "img/icon-192.auto.png", "sizes": "192x192" }]
  },
  {
    "name": "Spectate Live",
    "short_name": "Spectate",
    "description": "Watch ongoing games",
    "url": "/?action=spectate",
    "icons": [{ "src": "img/icon-192.auto.png", "sizes": "192x192" }]
  },
  {
    "name": "Tournament Lobby",
    "short_name": "Tournament",
    "description": "Browse and join tournaments",
    "url": "/?action=tournament",
    "icons": [{ "src": "img/icon-192.auto.png", "sizes": "192x192" }]
  }
]
```

**Schema rule:** every `url` value with `?action=*` MUST use a
keyword in the §3 table. Adding a new shortcut entry is a
two-PR contract:

1. Wire the new keyword in `action-router.ts` (add to
   `SUPPORTED_ACTIONS`, add `dispatchX()` helper, add case
   to the switch). Update §3 in this doc.
2. Add the manifest `shortcuts[]` entry with the new `?action=`
   URL.

A keyword without a router case will fail silently (parse
returns `null`, boot proceeds without dispatch). A manifest
entry referencing an unsupported keyword will install but
behave as if the user opened the bare lobby — broken UX but
not a crash.

## §5 — Boot-sequence ordering

The router must run **before** the W2 game-bootstrap import
guard in `src/index.ts`. The current ordering:

```ts
// 1. Boot the lobby module (synchronous import, eager).
import './lobby-app';
// 2. Intercept ?action=* shortcuts.
import { handlePwaActionFromUrl } from './action-router';
const handledByAction = handlePwaActionFromUrl();
// 3. Game-bootstrap guard (only fires when no action handled).
if (!handledByAction && window.location.search !== '') {
  // dynamic import of heavy renderer chunk — only when ?game=<id>
  import('./game-bootstrap').then((m) => m.bootGame());
}
```

The lobby import is eager so the action-router's
`document.querySelector` calls in `dispatchSpectate()` /
`dispatchTournament()` find their target tabs even on cold
launch (those buttons are rendered by `lobby-app.ts`'s
synchronous DOM construction).

`new-game` is more permissive — the button is in
`index.html` itself (the lobby module decorates it with
event handlers), so the click is functional regardless of
lobby-module readiness.

## §6 — Selector contract

`action-router.ts` reads three DOM hooks. Selector ownership
is documented in `tests/selectors.md` (Vasquez's selector
contract); restating here for routing-context completeness:

| Selector | Owned by | Used by router for |
|----------|----------|-------------------|
| `[data-action="new-game"]` | Hicks (frontend, W11) | `new-game` click target |
| `#lobby-public-games-tab` | Hicks (frontend, W8) | `spectate` tab activation |
| `#lobby-tournaments-tab` | Hicks (frontend, W8) | `tournament` tab activation |

If any of these IDs / data-attributes are renamed, the router
breaks and the spec suite (Vasquez) hard-fails. The
`data-action` attribute style is preferred for new hooks (more
test-friendly + less likely to collide with CSS / behavioural
selectors); the two `id="lobby-*-tab"` hooks predate W11 and
are kept for selector stability.

## §7 — Future-keyword reservation list

Reserved for future waves — do NOT use these as keywords for
anything other than the documented intent:

| Reserved keyword | Planned use |
|------------------|-------------|
| `settings` | Open the user-preferences modal post-bootstrap. |
| `help` | Open the keyboard-shortcuts help overlay. |

Adding any of these requires the §4 two-PR contract.

> W12 cashed in the `replay` reservation — see §3 + §2 for the
> co-parameter contract (`?action=replay&replayId=<guid>` →
> `/api/replays/{replayId}` → `/replay/{replayId}`).

## §8 — Failure modes

| Scenario | Behaviour |
|----------|-----------|
| `?action=` with no value | Treated as `null` (no action). |
| `?action=NEW-GAME` (case mismatch) | Lowercased to `new-game`, handled normally. |
| `?action=tournament&game=<id>` | Tournament dispatched first, then the game-bootstrap guard sees the surviving `?game=<id>` (the router only strips `action`, not other params) and bootstraps the game. Outcome: tournament tab activated, then game loaded — last-in-wins on visible content. Avoid combining `action` with `game` in producer URLs. |
| Router runs before DOM ready | `dispatchNewGame()` / `dispatchSpectate()` / `dispatchTournament()` install a `DOMContentLoaded` listener and retry the click/activate. |
| Lobby tab not yet rendered (race vs. async i18n boot) | Single microtask retry inside the `DOMContentLoaded` handler. If the tab still isn't there after that, the dispatch silently no-ops (better than throwing). Vasquez's spec asserts this via a `waitFor` on the post-dispatch URL + active-tab class. |
| Browser without `URLSearchParams` | The whole router code is feature-detected at module-load; absent `URLSearchParams` it skips dispatch and the user lands on the bare lobby — acceptable degradation. |

## §9 — Wave hand-off

W11 wires the three documented actions. W12 cashed in the
`?action=replay` reservation (see §3) against Bishop's new
id-addressable replay endpoint. Remaining W12 candidates:

- **Multi-param shortcuts** — e.g. `?action=new-game&seats=4`.
  Requires action-specific param schemas; today the router
  only reads the keyword plus a single ad-hoc co-param
  (`replayId` for `replay`). A more general schema layer (e.g.
  per-action `parseCoParams<T>()`) would generalise the
  W12 co-param trick without forcing each new action to
  reimplement the URL parse / strip / refetch sequence.
- **Server-confirmed deep links** — for shortcuts that mutate
  server state (e.g. a hypothetical `?action=join&table=<id>`),
  we'd need a server round-trip after dispatch. Not yet needed.
- **Visual-regression coverage** — Vasquez's W11 spec exercises
  the URL probes; a future Playwright visual-regression sweep
  could capture the post-dispatch tab visuals.
- **Replay-launcher payload-shape contract test** — W12 reuses
  `normalizeServerReplay()` for both the W7 game-id endpoint
  AND the W12 replay-id endpoint on the assumption Bishop's W12
  emits the same wire shape. If those shapes ever diverge the
  toast will fire silently (we only show "Replay not found");
  add a Playwright spec that mocks the W12 endpoint with a
  malformed body to guard against drift.
