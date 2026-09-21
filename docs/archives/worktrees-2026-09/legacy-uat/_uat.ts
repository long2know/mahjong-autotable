// Unified UAT import surface (Hudson-1 reconciliation, 2026-08-07, per Ripley).
// The assembly run merges two previously-split Changsha RED lanes into ONE canonical
// spec set in this worktree. Their two helper modules — `_uat_red.ts` (G4/G15/G17/G19
// world-coord + raw-WS harness) and `_uat-changsha.ts` (fresh-gameId / WS recorder /
// a11y probes for the RC/UI gates) — have DISJOINT export names, so this barrel gives
// a single entrypoint (`./_uat`) without touching either module or any existing spec.
//
// Existing specs keep their direct imports (still valid); new specs may import from
// here. The real-pointer surface (`./helpers/changsha-real-pointer`) additionally
// re-exports the `_playability` + `_uat-changsha` primitives for the folded RC gates.
export * from './_uat_red';
export * from './_uat-changsha';
