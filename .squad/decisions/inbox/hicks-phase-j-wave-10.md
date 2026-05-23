# Hicks — Phase J Wave 10 (final frontend polish)

**Branch:** `stlong/phase-j-wave-10-completion`
**Bundle hashes (Parcel build at completion):**
- JS:  `autotable-src.73dffdb4.js` (1.28 MB)
- CSS: `autotable-src.4a92b1f1.css` + `autotable-src.6633d8fb.css`
- About-CSS: `about.df85b4c4.css`
- ESM: `esm.eb93de05.js` (395 KB)

Stale bundle artifacts deleted (Wave 9 + intermediate Wave-10 hashes):
`autotable-src.6e0d2167.js`, `autotable-src.95ecc0f0.css`,
`autotable-src.df85b4c4.css`, `autotable-src.83193e10.js`.

## Scope shipped

Wave 10 wraps the Phase J polish run. All five deliverables in the
Wave-10 directive landed. Where Bishop's matching backend endpoint
isn't merged yet (tournaments, bot reasoning), the frontend
feature-detects and degrades to a placeholder so the wave can ship
ahead of (or in lockstep with) the backend.

### 1. CSP `style-src` tightening — bundle now CSP-clean

Every inline `style="..."` attribute in `src/frontend/autotable-src/index.html`
was migrated to a CSS class or to the HTML5 `hidden` attribute.
The shipped bundle no longer relies on inline style strings, so the
Wave-10 `Security:CspStrictStyles` opt-in knob (Apone's design) is
safe to flip in production.

**The default CSP still ships `'unsafe-inline'` by design** — per
Vasquez's `CspStyleSrcNoUnsafeInlineTests.DefaultCspConstant_StylesSection_KeepsUnsafeInlineUntilOptIn`
contract, the knob is the only path that drops it. Once the canary's
`/api/csp-report` sink reports zero `style-src` violations from the
new bundle, ops can flip `Security:CspStrictStyles=true` in
`appsettings.Production.json` and the policy hardens automatically.

**Middleware changes** (mine; `Observability/SecurityHeadersMiddleware.cs`):
- New `CspStrictStylesConfigKey = "Security:CspStrictStyles"` constant.
- New `_cspStrictStyles` field + ctor wire-up.
- New `DropStyleUnsafeInline(string csp)` internal static helper
  (referenced by Apone's tests).
- `_cspTemplate` ctor branch now wraps `_cspStrict ? StrictCsp : DefaultCsp`
  with `_cspStrictStyles ? DropStyleUnsafeInline(...) : ...`.
- Wave-10 docstring on `DefaultCsp` cross-references the contract.

**CSS class equivalents added (`style.css` Wave-10 block):**
- `.claim-countdown` — replaces inline width/colour on the claim timer.
- `.dropdown-menu-help` — replaces inline min-width/whitespace.
- `.modal-source-cite` — replaces inline italic + small text.

**Helper layer added (`utils.ts`):**
- `setElHidden(el, hidden)` flips the HTML5 `hidden` attribute AND
  clears any leftover inline `display` so Bootstrap's
  `[hidden] { display: none !important; }` doesn't fight us.
- `showEl(el)` / `hideEl(el)` are sugar.
- ~80 call sites migrated across `game-ui`, `chat`, `client-ui`,
  `audit`, `identity`, `leaderboard`, `lobby`, `profile`,
  `profile-page`, `settings-drawer`.

CSSOM property mutations (`el.style.X = Y`) are NOT subject to CSP
enforcement per the CSP3 spec, so the runtime show/hide / animation
code paths continue to work even after the knob flips.

### 2. Forced avatar migration modal (`identity.ts`)

Legacy `#808080` sentinel avatars are migrated via a blocking modal
that picks from `AVATAR_COLOR_PRESETS` (8 hex options from
`profile.ts`). Modal markup at `index.html` `#migrate-avatar-modal`
after the profile drawer.

**Behaviour:**
- `installAvatarMigrationModalIfNeeded()` (called from `index.ts`)
  subscribes to `onProfile()` so late-arriving profile loads
  re-evaluate. If `profile.avatarColor === '#808080'` (case-insensitive),
  the modal is shown and blocks UI until the user picks a colour.
- `setAvatarColor(hex)` is invoked; on success the modal hides.
- Idempotent — multiple installs / multiple profile updates won't
  stack modals.

**`data-testid` surface (per Vasquez's contract in `tests/selectors.md`):**
- `avatar-migration-modal` (root)
- `avatar-migration-pick-{name}` (swatch buttons; names = red, orange,
  yellow, emerald, teal, blue, purple, slate — index-aligned with
  `AVATAR_COLOR_PRESETS` in `profile.ts`)
- `avatar-migration-dismiss` ("Not now" button — soft-defer; the modal
  re-shows on the next profile load while `avatarColor` stays at
  `#808080`)
- `avatar-migration-confirm` (Confirm button)

### 3. Tournaments tab (`tournaments.ts`, graceful degrade)

New module + new tab pane after the leaderboard tab in the lobby.
**Bishop's `/api/tournaments` IS merged in HEAD (61a706f), so the
tab will render the live list+detail+register flow on production.**
On any 404 the placeholder ("Coming soon") shows.

**Entry point:** `installTournamentsPanel()` — called from `index.ts`
unconditionally; re-probes on each tab activation so the placeholder
self-heals once the backend deploys.

**Bishop endpoints consumed:**
- `GET /api/tournaments` — list
- `GET /api/tournaments/{id}` — detail (bracket + standings)
- `POST /api/tournaments` — create (admin)
- `POST /api/tournaments/{id}/register` — join
- `POST /api/tournaments/{id}/unregister` — leave
- `POST /api/tournaments/{id}/start` — admin

**`data-testid` surface (per Vasquez's pinned contract in `tests/selectors.md`):**
- `lobby-tournament-card` (root pane / Wave-10 panel container)
- `lobby-tournament-list` (rendered list)
- `lobby-tournament-name` (create-form name input)
- `lobby-tournament-create` (create-form submit button)
- `tournament-register-btn` (per-card button when status='open' and not registered)
- `tournament-registration-status` (per-card "Registered" / "Open" / status badge)
- `tournament-start-btn` (per-card button when status='open' + viewerCanStart)
- `tournament-matches-table` (detail bracket pane)
- `tournament-leaderboard` (detail standings table)
- `tournaments-placeholder` (degrade state)
- plus retained: `tournament-row-{i}`, `tournament-detail`, `tournament-unregister-btn`, `tournament-create-form`, `tournament-create-format`, `tournament-create-max-players`

### 4. Spectator chat polish (`chat.ts`)

Two surface improvements over Wave 9's chat panel:

**A. Distinct spectator accent** — Messages on the `spectators` and
`spectator-private` channels render with a 👁/🔒 prefix and a cyan
left-border accent (`.chat-msg-channel-spectators` /
`.chat-msg-channel-spectator-private`), making them visually
separable from regular table chat without forcing the user to read
the channel pill.

**B. Spectator-private subchannel** — A new UI-only `spectator-private`
channel lets spectators DM other spectators without polluting the
main `private` queue. Wire-channel remains `'private'` (so Bishop's
backend doesn't need changes), but the UI keeps the two queues
separate via the `m.channel` value preserved on each `ChatMessage`.

**Helpers added:**
- `needsRecipient(ch)` — true for `'private'` and `'spectator-private'`.
- `wireChannel(ch)` — maps UI channel → wire channel.
- `visibleMessages(state)` — filters using `wireChannel(state.channel) === wireChannel(m.channel)`.

**Wave-10 spectator contract (per Vasquez's `spectator-chat.spec.ts`):**
- When `isSpectator()` is true (URL `?seat=-1`), `installChatPanel`
  seeds `state.channel = 'spectators'` BEFORE rendering the channel
  picker, so the spectator default is `Spectators`, not `Table`.
- The composer is NOT disabled for spectators (the brief explicitly
  expects it enabled).
- `visibleMessages()` filters by `wireChannel(...)`, so a spectator
  viewing the `spectators` channel cannot see `table` messages
  leaking in.

**Visibility:** `spectator-private` only appears in the channel
picker when `isSpectator()` is true (URL `?seat=-1`).

**Per-message channel attrs** for CSS targeting:
- Root pane: `data-channel="{channel}"`
- Per message: `data-channel="{channel}"` + `.chat-msg-channel-{channel}` class.

**`data-testid` extensions:**
- `chat-channel-{table|spectators|spectator-private|private}` — option testids
- existing `chat-message-{id}` unchanged.

### 5. Bot decision "Why?" reasoning expand (`audit.ts`)

Each bot row in the replay audit tab gains a `Why?` toggle that
reveals/hides a `reasoning` sub-row. Reasoning items are colour-coded
by classification prefix:
- `[win]:` → green (`.audit-reason-win`) — favourable outcome
- `[caution]:` → amber (`.audit-reason-caution`) — risk flagged
- `[suboptimal]:` → red-orange (`.audit-reason-suboptimal`)
- (no prefix) → neutral default

When the `AuditRow.reasoning` array is `null` / empty (i.e. the
backend doesn't yet emit the field, OR the bot strategy returned no
explanation), the placeholder "Reasoning unavailable" renders
instead of an empty pane.

**`data-testid` surface (per Vasquez's contract in `tests/selectors.md`, per bot row `i`):**
- `replay-audit-row-{i}-why` — toggle button
- `replay-audit-row-{i}-reasoning` — collapsible sub-row
- `replay-audit-row-{i}-reasoning-list` — `<ul>`
- `replay-audit-row-{i}-reasoning-line-{j}` — `<li>` per reason
- `replay-audit-row-{i}-reasoning-unavailable` — fallback span
- `[data-strategy]` attribute on the row container, value = `botTier`
  (only set for bot rows; absent for human / system rows).

**Bishop's BotDecision now carries a `reasoning` array** (Wave-10
commit 61a706f), so the frontend will render the colour-coded
explanations once the audit endpoint surfaces them.

## i18n additions (lockstep across all 3 catalogs)

| Key                                  | en              | zh-Hans     | zh-Hant     |
|--------------------------------------|-----------------|-------------|-------------|
| `chat.channel.spectator_private`     | "Spectator DM"  | "观众私聊"  | "觀眾私訊"  |
| `replay.audit.why`                   | "Why?"          | "为什么？"  | "為什麼？"  |
| `replay.audit.reasoning_unavailable` | "Reasoning unavailable." | "暂无推理过程。" | "暫無推理過程。" |

Tournament UI copy uses hard-coded English at the moment (acceptable
for the placeholder/coming-soon state; a follow-up wave can move it
to the catalog once the UI is stable).

## TypeScript baseline

`tsc --noEmit --skipLibCheck` reports only the pre-existing Wave-8
baseline error:

```
src/sentry.ts(97,24): error TS1323: Dynamic imports …
```

No new TypeScript errors from Wave-10.

## Files committed (in this branch by Hicks)

**Backend (CSP middleware mechanism only — Hicks owns from Wave 9):**
- `src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs`

**Frontend source:**
- `src/frontend/autotable-src/index.html`
- `src/frontend/autotable-src/src/audit.ts`
- `src/frontend/autotable-src/src/chat.ts`
- `src/frontend/autotable-src/src/client-ui.ts`
- `src/frontend/autotable-src/src/game-ui.ts`
- `src/frontend/autotable-src/src/identity.ts`
- `src/frontend/autotable-src/src/index.ts`
- `src/frontend/autotable-src/src/leaderboard.ts`
- `src/frontend/autotable-src/src/lobby.ts`
- `src/frontend/autotable-src/src/profile-page.ts`
- `src/frontend/autotable-src/src/profile.ts`
- `src/frontend/autotable-src/src/settings-drawer.ts`
- `src/frontend/autotable-src/src/style.css`
- `src/frontend/autotable-src/src/tournaments.ts` (NEW)
- `src/frontend/autotable-src/src/utils.ts`
- `src/frontend/autotable-src/src/i18n/en.json`
- `src/frontend/autotable-src/src/i18n/zh-Hans.json`
- `src/frontend/autotable-src/src/i18n/zh-Hant.json`

**Frontend bundle (Parcel output):**
- `src/frontend/autotable/index.html`
- `src/frontend/autotable/autotable-src.83193e10.js`
- `src/frontend/autotable/autotable-src.4a92b1f1.css`

**Squad artefacts:**
- `.squad/agents/hicks/history.md` (this wave appended)
- `.squad/decisions/inbox/hicks-phase-j-wave-10.md` (this memo)

## Cross-cutting notes

- **Vasquez's `CspStyleSrcNoUnsafeInlineTests.cs`** (untracked in my
  working tree from prior coordination) references
  `CspStrictStylesConfigKey` and the `DropStyleUnsafeInline` helper —
  both shipped in this wave's middleware. Once Vasquez commits the
  test file the contract is fully pinned.
- **Apone's Wave-10 additions to `CspHeaderTests.cs`** (also
  untracked in my tree) likewise depend on the helper I shipped.
- **Bishop's Wave-10 backend** (commit 61a706f, already on this
  branch) ships `/api/tournaments` + the `BotDecision.reasoning`
  field — both wired up on the frontend in this wave.

## Blockers / open items

None blocking the wave landing. The strict-styles knob defaults OFF,
so flipping it is an operational decision (recommend Vasquez green-
light after the canary's CSP-report sink shows zero `style-src`
violations from this bundle).
