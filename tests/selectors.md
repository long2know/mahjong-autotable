
### Playability gate (#122) — additional live-game controls (merged deps)

Real controls the full-game gate drives after #123/#125/#127 landed:

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `#pickup-hud` / `#pickup-take-btn` | `<div>` / `<button>` | Manual-pickup HUD ("Your turn — pick N tiles") + the real "Take N" button the human clicks to draw hands 2..N. | `src/frontend/autotable-src/index.html` · `src/game-ui.ts:504-507,1587` |
| `#result-modal` / `#result-next` | Bootstrap modal / `<button>` | Per-hand scoring modal + "下一局 Next Hand" button that advances to the next hand (`match[1]={action:'nextHand'}`). `data-backdrop="static"`, keyboard-disabled — dismissable only via `#result-next`. | `src/frontend/autotable-src/index.html:366-392` · `src/game-ui.ts:972-975` |
| `#perspective` | `<input type=checkbox>` | Flat/perspective view toggle (also the `p` key → `MainView.setPerspective`). The gate toggles views live. | `src/game.ts:59,204-207` |
