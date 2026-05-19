import { DealType, GameType } from "./types";

type DealRange = [string, 0 | 1 | 2 | 3, number];

export interface DealPart {
  roll?: number;
  tiles?: Array<number>;
  rotationIndex?: number;
  ranges: Array<DealRange>;
  absolute?: boolean;
}

// Changsha-only deal tables.
// Wall layout: 14/14/13/13 stacks per seat (28/28/26/26 tiles) = 108 tiles total.
// HANDS deal hands 13 tiles to each seat + 1 extra tile to the dealer (hand.extra@0).
// Dealer's 14th tile counts as their first draw per Vasquez §1.5 — drag-to-discard
// is the only client-driven input in Phase B.
//
// Note: Phase B does not implement dice-roll-conditional break-point placement.
// The remaining 55 wall tiles drop into seat wall positions in a fixed shape that
// approximates a post-deal Changsha wall. Phase C/D revisits this once the
// backend drives placement authoritatively.
export const DEALS: Record<GameType, Partial<Record<DealType, Array<DealPart>>>> = {
  CHANGSHA: {
    INITIAL: [
      {
        ranges: [
          ['wall.1.0', 0, 28],
          ['wall.1.0', 1, 28],
          ['wall.1.0', 2, 26],
          ['wall.1.0', 3, 26],
        ],
      },
    ],

    HANDS: [
      {
        ranges: [
          ['hand.0', 0, 13],
          ['hand.0', 1, 13],
          ['hand.0', 2, 13],
          ['hand.0', 3, 13],
          ['hand.extra', 0, 1],
        ],
        rotationIndex: 2,
      },
      {
        ranges: [
          ['wall.1.0', 0, 14],
          ['wall.1.0', 1, 15],
          ['wall.1.0', 2, 13],
          ['wall.1.0', 3, 13],
        ],
      },
    ],

    UNSHUFFLED: [
      {
        ranges: [
          ['wall.1.0', 0, 28],
          ['wall.1.0', 1, 28],
          ['wall.1.0', 2, 26],
          ['wall.1.0', 3, 26],
        ],
      },
    ],
  },
};
