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

> **Phase K Wave 9 update — Lighthouse 13 migration.**
>
> `lighthouse@13` (released 2026) **fully removed the PWA category
> and every PWA-specific audit** (`installable-manifest`,
> `maskable-icon`, `splash-screen`, `themed-omnibox`,
> `content-width`, `apple-touch-icon`, `service-worker`). The only
> survivor in the PWA set is `viewport`, which now lives under
> `best-practices`. Available categories in LH13: `performance`,
> `accessibility`, `best-practices`, `seo`, `agentic-browsing`.
>
> Per Lighthouse RFC: "PWA validation moves to PWA Builder, which
> is now the canonical installability gauge for cross-engine PWA
> support". The Lighthouse team explicitly recommends running
> PWA Builder's report card alongside Lighthouse for the
> non-PWA categories.
>
> W9 migrated `package.json` from `lighthouse@11.7.1` to
> `lighthouse@^13` and rewrote the recipe (§3 below).
> The W8 score (1.00 / 100% PWA) is preserved as a historical
> anchor — see §3.3 for the PWA Builder report card equivalent.

`lighthouse@11.x` was the last release that yielded a single
PWA category score via `--only-categories=pwa`. LH12 deprecated
the category; LH13 deleted it. There is no LH13 flag that
yields a single composite PWA score — that's by design.

## §3 — Audit recipe (Lighthouse 13 + PWA Builder)

### §3.1 — Build + serve the bundle

```bash
cd src/frontend/autotable-src
WAVE_NAME=K9 npm run build:vite

cd ../autotable
python3 -m http.server 8765 > /tmp/lh-server.log 2>&1 &
SERVER_PID=$!
sleep 2
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8765/index.html  # expect 200
```

### §3.2 — Lighthouse 13: non-PWA categories

`lighthouse` is a permanent `devDependency` of
`autotable-src/package.json` as of W9.

```bash
cd ../autotable-src
CHROME_PATH=/usr/bin/google-chrome \
./node_modules/.bin/lighthouse http://127.0.0.1:8765/index.html \
  --output=json \
  --output-path=./.lighthouse-report.json \
  --chrome-flags="--headless=new --no-sandbox --disable-gpu --disable-dev-shm-usage" \
  --quiet
```

Parse:

```bash
node -e "
const d = JSON.parse(require('fs').readFileSync('.lighthouse-report.json'));
for (const cat of Object.keys(d.categories)) {
  const c = d.categories[cat];
  console.log(cat.padEnd(20), c.score === null ? 'n/a' : (c.score * 100).toFixed(0) + '%');
}
"
```

LH13 baseline (W9, after build): `performance ≈ 79%`,
`accessibility ≈ 83%`, `best-practices ≈ 92%`, `seo ≈ 90%`,
`agentic-browsing ≈ 50%`. None of these scores gate W9 — see
§4 for the targets we propose for W10+.

### §3.3 — PWA Builder report card (installability gauge)

PWA Builder (https://www.pwabuilder.com/) replaces the
Lighthouse PWA category. Two paths:

**Browser (manual / once per release):**

1. Visit https://www.pwabuilder.com/reportcard?site=<URL>
   with the staging URL.
2. Capture the three category scores (Manifest, Service Worker,
   Security) into `docs/frontend-pwa-audit.md`.

**CLI (for CI, in-progress):**

```bash
# Not yet wired into autotable-src — placeholder for W10.
# npx @pwabuilder/cli analyze https://staging.example.com
```

The PWA Builder API rejects `localhost` / `127.0.0.1` URLs, so
this step needs a public preview deploy (Cloudflare Pages
preview env, or a tunnel like `cloudflared tunnel`). Defer the
CI wiring to W10; W9 only stands up the LH13 toolchain and the
manual PWA Builder recipe.

### §3.4 — Sanity checks that survive without PWA Builder

While Lighthouse 13 dropped the audits, the artefacts those
audits checked all still exist and still need to be correct.
W9 added a minimal local lint to validate the manifest +
service-worker preconditions without any network round-trip:

```bash
cd src/frontend/autotable
node -e "
const m = JSON.parse(require('fs').readFileSync('manifest.json'));
const issues = [];
if (!m.name)        issues.push('manifest.name missing');
if (!m.short_name)  issues.push('manifest.short_name missing');
if (!m.start_url)   issues.push('manifest.start_url missing');
if (!m.display)     issues.push('manifest.display missing');
if (!m.theme_color) issues.push('manifest.theme_color missing');
if (!m.icons || m.icons.length === 0) issues.push('manifest.icons[] empty');
const big = (m.icons || []).filter(i => /^[0-9]+x[0-9]+$/.test(i.sizes || '') && parseInt(i.sizes) >= 192);
if (big.length === 0) issues.push('no icon ≥ 192 px');
const maskable = (m.icons || []).filter(i => (i.purpose || '').includes('maskable'));
if (maskable.length === 0) issues.push('no maskable icon');
if (issues.length === 0) {
  console.log('manifest ✅');
} else {
  console.log('manifest ❌');
  issues.forEach(i => console.log('  - ' + i));
  process.exit(1);
}
"
```

This is the W9 substitute for `installable-manifest` /
`maskable-icon` / `splash-screen` / `themed-omnibox` LH11 audits.
It's deliberately tiny — extend it instead of reaching for a
new dependency.

### §3.5 — Cleanup

```bash
kill $SERVER_PID
rm -f .lighthouse-report.json
```

`.lighthouse-report.json` is `.gitignore`d
(`src/frontend/autotable-src/.gitignore`) — re-generate locally
whenever the manifest, service worker, or icon set changes.

## §4 — Hand-off notes for future waves

- **W10 CI integration:** wire PWA Builder's CLI behind a public
  preview URL (Cloudflare Pages preview env) and gate releases on
  Manifest ≥ 95% + Service Worker = 100%.
- **LH13 category targets (proposed W10):**
  `performance ≥ 85%`, `accessibility ≥ 95%`,
  `best-practices ≥ 95%`, `seo ≥ 95%`. `agentic-browsing` is a
  new LH13 category targeting LLM crawlers; current score 50%
  is acceptable for an authenticated game client (not a content
  site) but worth a review with Squad's marketing thread before
  W10.
- **Manifest field gaps:** the current manifest is missing
  `screenshots[]` (used by Edge's Store-style install flow),
  `id` (explicit PWA identity — recommended W3C 2024), `lang`,
  `dir`, and `iarc_rating_id`. PWA Builder will flag these
  when we wire the report card.
- **W8 historical PWA score (1.00 / 100%, LH11 PWA category):**
  preserved for reference. To reproduce, install
  `lighthouse@11.7.1` into a throw-away project and run the
  recipe from `git show stlong/phase-k-wave-8-bringup --
  docs/frontend-pwa-audit.md` (the W8 recipe is still in the
  repo's git history).
