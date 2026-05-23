# Scribe — Phase K Wave 12 sweep memo

**Author:** Scribe (Archive) `<scribe@squad.mahjong>`
**Branch:** `stlong/phase-k-wave-12-bringup` (cut from `main` @ `ee9dba0` / Wave 11 squash-merge PR #57)
**Date:** 2026-10-XX

## What this sweep delivered

1. **Folded the 4-file Phase K Wave 12 inbox into canonical `.squad/decisions.md`** as a single `## Phase K — Wave 12 (...)` section appended after the Wave 11 entry. **942 lines added** (file 11,389 → 12,331 lines). Section closes with `### Phase K Wave 12 — DONE.` + `---` separator.

2. **Created `docs/wave-summaries/phase-k-wave-12.md`** — PR-body-length wave summary covering all four lanes + the W13 forward queue. **1,266 lines** (mirrors `docs/wave-summaries/phase-k-wave-11.md` at 1,054 lines + W12-specific extensions for the DutchSwissPairingService retirement / `?action=replay&replayId=*` deep-link routing / lane-map `bracket_shared` extension / 27-wave zero-skip streak).

3. **Appended W12 entry to `.squad/agents/scribe/history.md`** — 98 lines added (file 866 → 964 lines). Mirrors W11 entry structure exactly (heading + opener + Timestamp/Branch/Contribution + commit-roll-up table + 7 Headlines + 14 Notable observations + top-5 inbox items + W13 hand-offs by lane + Stephen action items + Open questions + sign-off).

4. **Created this memo** (`.squad/decisions/inbox/scribe-phase-k-wave-12-sweep.md`) — force-added since `.squad/decisions/inbox/` is in `.gitignore`.

## Source material consumed

| Source                                                             | Lines | Lane    |
|--------------------------------------------------------------------|------:|---------|
| `.squad/decisions/inbox/hicks-phase-k-wave-12.md`                  |   177 | Hicks   |
| `.squad/decisions/inbox/bishop-phase-k-wave-12.md`                 |   350 | Bishop  |
| `.squad/decisions/inbox/apone-phase-k-wave-12.md`                  |   305 | Apone   |
| `Phase_K_W12/Vasquez/vasquez-phase-k-wave-12.md`                   |   237 | Vasquez |
| Commit messages (`ec69dd5` / `35e6018` / `a3a8788` / `e22ef5c`)    |    ~  | All     |

## W12 commit roll-up (all correctly authored at the `%an <%ae>` level)

| Lane    | SHA       | Author                                          |
|---------|-----------|-------------------------------------------------|
| Hicks   | `ec69dd5` | `Hicks (Frontend) <hicks@squad.mahjong>`        |
| Vasquez | `35e6018` | `Vasquez (QA) <vasquez@squad.mahjong>`          |
| Bishop  | `a3a8788` | `Bishop (Backend) <bishop@squad.mahjong>`       |
| Apone   | `e22ef5c` | `Apone (DevOps) <apone@squad.mahjong>`          |

## Numeric facts pinned this wave

- **Test gate:** 2610 / 0 / 0 (+207 vs W11 baseline 2403; +73 over Vasquez baseline 2537 after Vasquez's surface seeding +134). Phase K trajectory `W6 1422 → W7 1506 → W8 1706 → W9 1880 → W10 2108 → W11 2403 → W12 2610` (+1,188 over 7 waves; 83.5 % growth).

- **Bundle:** three-renderer big chunk `466.40 KB → 448.65 KB` (−17.75 KB / −3.8 %). **W12 <450 KB stretch ceiling BEAT by ~1.4 KB.** Trajectory `740 → 579 → 532 → 507 → 497 → 466 → 448 KB` across W6 → W12 (**7-wave monotonic-decrease; cumulative −39.4 %**).

- **Zero-skip streak:** **27 consecutive waves** (J.1 → J.10 + K.1 → K.12).

- **Identity hardening:** **7th consecutive clean wave** — per-invocation `git -c user.name=X -c user.email=Y commit ...` held across all 4 W12 rollup commits; zero identity drift + zero coordinator fix-up commits.

- **`.work/squad-git-lock` mutex:** **3rd consecutive fully-adopted wave** (W10 cutover; W11 first held; W12 holds clean).

- **Lane-discipline:** strict-mode `checked=5 violations=0` — **SECOND CONSECUTIVE 0-VIOLATION WAVE** (W11 was first). New lane-map entry `bracket_shared` (Bishop+Hicks 2-author for `tournament-bracket-*.{ts,cs}`) closes the W11→W12 cross-lane hand-off.

## Observations worth flagging to coordinator

- **DutchSwissPairingService retirement closes a W11→W12 hand-off cleanly.** Source files `SwissDualEngineParityFacts.cs` + `DutchSwissPairingService.cs` DELETED in `a3a8788`. FIDE C.04 backtracking is sole owner of Swiss pairing; W10 single-swap + float-down + `__bye__` insertion logic moves into `FideC04BacktrackingPairingService` as a fallback ONLY when the backtracking solver exceeds `floatAttempts < b.Count` cap. Documented at `docs/tournament-architecture.md §4.3` (NEW W12).

- **`?action=replay&replayId=<id>` PWA deep-link routing wired against Bishop's new `/api/replays/{replayId}` endpoint.** Critical invariant: **NO fallback to legacy `/api/games/{gameId}/replay`** — the schemas differ (canonical W12 endpoint returns `{ replayId, gameId, recordedAt, schemaVersion: 2, ... }` vs legacy `{ moves, players }`); silent rollback would corrupt the replay viewer. Hicks's `replay-loader.ts` (NEW W12) is sole owner.

- **`TileReference` reserved-byte allocation pinned this wave.** Byte 1 bit 0 = `HasRedFive`; bit 1 = `IsAkaDora`; bit 2 = `IsTsumogiri` (self-drawn discard marker — needed for commentary streaming); bits 3-7 reserved (5 unused bits for future expansion). Byte 2 is reserved zero-padding. Byte 3 stays checksum (XOR of bytes 0-2). Documented at `docs/commentary-tile-codec.md §2` (NEW W12).

- **`CommentaryRecord` polymorphism via `[JsonDerivedType]`** — 3 subtypes ship W12 (`RiichiCall` / `Discard` / `Pung`); 4 W13 candidates (`Chii` / `Kan` / `Win` / `Draw`). STJ + Newtonsoft round-trip parity confirmed via 12-case fact spec. **`SystemTextJson` is the canonical serializer; Newtonsoft round-trip parity is a regression guard, not a primary path.**

- **Janus mountpoint admission control: `MaxConcurrentMountpoints=128` cap + ITU-T Y.1541 queue-by-priority** (`Premium` > `Standard` > `Spectator`). New metric `mountpoint_admission_rejected_total{reason="capacity"}` joins the W10 mountpoint-eviction taxonomy on the SLO dashboard. Documented at `docs/voice-sfu-design.md §4.6` (NEW W12).

- **Prod Redis `terraform apply` LANDED** (W11 was blocked on prod EKS cluster cutover; W12 unblocks). `cache.r6g.large` multi-AZ + CMK KMS + AUTH + TLS. Hudson + Apone re-ran W10 `k6` load-test against `cache.r6g.large` and confirmed p99 < 12 ms steady-state. **NetworkPolicy for argo-rollouts dashboard** closes the W11 in-cluster bypass gap.

- **Apone's `docs/agent-handoff-protocol.md §5.10` (NEW W12) canonicalises the per-invocation identity binding template**; `§6.1` (NEW W12) ships the `flock` cutover ledger documenting the `/tmp/` → `.work/` migration outcome across W10 → W11 → W12.

- **W13 forward queue consolidated to ~31 items** across Bishop (8) / Hicks (7) / Apone (7) / Vasquez (5) + cross-cutting lane-discipline + 5 Scribe/coordinator carry-forwards.

- **Stephen action items: branch-protection flip now EIGHTH consecutive wave hand-off** — Vasquez's W14 escalation fallback dispatches the `gh api -X PATCH /repos/long2know/mahjong-autotable/branches/main/protection` one-liner if no manual flip by then. **OpenAI API key now blocks `EfCommentaryStore` persistence dogfood in prod for 2 consecutive waves.**

## Files modified by this sweep

| Path                                                         | Op        | Before  | After   | Δ      |
|--------------------------------------------------------------|-----------|--------:|--------:|-------:|
| `.squad/decisions.md`                                        | append    | 11,389  | 12,331  | +942   |
| `docs/wave-summaries/phase-k-wave-12.md`                     | create    | —       |  1,266  | +1,266 |
| `.squad/agents/scribe/history.md`                            | append    |    866  |    964  | +98    |
| `.squad/decisions/inbox/scribe-phase-k-wave-12-sweep.md`     | create -f | —       | (this)  | (this) |

## Identity (per-invocation, race-safe)

This sweep's commit will be authored as `Scribe (Archive) <scribe@squad.mahjong>` via the W6+ per-invocation pattern:

```bash
git -c user.name="Scribe (Archive)" -c user.email="scribe@squad.mahjong" \
    commit -m "Phase K Wave 12 — Scribe sweep: decisions ledger fold + wave summary

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

NO `git config user.name` / `git config user.email` invocations — only per-invocation `-c` flags. The `flock 9>.work/squad-git-lock` mutex serializes the commit + push against any concurrent agent run.

— Scribe (Archive), Phase K Wave 12 sweep close
