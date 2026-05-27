# Playable-gate tests

Concrete visual/behavioural Playwright specs that must pass before the
team or anyone else may claim a Changsha variant feature is "playable".

These gates exist because passing dotnet test, passing vitest, green CI,
small bundle sizes, and even green Playwright runs that *count* things
without inspecting the visual contract are demonstrably insufficient.
Stephen called this out on 2026-05-27 after a face-down-wall regression
shipped to main while every other signal was green:

> "no reason for you to keep churning and have no output or be confused
> about what works."

## Active playable-gates

### Changsha dealing ceremony

**Spec:** `playtest-artifacts/playtest-changsha-dealing-ceremony.spec.mjs`
**Owner:** Vasquez (rules/QA), with Bishop+Hicks+Frost as implementation
contacts for failure triage.
**Memo:** `.squad/decisions/inbox/vasquez-changsha-dealing-ceremony-gate.md`

Gates (must all PASS for the claim):

| ID | Gate | Why |
| --- | --- | --- |
| GATE-1 | Wall count = 4 | Mahjong has a square wall layout, one per seat. |
| GATE-2 | Walls face-down | Per Stephen 2026-05-27: tiles must start face-down. Backs only on the wall. |
| GATE-3 | No hand tiles visible | Pre-deal phase. Hand slots empty. |
| GATE-4 | Dice not yet rolled | Pre-deal phase. Dice has no value. |
| GATE-5 | Pickup ceremony plays out | After `world.deal('HANDS')` the chain reaches 12/12/12/12 (post-3-rounds) **or** 14/13/13/13 (final). Anything else means the ceremony mis-fired. |
| GATE-6 | Zero page errors | No JS exceptions during the ceremony. |

Run locally:

```bash
# 1. Build + start the backend on 8088 (cwd MUST be the project dir):
cd src/backend/src/Mahjong.Autotable.Api
ASPNETCORE_URLS=http://0.0.0.0:8088 ASPNETCORE_ENVIRONMENT=Development \
  dotnet bin/Debug/net10.0/Mahjong.Autotable.Api.dll &
sleep 8

# 2. Run the gate spec:
cd ../../../..
E2E_BASE_URL=http://127.0.0.1:8088 \
  node playtest-artifacts/playtest-changsha-dealing-ceremony.spec.mjs
echo "exit=$?"
```

Exit code 0 = ship. Exit code 1 = the regression you just introduced
broke the dealing ceremony. Inspect
`playtest-artifacts/changsha-dealing/findings.json` for the structured
gate report and the screenshot trail under
`playtest-artifacts/changsha-dealing/`.

Environment knobs:

- `E2E_BASE_URL` — defaults to `http://127.0.0.1:8088`.
- `PLAYTEST_GAME_ID` — pin to a deterministic seed for repeatable runs.
- `PLAYTEST_POST_FIX=1` — suppresses the `baseline-before-fix.png` copy
  when running the spec post-fix to confirm a green gate.

## Promotion rules

1. **Any agent claiming "playable Changsha"** must include the gate
   spec's `findings.json` (showing all six gates green) and a fresh
   screenshot set in their decision memo.
2. **Any backend or frontend PR touching the dealing chain** (Autotable
   slot map, ChangshaToAutotableTranslator, AutotableWsEndpoint,
   world.ts, setup-deal.ts, client.ts pickup collection) must run the
   gate before merge. A red gate = red PR.
3. **The gate is the contract.** If a legitimate change makes the gate
   over-strict (e.g. a new ceremony phase between rounds), update the
   gate spec *and* the memo at
   `.squad/decisions/inbox/vasquez-changsha-dealing-ceremony-gate.md`
   in the same PR — never weaken the gate without an explicit memo.

## Adding new gates

Place a new `.spec.mjs` under `playtest-artifacts/`, add a row to the
table above, and add a memo at
`.squad/decisions/inbox/<owner>-<slug>-gate.md` capturing:

- The behaviour the gate enforces.
- The specific regression (with citation) it would have caught.
- The full pass/fail criteria + run commands.

Gate specs must exit non-zero on any failure so CI can wire them in
without bespoke pass/fail parsing.
