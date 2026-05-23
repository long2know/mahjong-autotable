# Project Context

- **Owner:** {user name}
- **Project:** {project description}
- **Stack:** {languages, frameworks, tools}
- **Created:** {timestamp}

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase J Wave 7 (Hicks, 2026-05-XX) — replay viewer wiring + a11y + settings drawer + profile page

- Replay viewer rewired to `GET /api/games/{gameId}/replay` via new
  `replay-launcher.ts` with 404 fallback to in-memory playback.
- New app-wide tabbed settings drawer (`settings-drawer.ts`) persisting
  to `localStorage["mahjong.settings.v1"]`, mirrors sound + perspective
  toggles to existing Wave-2 keys.
- New player profile page (`profile-page.ts`) with stats grid + recent
  games (with replay links) + editable display name / avatar colour.
  Read-only mode opens for any leaderboard row via custom event.
- Replay footer gained prev/next-hand buttons, playback-speed dropdown
  (0.5×–4×), and an aria-live event counter.
- Accessibility sweep: `aria-*` coverage on every Wave-7 surface +
  Wave-6 touchups; new `tests/e2e/a11y.spec.ts` (axe-core, 5 specs)
  asserting zero serious/critical violations across lobby, leaderboard,
  settings drawer, profile page, replay viewer.
- Bundle hashes: `autotable-src.85bbb8ca.js` + `autotable-src.a7cd8ea4.css`
  (main).  Pruned stale `2391eb20.js` + `094cde3a.css`.
- Gates: TS strict ✅ · parcel ✅ · `playwright --list` ✅ (24 tests).
  Full e2e requires Apone's container; out-of-band locally.

