# Frontend PWA audit — Phase K Wave 8

Tracks Lighthouse PWA installability progression for the autotable
bundle (`src/frontend/autotable-src/` → built into `src/frontend/autotable/`).

## Wave 8 target

PWA category score **≥ 0.95** (95%).

## Wave 8 result

**PWA score: 1.00 (100%)** ✅ — every binary PWA audit passes.

| Audit | Score | Notes |
|-------|-------|-------|
| `installable-manifest`   | ✅ 1.0 | Manifest + service worker meet installability requirements (icon path bug fixed — see §1). |
| `splash-screen`          | ✅ 1.0 | Theme + bg color + name + 192px+ icon → Android splash renders. |
| `themed-omnibox`         | ✅ 1.0 | `<meta name="theme-color" content="#1e2a36">` matches manifest. |
| `content-width`          | ✅ 1.0 | No horizontal overflow at default viewport widths. |
| `viewport`               | ✅ 1.0 | `<meta name="viewport" content="width=device-width, initial-scale=1, …">`. |
| `maskable-icon`          | ✅ 1.0 | `img/icon-maskable-512.auto.png` with `"purpose": "maskable"`. |
| `pwa-cross-browser`      | · (manual) | Manual review — current build runs on Chrome, Firefox, Safari (W6 release notes). |
| `pwa-page-transitions`   | · (manual) | Manual review — autotable is a single-page app (no inter-page nav). |
| `pwa-each-page-has-url`  | · (manual) | Manual review — `/game/:id`, `/replay/:id` are URL-addressable. |

The three `pwa-*` audits are Lighthouse "manual" checks (score
`null` by design) and don't affect the numeric category score.

## §1 — The icon path bug found by the audit

### Baseline (pre-fix)

Initial W8 Lighthouse run scored the PWA category at **0.75 (75%)**
— well under target. The single failing audit was
`installable-manifest`:

```text
✗ installable-manifest — Web app manifest or service worker do not
  meet the installability requirements
  items: [{ "reason": "No supplied icon is at least 144 px square in
                       PNG, SVG or WebP format, with the purpose
                       attribute unset or set to \"any\"" }]
```

### Root cause

The manifest icon entries reference the source paths
(`img/icon-192.auto.png`, etc.) but Vite's HTML processor moves all
HTML-referenced icons to the build root with content-hashed names
(`icon-192.auto.88edf577.png`). The manifest is emitted as a static
copy via `vite.config.ts:copyStaticAssets`, so its `src` values
never get rewritten. Result: every manifest icon 404'd, and
Lighthouse couldn't find a single icon ≥ 144px to satisfy the
installability rule.

This had been broken since Wave 7 (the Parcel→Vite swap); W7's
PWA audit hadn't been re-run after the swap so the regression
went unnoticed until W8.

### Fix

`vite.config.ts:copyStaticAssets` now also copies the un-hashed
PWA icons to `out/img/icon-NNN.auto.png` so the manifest's `src`
paths resolve. The hashed root-level copies remain (referenced
from `index.html` via Vite's HTML processor) — they don't conflict
because they live at different paths.

```ts
// vite.config.ts
const iconNames = [
  'icon-16.auto.png', 'icon-32.auto.png', 'icon-96.auto.png',
  'icon-192.auto.png', 'icon-512.auto.png', 'icon-maskable-512.auto.png',
];
for (const name of iconNames) {
  copyFileSync(`${root}/img/${name}`, `${out}/img/${name}`);
}
```

Post-fix the audit passes at 1.00 (every other audit was already
green — the missing icon was the sole gating issue).

## §2 — Lighthouse version note

`lighthouse@13` (released 2026) **dropped the PWA category
entirely**, distributing the individual audits to other categories
and pruning most of them. Lighthouse 11.x is the last release with
a `--only-categories=pwa` flag that yields a category score.

The audit recipe in §3 pins `lighthouse@11` for repeatable scoring.
When/if the team wants to move to a PWA-Builder-based audit
(Microsoft's tool that replaces the deprecated Lighthouse path),
that work belongs in a separate doc.

## §3 — Audit recipe

```bash
# 1. Build the production bundle.
cd src/frontend/autotable-src
WAVE_NAME=K8 npm run build:vite

# 2. Serve the static output via any HTTP server.  We use python's
#    built-in for portability — no extra deps.
cd ../autotable
python3 -m http.server 8765 &
SERVER_PID=$!

# 3. Install lighthouse@11 locally (it is NOT a permanent
#    devDependency — the audit is run on demand only).
cd ../autotable-src
npm install --no-save --no-package-lock lighthouse@11

# 4. Run the PWA category audit.
node_modules/.bin/lighthouse \
  http://localhost:8765/index.html \
  --only-categories=pwa \
  --output=json \
  --output-path=./.lighthouse-pwa.json \
  --chrome-flags="--headless --no-sandbox --disable-dev-shm-usage" \
  --quiet

# 5. Parse the score.
node -e "
const data = JSON.parse(require('fs').readFileSync('.lighthouse-pwa.json'));
console.log('PWA score:', data.categories.pwa.score);
for (const ref of data.categories.pwa.auditRefs) {
  const a = data.audits[ref.id];
  const mark = a.score === 1 ? '✓' : (a.score === null ? '·' : '✗');
  console.log('  ' + mark + ' ' + ref.id + ' — ' + a.title);
}
"

# 6. Clean up.
kill \$SERVER_PID
```

The `.lighthouse-pwa.json` output is `.gitignore`d
(`src/frontend/autotable-src/.gitignore`) — re-generate locally
whenever the manifest, service worker, or icon set changes.

## §4 — Hand-off notes for future waves

- **Lighthouse 13+ migration:** the next audit refresh will need to
  drop the `--only-categories=pwa` flag and assemble a PWA score
  from individual audits (`installable-manifest`, `maskable-icon`,
  `splash-screen`, `themed-omnibox`, `viewport`) in Lighthouse 12+.
  Cleanest path is to write a small wrapper that runs the full
  category set and weights the PWA-relevant audits ourselves.
- **PWA Builder integration:** Microsoft's PWA Builder
  (https://www.pwabuilder.com/) is the recommended replacement.
  It surfaces additional installability checks (handlers,
  share_target, file_handlers, IARC rating) we don't currently
  declare. Plenty of room to grow the manifest in W9+.
- **Manifest field gaps:** the current manifest is missing
  `screenshots[]` (used by Edge's Store-style install flow),
  `id` (explicit PWA identity — recommended W3C 2024), `lang`,
  `dir`, and `iarc_rating_id`. None are required for a 1.00 score
  on Lighthouse 11, but PWA Builder would flag them. Defer to W9
  unless a release blocker surfaces.
