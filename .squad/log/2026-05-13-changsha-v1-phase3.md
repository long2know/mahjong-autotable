# Changsha v1 Phase 3 — Playable End-to-End

**Date:** 2026-05-13
**Requested by:** Stephen Long
**PR:** #25 (squash-merged to `main` SHA `a03feda`)
**Agents spawned:** Vasquez, Bishop, Hicks, Hudson (4-stream parallel)

## Outcome

✅ **COMPLETE** — Changsha v1 playable end-to-end. Phase 3 wave landed all four streams:

1. **Vasquez** (Rules) — Banker rotation v1.2 canonical locked; spec updated.
2. **Bishop** (Backend) — Five surgical fixes; 203 passing tests.
3. **Hicks** (Frontend) — Lobby + claim UX shipped; 48/48 vitest tests green.
4. **Hudson** (QA) — Vitest infra + 47 frontend tests landed.

## Key outputs

- **decisions.md** merged with 4 inbox files (Vasquez, Bishop, Hicks, Hudson phase 3 stream results).
- **Spec lock:** `docs/rules/changsha-spec.md` v1.2 (banker rotation + base unit clarifications).
- **Backend fixes:** Banker rotation, Kong/Pung priority, per-hand wall seed, chow tileIds, missed-win enforcement.
- **Frontend:** Lobby (createGame → fillWithBots → takeSeat → startGame), claim UX (chow picker, kong buttons, zimo), reconnect, SignalR bridge fixes.
- **Test infra:** Vitest + jsdom + React Testing Library; 47 tests covering reducer, bridge, signalrClient, mock hook.

## Remaining gaps (v3.1+)

- 3D mesh rendering in autotable iframe.
- `useLiveChangshaGame` refactor for testability.
- Component tests (modal, hand panel, badge).
- 16-hand E2E test (hub-layer regression guard).
- Persistence hydration on process restart.

---

**All 4 streams merged to main in PR #25 (SHA a03feda).**
