# Commentary `TileReference` wire contract

> **Status:** Authoritative as of Phase K Wave 10 (2025-Q4).
> **Owners:** Bishop (backend / commentary feed authority).
> Hicks (frontend / commentary-panel renderer authority).
> Vasquez (test) owns the mock-fixture rotation that exercises
> both the W9 string-shape carry-over and the W10 object shape.

This document pins the **canonical** shape of tile references
embedded in commentary-panel records and the
`mahjong:highlight-tile` DOM event. It supersedes the W9 ad-hoc
"bare tile-id string" convention (e.g. `"m5"`, `"p3-red"`).

## 1 — Canonical shape

```ts
interface TileReference {
  /** Stable tile identity. Format: "<suit>-<rank>[-<variant>]". */
  tileId: string;

  /** Suit category. One of: "man" | "pin" | "sou" | "honor" | "flower". */
  suit: string;

  /** 1-9 for numbered suits; 1-7 for honors; 1-8 for flowers. */
  rank: number;
}
```

### Field discipline

- **`tileId`** is the source of truth for *identity*. Two
  references with equal `tileId` MUST highlight the same tile in
  the 3D scene. Backend produces it; frontend never synthesises.
- **`suit` / `rank`** are **denormalised** view-helpers — they
  let the panel CSS hook
  `[data-tile-suit="man"][data-tile-rank="5"]` style chips
  without re-parsing `tileId`. If suit + rank disagree with
  `tileId`, the renderer trusts `tileId` and emits a
  `console.warn`. Bishop's serializer asserts consistency at
  encode time.
- **Variant suffix** (`-red`, `-aka`, `-flower`) appears in
  `tileId` only. Variants don't get a dedicated wire field; the
  renderer slices the suffix off when computing the asset URL.

### Suit vocabulary

| Wire value | UI label | Asset prefix | Notes                                    |
|------------|----------|--------------|------------------------------------------|
| `man`      | Man / Wan / Cracks | `m`   | Numbered 1-9.                            |
| `pin`      | Pin / Dots         | `p`   | Numbered 1-9. `-red` for 5p red-5.       |
| `sou`      | Sou / Bamboo       | `s`   | Numbered 1-9. `-red` for 5s red-5.       |
| `honor`    | Honor              | `z`   | Ranks 1-4 = winds (E/S/W/N); 5-7 = dragons. |
| `flower`   | Flower / Season    | `f`   | Changsha variant only. Ranks 1-8.        |

Renderer assets live under
`src/frontend/autotable-src/img/tiles/`; the asset filename is
derived by `{prefix}{rank}{variant}.png` where `prefix` comes
from the table above.

## 2 — Wire location

`TileReference[]` appears as the `tileReferences` field on each
commentary record returned by the live-commentary feed
(`WS /autotable/ws` → `commentary.tile.highlight` envelope) and
on each `CommentaryRecord` row fetched via the polling fallback
(`GET /api/commentary/{tableId}`).

```jsonc
{
  "kind": "commentary.tile.highlight",
  "record": {
    "id": "c-2025-11-01T00:00:00Z-001",
    "timestamp": "2025-11-01T00:00:00Z",
    "speaker": "auto",
    "body": "East discards 5m and 3p simultaneously …",
    "tileReferences": [
      { "tileId": "m5",       "suit": "man", "rank": 5 },
      { "tileId": "p3-red",   "suit": "pin", "rank": 3 }
    ]
  }
}
```

### W9 compatibility hatch (drops at W12)

Hicks's `pickTileReferences()` accepts both:

1. **Object shape** (the W10 canonical) — passes through after
   field-level validation.
2. **Bare string** (the W9 wire) — parsed via `parseTileIdShape()`
   into `{ tileId, suit, rank }`. The parser implements the
   suit-prefix grammar above; if the string doesn't parse, the
   reference is dropped and a single `console.warn` is emitted.

Bishop's W10 backend ships objects. The string fallback exists
**only** to bridge the rolling deploy window where one peer
emits W9-shaped records and another consumes them. Plan to
remove the string-coercion branch in W12 once two consecutive
backend deploys ship the object shape.

## 3 — `mahjong:highlight-tile` DOM event

When a commentary tile chip is clicked, the panel dispatches a
custom event on `document`:

```ts
interface HighlightTileDetail {
  tileId: string;
  source: 'commentary-panel'
        | 'live-board'
        | 'replay-timeline'
        | 'spectator-panel';
}

document.dispatchEvent(
  new CustomEvent<HighlightTileDetail>('mahjong:highlight-tile', {
    detail: { tileId, source: 'commentary-panel' },
  }),
);
```

### `source` discipline

- The 3D scene listener (`autotable-src/src/scene-effects.ts`
  → `wireHighlightHandlers`) reads `detail.source` to disambiguate
  which on-screen affordance triggered the highlight so it can
  pulse the corresponding ring-marker variant.
- Adding a new dispatcher? Append a literal to the union above
  AND register the source string in the renderer's
  `HIGHLIGHT_SOURCE_PALETTE` map (`scene-effects.ts`). Unknown
  sources fall through to the neutral pulse.
- **DO NOT** dispatch without `source` — pre-W10 dispatchers
  omitted it and the renderer fell through to a default ring;
  W10's audit removed the fallback in favour of a `console.warn`
  to surface the gap.

## 4 — Migration discipline (W9 → W10)

Ordered rollout:

1. **Bishop** ships W10 backend with a flag-gated dual emission
   (both shapes in `tileReferences` until the flag flips). The
   `__compat` flag defaults OFF on `main` so the wire is
   single-shape at any given time.
2. **Hicks** lands the object-shape consumer + string fallback
   (this wave — Phase K W10).
3. **Vasquez** rotates the mock fixtures
   (`tests/fixtures/commentary-*.json`) from string shape to
   object shape. The renderer must remain green throughout.
4. **Bishop** drops the `__compat` flag once steps 2 + 3 are
   merged to `main`. Hicks then schedules the W12 cleanup that
   removes `parseTileIdShape`.

If a step lands out-of-order, the renderer keeps working — the
fallback parser is the safety net. The discipline above keeps
the safety net unused (and therefore properly tested) for
exactly one wave.

## 5 — Renderer contract testing

`src/frontend/autotable-src/tests/commentary-panel.spec.ts` has
the canonical assertions:

- Object-shape records render with `data-tile-suit` /
  `data-tile-rank` attributes set from the wire values.
- String-shape records (W9 compat) render with the parsed
  attributes and a single `console.warn` is observed.
- Clicking a chip dispatches `mahjong:highlight-tile` exactly
  once, with `detail.source === 'commentary-panel'` and
  `detail.tileId` matching the chip's `data-tile-id`.

The Playwright spec is W11 work — the unit-test surface above is
the W10 entry point.
