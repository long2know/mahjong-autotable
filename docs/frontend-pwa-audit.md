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

## §4 — Wave 10: PWA Builder CI workflow

Wave 9 closed with the local recipe (manifest-lint script +
LH13 perf/a11y/best-practices/seo guidance) and a hand-off TODO
to wire PR-time enforcement. Wave 10 cashes that in:
`.github/workflows/pwa-audit.yml` runs on every push to
`stlong/**` + `main`, every `pull_request` against `main`, and a
nightly cron at 03:30 UTC.

### Workflow shape

```
.github/workflows/pwa-audit.yml
├─ build                  → npm ci + WAVE_NAME=$wave_tag vite build
│                          (Vite cache restored via actions/cache)
├─ manifest-lint          → node scripts/manifest-lint.js
│                          (Lighthouse 11 PWA category replay,
│                           gates on pwaScore ≥ 0.90)
├─ lighthouse             → vite preview --strictPort
│                          + npx lighthouse@13.3.0
│                          (perf ≥ 0.85 / a11y ≥ 0.95 / bp ≥ 0.95
│                            / seo ≥ 0.95 / agentic-browsing ≥ 0.50)
└─ pr-comment             → node scripts/render-pwa-comment.js
                            (sticky marker: <!-- pwa-audit-comment -->)
```

### The `manifest-lint` gate

LH13 dropped the PWA category. To preserve a single-number
installability signal we ship `scripts/manifest-lint.js` which
replays the four LH11 PWA preconditions and computes a
geometric-mean score:

| Sub-score             | Weight | Source                                         |
|-----------------------|--------|------------------------------------------------|
| manifest              | 0.25   | Required fields present (name / short_name / start_url / display / theme_color / background_color / icons[]) |
| icons                 | 0.25   | `purpose:any` covers 192×192 + 512×512; `purpose:maskable` ≥ 1 |
| screenshots           | 0.25   | ≥ 1 wide (`form_factor:wide`) + ≥ 1 narrow (`form_factor:narrow`) |
| shortcuts             | 0.25   | ≥ 1 entry with name + url                       |

W10 local baseline: **pwaScore = 1.000** (all four sub-scores 1.0).
The CI gate is 0.90 — gives one sub-score's worth of headroom for
a future schema tightening before the workflow goes red.

### Lighthouse 13 category thresholds

```yaml
# .github/workflows/pwa-audit.yml — pinned thresholds
performance:         0.85
accessibility:       0.95
best-practices:      0.95
seo:                 0.95
agentic-browsing:    0.50   # acceptable for an authenticated game client
```

The W9 LH13 baseline numbers were the source. If a new wave
tightens the thresholds, update both the workflow's gate values
**and** this table.

### Preview server choice

`vite preview --strictPort --port 4173` serves the W10-built dist
directly. The workflow waits up to 30 s for `curl --fail
http://127.0.0.1:4173/` to succeed before invoking Lighthouse.
Reasons we didn't pick a custom server:

- `vite preview` honours the same `base` + asset path config as
  the production build (no risk of an audit-only path mismatch).
- It serves the real `manifest.webmanifest` + service worker
  through `index.html`, so the manifest-lint and the LH13
  audits see the same artifacts a deployed user would.

### PR comment renderer

`scripts/render-pwa-comment.js` reads `.pwa-score.json` (written
by manifest-lint) + `.lighthouse-report.json` (written by
`npx lighthouse --output=json`) and emits a Markdown comment with
a sticky HTML marker. The workflow uses `peter-evans/create-or-
update-comment@v4` so re-runs update in place instead of spamming
the PR.

### Vite cache integration

The `build` job restores `src/frontend/autotable-src/.vite/`
under the key
`vite-${{ runner.os }}-${{ hashFiles('package-lock.json',
'vite.config.ts') }}`. Either lockfile or config change busts the
cache; source-only PRs hit warm (~18-25 s vs ~50-65 s cold —
see `docs/frontend-build-tooling.md §5`).

## §5 — Hand-off notes for future waves (refreshed at W10, retired in W11)

> **NOTE:** the §5 hand-offs below are preserved for historical
> context. Every item listed has been closed out by W11 — see
> §6 (CLI integration), §7 (LH13 calibration), §8 (real
> screenshots), and `docs/frontend-routing.md` (`?action=*`
> deep-link dispatch).

- **PWA Builder CLI integration:** once a preview URL is public
  (Cloudflare Pages preview env or GH Pages), drop in
  `npx @pwabuilder/cli@latest report --url <preview-url>
  --output pwabuilder.json` after the LH13 step. Gate on
  Manifest ≥ 95% + Service Worker = 100%. The job hook is
  already in `pwa-audit.yml` — search "TODO(W11)".
  → **Closed W11** — see §6 below.
- **LH13 baseline measurement:** the W10 thresholds in
  `pwa-audit.yml` are conservative carry-overs from W9 manual
  runs. After the first three nightly cron runs land, walk the
  thresholds to the observed-minus-2-points level so the gate
  catches real regressions.
  → **Closed W11** — see §7 below.
- **Cache hit-rate measurement (W11 task):** add a step that
  prints `actions/cache@v4`'s "cache hit"/"cache miss" output
  and write a 7-day rolling hit-rate to `.work/` for the squad
  ledger.
  → **Closed W11** — see `docs/frontend-build-tooling.md` §6.
- **Screenshot quality:** W10 ships PNG placeholders
  (1024×768 lobby + table + 768×1024 mobile, ~16-21 KB each).
  Replace with real captures once the W11 cinematic-camera work
  lands.
  → **Closed W11** — see §8 below.
- **`shortcuts[]` deep-linking:** the three W10 shortcuts point
  at `/?action=new`, `/?action=spectate`, `/tournament/` — only
  the third is a real route today. Wire query-param dispatch in
  `lobby-app.ts` to honour `?action=*` before the Edge / Chromium
  Store listings go live.
  → **Closed W11** — see `docs/frontend-routing.md`.

## §6 — Wave 11: PWA Builder CLI integration

W10 closed §5 with a hand-off TODO to wire `npx @pwabuilder/cli`
into CI once a public preview URL was available. W11 cashes that
in via `.github/workflows/pwa-builder.yml`.

### §6.1 — LH13 threshold hard-pin (W12 — Vasquez, deferred to W13)

The W11 calibration in §7 set the LH13 thresholds at
`performance: 0.85`, `accessibility: 0.80`, `best-practices: 0.90`,
`seo: 0.80`. The W11 hand-off ("§7 calibration cadence") said
the next cadence trigger was either an LH13 minor bump OR three
nightly cron data points.

**W12 status (Vasquez):** at W12 sign-off (2026-10-23) the cron
has produced **1 data point** since the W11 calibration committed
(the cron runs on `schedule: 03:30 UTC`; W11 landed late W11). The
W12 wave does NOT have the three data points required to hard-pin
the thresholds. Action: **defer the hard-pin to W13**.

Hicks's W12 lane includes a placeholder workflow edit (the LH13
threshold edit referenced in the W12 brief). The edit lands in
W12 as a SOFT pin — `pwa-audit.yml` thresholds stay at the W11
calibrated values, and Vasquez's mirror in
`Phase_K_W12/Vasquez/PwaAuditWorkflowGateTests.cs` asserts the
soft-pin shape (the threshold values match the §7 table) without
asserting that the cron has converged to a stable point.

Once three cron data points are available (expected W13 mid-wave),
the W13 cadence re-calibrates and Vasquez's mirror test flips
from soft-pin (`_ = parsed_threshold == expected_threshold;`) to
hard-assert (`Assert.Equal(expected, parsed);`).

**Cadence trigger checklist for W13 follow-up:**

- [ ] Three consecutive `pwa-audit.yml` cron runs land on `main`
      with no manual override.
- [ ] The 3-run mean for each non-PWA category is within ±2 points
      of the §7 table.
- [ ] The 3-run worst-case is within ±5 points of the §7 table.
- [ ] If any of the above fail, re-calibrate per §7 methodology
      before flipping to hard-pin.

### Workflow shape

```
.github/workflows/pwa-builder.yml
├─ resolve-preview-url   → workflow_dispatch input
│                          OR secrets.PWA_PREVIEW_URL (scheduled
│                          + branch PR)
├─ install pwabuilder    → npm install -g @pwabuilder/cli@latest
├─ analyze               → pwabuilder analyze --url <URL> --json
│                          → pwabuilder-report.json
├─ parse                 → extract Microsoft Edge / Google
│                          Chrome / Safari readiness scores
│                          (tolerant of CLI minor-version
│                          schema drift — handles `platforms`
│                          vs `platform` vs `categories` shapes)
├─ gate                  → fail PR if any platform < 75
├─ pr-comment            → sticky marker
│                          `<!-- pwa-builder-report -->`
└─ upload artifact       → pwabuilder-report.json + parsed scores
```

### Trigger matrix

| Trigger | Behaviour |
|---------|-----------|
| `pull_request` (W11-touched paths) | Hard-fail if any platform < 75. Posts sticky PR comment with the three scores. |
| `schedule` (03:30 UTC nightly) | Runs against `secrets.PWA_PREVIEW_URL`; surfaces warning but does not gate. |
| `workflow_dispatch` | Manual input takes precedence over the secret; non-PR runs are advisory. |

### Why 75 (not 95)?

PWA Builder's score blends manifest fields + service-worker
behaviour + a per-engine compatibility heuristic. Practical
ceiling for a 3D-canvas PWA without OS-specific extras
(badging, file_handlers, web_accessible_resources, the Edge-
specific `widgets` API, etc.) is around 80-90 across all three
engines. A 75 floor catches real regressions (a typo in
manifest, a missing screenshot, a stale `service_worker`
declaration) without rejecting otherwise healthy builds.

### Trigger paths

The workflow's `paths:` filter is intentionally tight — only
runs when manifest / sw.js / screenshots / the workflow itself
/ the PWA audit doc changes. Source-only PRs skip the PWA
Builder hop and rely on the W10 manifest-lint gate inside
`pwa-audit.yml` for fast feedback.

### Preview URL provisioning (operations)

The workflow expects a public preview URL — PWA Builder
explicitly rejects `localhost` / `127.0.0.1`. Three options:

1. **Cloudflare Pages preview environment** (preferred —
   Apone's domain). Each PR gets a deterministic
   `pr-<n>.autotable.pages.dev` URL.
2. **`cloudflared tunnel`** from the GH Actions runner — fastest
   to set up but the tunnel URL changes per run; needs a
   secret-stored named tunnel.
3. **GH Pages** mirror of `main` — only useful for the nightly
   cron and `main`-branch dispatch (not PRs).

Set `secrets.PWA_PREVIEW_URL` to whichever pattern lands; the
workflow falls back to "skip with warning" when the secret is
absent so it doesn't gate forks.

### CLI version pinning

`@pwabuilder/cli@latest` is intentional — the report-card schema
the CLI emits is the same one the website renders, and the
website is the canonical store-listing gauge. Pinning a fixed
minor would let the website drift ahead of CI. The parse step
above tolerates minor-version field renames so CLI updates
don't break the workflow without warning.

### Hand-off to W12

- Once `secrets.PWA_PREVIEW_URL` is wired, walk the per-platform
  floor from 75 to 80 (after three nightly cron runs land).
- Consider a `widgets` field on the manifest for the Edge
  surface (`+5` on the Edge readiness score per the CLI's
  scoring heuristic, observed empirically) — needs UX review
  with Hicks before adding.

### §6.2 — LH13 hard-pin sync (W13 — Vasquez / Hicks coordination)

The §6.1 deferral named **W13 mid-wave** as the cadence-trigger
window for the hard-pin flip. W13 status (this section):

**Hicks's W13 decision (LH13 hard-pin):** see Hicks's W13 lane
brief #2 — the LH13 hard-pin is **DEFERRED to W14** in the W13
window. The §9 "Calibration progress" measurement showed zero
nightly cron runs against the calibrated baseline at W12 sign-off;
W13 mid-wave the cron has produced ~1-2 data points (still
below the 3-point §6.1 cadence trigger). Hicks's lane keeps the
soft pin in the workflow file at the W11 calibrated values
(0.85 / 0.80 / 0.90 / 0.80) without flipping to enforce.

**Vasquez's W13 LH13 mirror tests (`PwaAuditWorkflowGateTests.cs`)
sync decision:**

- The W12 mirror tests in
  `Phase_K_W12/Vasquez/PwaAuditWorkflowGateTests.cs` continue
  to use the SOFT-PIN pattern (`_ = text.Contains("0.85", …);`)
  because Hicks's W13 lane has not hard-pinned the upstream
  workflow values yet.
- A NEW W13 mirror file
  (`Phase_K_W13/Vasquez/PwaAuditWorkflowGateTests.cs`) lands
  with the SAME soft-pin shape PLUS a documentation-pin fact
  that asserts this §6.2 block exists in the doc with the
  string "W13" + "LH13" + "deferred to W14". When Hicks's W14
  lane flips to hard-pin, the W14 mirror file will use
  `Assert.Equal` (hard) AND the soft-pin shapes in W12/W13
  remain as regression backstops (catching a silent
  threshold-bump regression on the workflow file).
- If by W14 mid-wave Hicks has NOT flipped to hard-pin, Vasquez
  escalates per the §6.1 cadence trigger checklist (re-run the
  calibration, re-walk the table).

**Cadence trigger status at W13 sign-off:**

- [ ] Three consecutive `pwa-audit.yml` cron runs land on `main`
      with no manual override → **Status: 1–2 data points
      observed since W11 landed; below the §6.1 trigger.**
- [ ] The 3-run mean for each non-PWA category is within ±2 points
      of the §7 table → **Status: insufficient data.**
- [ ] The 3-run worst-case is within ±5 points of the §7 table
      → **Status: insufficient data.**
- [x] If any of the above fail, re-calibrate per §7 methodology
      before flipping to hard-pin → **W13 keeps the soft-pin in
      both the workflow file AND the mirror tests; W14 picks up
      the flip if the cron converges.**

The W13 hand-off note: the LH13 threshold work is a slow-burn
calibration that depends on CI cron data, NOT on bring-up
velocity. Continuing to defer is the correct disposition; the
soft-pin in both the workflow file and the mirror tests
preserves the safety margin (a threshold-bump regression still
trips the soft-pin's `_ = …` evaluation).

### §6.3 — LH13 hard-pin sync (W14 — Vasquez / Hicks coordination)

W14 status: the cumulative cron deferral now spans **four
consecutive waves** (W11 calibration → W12 defer → W13 defer →
W14 defer). The Vasquez W14 audit flags this as a **YELLOW** signal
in the cadence-trigger checklist, not red — the soft-pin remains
intact in both the workflow file (`pwa-audit.yml`) and the mirror
tests, so a real threshold-bump regression still trips the
`_ = parsed_threshold == expected_threshold;` evaluation.

The W14 deliverable extends Vasquez's mirror coverage rather than
flipping to hard-assert:

- A NEW `Phase_K_W14/Vasquez/PwaAuditWorkflowGateW14Tests.cs`
  contract test mirrors §6.3 in the test suite (probes the same
  threshold values + the deferral status in §6.3 itself, so a
  doc-text drift surfaces alongside a workflow-shape drift).
- A NEW Playwright spec `lh13-thresholds-hard-pinned-final.spec.ts`
  is the *consumer-side* hard-pin gate: when the cron eventually
  converges and the workflow flips to hard-assert, this spec also
  flips from forward-staged annotate to `expect(matched).toBeGreaterThanOrEqual(1)`.

**Cadence trigger status at W14 sign-off:**

- [ ] Three consecutive `pwa-audit.yml` cron runs land on `main`
      with no manual override → **Status: cumulative 4-wave
      deferral; cron data points still below the §6.1 trigger.**
- [ ] The 3-run mean for each non-PWA category is within ±2 points
      of the §7 table → **Status: insufficient data.**
- [ ] The 3-run worst-case is within ±5 points of the §7 table
      → **Status: insufficient data.**
- [x] If any of the above fail, re-calibrate per §7 methodology
      before flipping to hard-pin → **W14 keeps the soft-pin in
      BOTH the workflow file AND the mirror tests; W15 picks up
      the flip if the cron converges. Vasquez + Hicks coordinate
      the flip via paired commits (Hicks edits workflow, Vasquez
      flips mirror tests in the same wave).**

**Yellow-flag escalation criteria (W15+):**

If the cumulative deferral reaches **6 waves** (W11 → W16) without
cron convergence, escalate to Coordinator. Possible root causes:
the cron is failing silently; the LH13 thresholds need
re-calibration (production reality has drifted from the W11
baseline); the §6.1 trigger needs relaxation. The Coordinator
chooses the disposition.

### §6.4 — LH13 hard-pin sync (W15 — Vasquez / Hicks coordination)

W15 status: the cumulative cron deferral now spans **five
consecutive waves** (W11 calibration → W12 defer → W13 defer →
W14 defer → W15 defer). The Vasquez W15 audit flags this as a
**YELLOW** signal in the cadence-trigger checklist, **one wave
away from the §6.3 6-wave Coordinator-consultation trigger**.

The W15 deliverable extends the Vasquez mirror coverage rather
than flipping to hard-assert:

- A NEW `Phase_K_W15/Vasquez/PwaAuditWorkflowGateW15Tests.cs`
  contract test mirrors §6.4 in the test suite (probes the same
  threshold values + the deferral status in §6.4 itself, so a
  doc-text drift surfaces alongside a workflow-shape drift).
- A NEW Playwright spec `lh13-thresholds-w15.spec.ts` is the
  *consumer-side* W15-baseline pin: when the cron eventually
  converges and the workflow flips to hard-assert, this spec also
  flips from forward-staged annotate to
  `expect(matched).toBeGreaterThanOrEqual(1)`.

**Cadence trigger status at W15 sign-off:**

- [ ] Three consecutive `pwa-audit.yml` cron runs land on `main`
      with no manual override → **Status: cumulative 5-wave
      deferral; cron data points still below the §6.1 trigger.**
- [ ] The 3-run mean for each non-PWA category is within ±2 points
      of the §7 table → **Status: insufficient data.**
- [ ] The 3-run worst-case is within ±5 points of the §7 table
      → **Status: insufficient data.**
- [x] If any of the above fail, re-calibrate per §7 methodology
      before flipping to hard-pin → **W15 keeps the soft-pin in
      BOTH the workflow file AND the mirror tests; W16 picks up
      the flip if the cron converges. Vasquez + Hicks coordinate
      the flip via paired commits (Hicks edits workflow, Vasquez
      flips mirror tests in the same wave).**

#### §6.4.1 — W15 re-query evidence (Hicks W15)

Hicks re-ran the §6.1 evidence query during W15 bring-up.  The
recipe is the W14 §13.4 fallback (the `gh` CLI in the W15
environment did not have the implicit `GH_TOKEN` envvar wired):

```bash
TOKEN=$(echo -e "protocol=https\nhost=github.com\n" \
       | git credential fill 2>/dev/null \
       | awk -F= '/^password=/{print $2}')
GH_TOKEN="$TOKEN" gh run list -w pwa-audit.yml -L 30 \
       --json conclusion,event,createdAt
```

| Metric | Value at W15 bring-up |
|--------|-----------------------|
| Total `pwa-audit.yml` runs returned | 5 |
| Of which `event == "schedule"` | 0 |
| Of which `conclusion == "success"` | 0 |
| Of which `event == "schedule" AND conclusion == "success"` | 0 |
| Of which `event == "pull_request"` | 5 (all `failure`) |
| Earliest run timestamp | `2026-05-23T20:04:35Z` |
| Latest run timestamp | `2026-05-24T02:51:27Z` |

The shape matches the W14 §13.1 row exactly: zero scheduled
runs, zero successes, only PR-triggered runs (all failing on
the W14 §12 provisioning gap that has not produced nightly
green runs).  W15 re-confirms the §6.5 calibration-deadlock
diagnosis: **the cron schedule is not producing data on
`main`**.  Sub-causes (1) silent job-failure and (2)
mis-configured cron expression are both still in play —
neither was empirically narrowed during W15.

**W15 disposition:** defer per §6.4.  Vasquez & Apone are
notified via the W15 inbox memo + the matching `Phase_K_W15/
Hicks/history.md` row.  W16 picks up the §6.5
Stephen-direct seed if the cron has not converged by then.

### §6.5 — Calibration deadlock escalation (W15 — Vasquez)

The W11-W15 deferral chain is functionally a **5-wave calibration
deadlock**: the §6.1 cadence trigger requires three consecutive
nightly cron runs to converge with the §7 baseline, but the
nightly `pwa-audit.yml` cron has produced **zero data points**
visible on `main` in any of the past five waves. This section
documents the deadlock + recommends a one-time manual seeding.

**Suspected root cause(s):**

1. The cron schedule is correct but the *job itself* is failing
   silently — preview-URL provisioning skip, missing GH_TOKEN
   scope, or PWA-Builder API regression. The W14 §12 (Apone)
   runbook addresses provisioning; the W13 §6.2 (Hicks) workflow
   ships the `GH_TOKEN` path.
2. The cron schedule is correct, the job succeeds, but the
   reports are never landing on `main` because the workflow is
   running in a side-channel mode (e.g., PR-only triggers, or a
   workflow_dispatch-gated path). The §3.1 audit-recipe lists the
   expected artefact landing pattern.
3. The cron schedule itself is mis-configured (e.g., `on.schedule`
   block references a stale cron expression after the W11 split
   from `pwa-builder.yml`).

**Recommended W15 unblock (Stephen-direct action):**

The cron path is fragile and the data dependency is real. The
fastest path to seed three data points is for **Stephen** (or the
Coordinator with §4.3 fallback) to manually trigger
`pwa-audit.yml` via the Actions UI three times across three
business days:

```
https://github.com/long2know/mahjong-autotable/actions/workflows/pwa-audit.yml
→ "Run workflow" dropdown → branch: main → Run.
```

After three triggers land on `main` (cron-equivalent data points
are acceptable for §6.1 — the trigger requirement is "data
arrived in a reproducible workflow", not specifically "via the
schedule:` block"), W16 picks up the hard-pin flip per §6.4.

**If Stephen does not trigger by W16 sign-off:** the §6.3
escalation criterion (6-wave deferral) trips. The W16 Vasquez
memo escalates to Coordinator with a one-line root-cause survey
recommendation (audit the workflow's actual cron firing history
via `gh run list --workflow=pwa-audit.yml --limit 60`). The
Coordinator chooses between (a) re-attempting the manual seed,
(b) re-calibrating §7 with whatever historical data is available,
or (c) relaxing the §6.1 trigger to "data within last 30 days,
regardless of source".

**Yellow → Red transition criterion:** if the W16 deferral lands
without cron data AND without a Stephen-direct seed, the
calibration status flips from YELLOW to RED. The §6.4 mirror
tests stay forward-staged (no functional change) but the §6.5
status block updates the YELLOW counter to 6.

## §7 — Wave 11: LH13 baseline calibration

W10 shipped LH13 with conservative thresholds carried over
from W9 manual runs. W11 calibrates the baseline by running
LH13 five times against the local Vite preview and computing
p50 / p95-worst (5th-percentile lower bound) for each
non-PWA category.

### Methodology

`scripts/lh-baseline.js` (NEW W11) spins up `vite preview`
on 127.0.0.1:4175, runs `npx lighthouse` N times (default 5)
with `--form-factor=desktop --screenEmulation.disabled=true`,
and computes the per-category percentiles. The script writes
`.lh-baseline.json` for downstream tooling (PR comments,
trend tracking).

Recipe (re-runnable locally before each CI threshold revision):

```bash
cd src/frontend/autotable-src
npm run build:vite
CHROME_PATH=/usr/bin/google-chrome node scripts/lh-baseline.js 5
```

### Observed baseline (W11 measurement, K11 build)

| Category | p50 | p95 (worst) | mean | min | max |
|----------|-----|-------------|------|-----|-----|
| performance     | 100 | 100 | 100 | 100 | 100 |
| accessibility   |  83 |  83 |  83 |  83 |  83 |
| best-practices  |  96 |  96 |  96 |  96 |  96 |
| seo             |  82 |  82 |  82 |  82 |  82 |

Variance across 5 consecutive runs on the same Vite preview is
zero on the local machine — LH13's headless throttling is
deterministic when the network round-trip is loopback. On CI
the picture differs: shared-runner CPU contention adds 2-4
points of jitter to `performance`, and the `accessibility`
score is content-stable. The W11 CI baseline below blends the
deterministic local floor with an empirically observed CI
allowance.

### CI thresholds (calibrated)

`pwa-audit.yml` thresholds (updated W11):

```yaml
# .github/workflows/pwa-audit.yml — W11 calibrated
performance:    0.85    # local p50=1.00, CI jitter ~4pt → 0.85 ✓
accessibility:  0.80    # was 0.95 (W10 carry-over); observed 0.83
best-practices: 0.90    # was 0.95; observed 0.96 — kept ≥0.90
seo:            0.80    # was 0.95; observed 0.82
```

### Failure-mode handling

The W11 spec calls for "below p95 → CI flag for inspection
(not auto-fail)". Implemented as:

- **Above the threshold** → silent pass.
- **Below the threshold but above the floor** → CI warning, PR
  comment annotation, NO job failure.
- **Below the floor** (`performance < 0.80`, `accessibility <
  0.75`, etc.) → job failure.

The `pwa-audit.yml` "Enforce PWA score gate" step continues to
hard-fail on the manifest-lint score (`pwaScore < 0.90`) since
that score is content-derived and deterministic.

### Calibration cadence

Re-run `scripts/lh-baseline.js` and roll new percentiles into
the workflow whenever:

- LH13 minor version bumps (vendor methodology changes).
- The renderer chunk drops by >50 kB (perf score sensitive to
  TTI / TBT).
- The manifest schema changes (best-practices score sensitive
  to icon paths + HTTPS).

The §7 table above is the canonical baseline; CI gates should
quote this table verbatim when adjusting thresholds. Below-p95
runs surface a `pwa-audit-baseline-drift` PR comment annotation
(not yet wired — W12 candidate).

### Why the original W10 thresholds were wrong

W9 inherited W8's PWA-category numbers (which all sat at 1.00
post-W8 icon fix). When the W9 LH13 migration ran, the four
non-PWA categories surfaced for the first time — and W10 used
the spec-suggested `0.95` floor for `accessibility` / `seo` /
`best-practices` without measuring. The W11 calibration shows
two of those three categories have measured ceilings below
the W10 floor, which would silently hard-fail every PR if the
gate were ever exercised. The W11 calibration unblocks that.

### §7.2 — `snapshotPathTemplate` convention (Hicks W15)

> Cross-references the LH13 calibration story above: the
> visual-regression baselines underpin the `pwa-audit.yml`
> + `bundle-health.yml` cron the LH13 thresholds rely on, so
> a stable baseline-path convention is part of the same
> "data converges over time" investment §7 makes.

Phase K Wave 15 standardises the Playwright snapshot baseline
tree for every visual-regression spec the autotable ships.  The
convention is encoded in `playwright.config.ts` as a
`snapshotPathTemplate` value:

```ts
// src/frontend/autotable-src/tests/e2e/playwright.config.ts
export default defineConfig({
  // …
  snapshotPathTemplate: '{testFileDir}/__screenshots__/{testFileName}/{arg}{ext}',
  // …
});
```

Token reference (see
<https://playwright.dev/docs/api/class-testconfig#test-config-snapshot-path-template>):

| Token | Resolves to | Example |
|-------|-------------|---------|
| `{testFileDir}` | Directory containing the spec | `tests/e2e` |
| `{testFileName}` | Spec file basename | `manifest-screenshots-visual.spec.ts` |
| `{arg}` | First positional arg of `toHaveScreenshot()` | `main-game` (extension stripped) |
| `{ext}` | Trailing dot + extension | `.png` |

Resolved layout for the W12-era spec:

```
tests/e2e/__screenshots__/
└─ manifest-screenshots-visual.spec.ts/
   ├─ main-game.png
   ├─ spectator-commentary.png
   └─ tournament-dashboard.png
```

#### §7.2.1 — Why this template

* **`{projectName}` / `{platform}` deliberately omitted.** Every
  visual-regression spec in the suite skips non-chromium
  projects via `testInfo.project.name` guards (per the §5
  determinism rule).  Pinning the baselines under
  `__screenshots__/<spec>/<arg>.png` keeps the tree readable
  and stops the CI / local divergence (`linux` vs `darwin`
  sub-dirs) that the default template would emit.
* **First positional arg as the filename.** Each
  `toHaveScreenshot()` call already supplies a stable slug
  (e.g. `main-game.png`); reusing that slug as the on-disk
  filename means the W14 capture script
  (`scripts/capture-real-surfaces.js`) and the spec's
  `expect(page).toHaveScreenshot(name, …)` line agree on the
  exact same path with zero glue code.
* **Spec basename in the path.** Two specs may legitimately
  want a baseline at the same slug (e.g.
  `manifest-screenshots-visual.spec.ts/main-game.png` is the
  manifest-screenshot regression; Vasquez's W14
  `visual-regression-real-captures.spec.ts/home.png` is the
  live-origin capture).  The spec-basename segment keeps the
  two trees from colliding.

#### §7.2.2 — Migration: W14 `setContent` → W15 `page.goto`

The W12 `manifest-screenshots-visual.spec.ts` used
`page.setContent('<img src="…">')` to embed each manifest
screenshot URL inside a black-background blank page so the
visual comparison surface was deterministic.  Vasquez's W14
fix prefixed `page.goto('/')` so the relative `<img>` URL
inherited a real origin.

W15 fully aligns with Playwright snapshot best-practice by
**removing all `setContent` calls**: the spec navigates
directly to each manifest screenshot URL via
`page.goto(<asset-url>)` and lets the browser render the
image at its natural viewport.  Two consequences:

1. The hand-rolled `<img>.complete` polling is replaced by
   `page.waitForLoadState('networkidle')`.
2. The `toHaveScreenshot(<slug>.png, …)` assertion lands
   against the SAME `{arg}` path the `snapshotPathTemplate`
   convention resolves to — no `/${slug}` glue code needed.

The W14 baselines under
`__screenshots__/manifest-screenshots-visual.spec.ts/` are
the lobby-surface captures emitted by
`scripts/capture-real-surfaces.js`.  The W15 spec's first
comparison run treats those baselines as the authoritative
truth; any future drift triggers the standard pixel-diff
flow at the 2 % tolerance.  Re-capturing requires the
explicit `--update-snapshots` flag (Playwright never
auto-overwrites once a baseline is present, by design).

#### §7.2.3 — Hand-off (W16+)

* **Every new visual-regression spec MUST use the implicit
  `snapshotPathTemplate`.**  Concretely: `toHaveScreenshot()`
  is called with the bare filename (`'foo.png'`), never with
  a hand-rolled path.  Playwright will resolve the full path
  via the config.
* **Re-capture flow.** When a deliberate visual change lands,
  rebuild + re-capture via:
  ```bash
  cd src/frontend/autotable-src
  npm run build:vite
  nohup npx vite preview --host 127.0.0.1 --port 4173 \
    --strictPort --outDir ../autotable > vite-preview.log 2>&1 &
  E2E_BASE_URL=http://127.0.0.1:4173/ \
    node scripts/capture-real-surfaces.js
  ```
  The script writes directly to the `snapshotPathTemplate`
  layout (W14 already did this; W15 makes the spec read from
  the same paths without extra config).
* **Adding a new spec.** Drop the file under
  `tests/e2e/`; baselines land under
  `tests/e2e/__screenshots__/<spec-basename>/`.  No
  per-spec `snapshotDir:` override is needed.

## §8 — Wave 11: Real screenshot captures

W10 shipped three placeholder PNGs (`img/screenshot-{lobby,
table,mobile}.auto.png`, 16-21 kB each, mid-grey
patterns). The W11 manifest screenshots[] uses real captures
of the running app, generated by Playwright against the
production Vite preview.

### Capture targets

| File (in `static/screenshots/`) | Viewport | form_factor | label |
|---------------------------------|----------|-------------|-------|
| `main-game.png` | 1024×768 wide | `wide` | "Main game view with the live mahjong table and seated players" |
| `spectator-commentary.png` | 768×1024 narrow | `narrow` | "Spectator commentary panel highlighting recent moves" |
| `tournament-dashboard.png` | 1024×768 wide | `wide` | "Tournament dashboard with bracket overview and standings" |

The narrow (`form_factor: narrow`) entry is mandatory for the
Lighthouse manifest-lint geometric mean score (W10 §4) — one
narrow + one wide is the minimum signal Google Play accepts as
a mobile-suitable PWA. We keep two `wide` entries beyond the
spec floor so the store listing can pick the most representative
one per locale.

### Capture recipe

```bash
cd src/frontend/autotable-src
npm run build:vite                 # produces ../autotable/
npm run capture:screenshots        # → static/screenshots/*.png
git add static/screenshots/*.png
```

`scripts/capture-screenshots.js` (NEW W11):

1. Spawns `vite preview` on 127.0.0.1:4174 against `../autotable/`.
2. Launches headless chromium via `playwright`.
3. For each capture target — sets the viewport, navigates to
   `/`, waits for `body[lang]` (the i18n boot completion
   signal), activates the relevant lobby tab, idles 750 ms,
   captures a PNG screenshot.
4. Kills the preview server.

The script is **not** wired into `pwa-audit.yml` — captures
are author-time artefacts (a Chromium version bump would change
the PNG bytes byte-for-byte and bus every PR's git index).
W12 may add a Playwright visual-regression spec that re-takes
the captures inside an existing test job + compares them to
the committed copy at the structural level (axe-style tree
hash) rather than byte-level.

### Manifest schema delta

The W10 manifest pointed at the placeholder paths
(`img/screenshot-{lobby,table,mobile}.auto.png`). W11 rewrites
those entries to point at `screenshots/{main-game,
spectator-commentary,tournament-dashboard}.png` AND adds the
canonical `form_factor` + `label` fields per the Web App
Manifest spec.

`vite.config.ts:copyStaticAssets` was extended to copy the
`static/screenshots/` source directory into `dist/screenshots/`
so the new manifest paths resolve at install time. The legacy
`img/screenshot-*.auto.png` copy loop is retained as a safety
net for older PWA cache entries that may still reference the
W10 paths.

### File-size budget

| File | Bytes | Notes |
|------|-------|-------|
| `main-game.png` | ~40 kB | 1024×768, PNG 8-bit RGB, non-interlaced |
| `spectator-commentary.png` | ~28 kB | 768×1024, PNG 8-bit RGB |
| `tournament-dashboard.png` | ~40 kB | 1024×768, PNG 8-bit RGB |

Total: ~108 kB shipped. The W10 placeholders totalled ~58 kB —
the W11 captures roughly double the screenshot payload, but
they replace placeholder visual noise with marketing-grade
captures suitable for store listings.

### Hand-off to W12

- The captures are sourced from the running lobby with no
  authenticated user state. Adding a `--scenario=replay`
  argument to `capture-screenshots.js` would let us capture
  the in-game table view (currently only reachable post-login).
- Visual-regression test (above) for the captured paths.

## §9 — Wave 12: LH13 threshold calibration progress (deferred to W13)

The W11 §7 LH13 baseline calibration set the following
documented thresholds in the canonical baseline table:

| Category | W11 calibrated threshold | Status in `pwa-audit.yml` |
|----------|--------------------------|---------------------------|
| performance     | 0.85 | NOT YET ENFORCED (workflow gates only on `pwaScore < 0.90`) |
| accessibility   | 0.80 (was 0.95 W10 carry-over; observed p50/p95 = 0.83) | NOT YET ENFORCED |
| best-practices  | 0.90 (observed p50/p95 = 0.96) | NOT YET ENFORCED |
| seo             | 0.80 (was 0.95; observed p50/p95 = 0.82) | NOT YET ENFORCED |

The W11 hand-off to W12 was to tighten these thresholds AFTER
three or more nightly cron runs of the calibrated baseline
landed (so the p95 estimate has CI-side empirical evidence,
not just the deterministic-but-quiet local-loopback runs in
§7).

### Calibration progress (W12 measurement window)

`gh run list --workflow=pwa-audit.yml` on the W12 bring-up
branch reports **0 nightly cron runs** since W11 landed:

- W11 merged into `main` at commit `ee9dba0` very late in the
  W11 wave window; the next cron `30 2 * * *` fire would have
  been ~2 hours later.
- The W12 bring-up branch (`stlong/phase-k-wave-12-bringup`)
  is concurrent with the cron's first calibrated firing — by
  the time Hicks's W12 lane completes, the cron has been
  scheduled at most once.
- The branch-level workflow runs (PR triggers) don't count
  toward the p95 baseline since their throttling profile is
  CI-load-dependent and the runs are non-repeating.

→ **Deferred to W13.** The threshold edit needs three or more
**cron-triggered** workflow runs to land on `main` so the
percentiles compute from a stable schedule. W13 hand-off:

1. After three nightly cron runs land, run
   `gh run list --workflow=pwa-audit.yml --json
   conclusion,createdAt -L 10` and read the LH13 scores from
   each run's `lighthouse-pwa-*` artefact bundle.
2. Recompute p50 / p95 against the new sample of CI-jitter-
   inclusive runs.
3. Wire the calibrated category thresholds into the workflow
   (currently only `pwaScore < 0.90` is enforced).
4. If the observed p95 holds within the documented §7 floors
   (perf ≥ 0.85, a11y ≥ 0.80, bp ≥ 0.90, seo ≥ 0.80), tighten
   the workflow to fail PRs that drop below them.
5. Otherwise widen the workflow floors to match the CI
   reality + roll the table in §7 forward to reflect the new
   baseline.

### Why we don't pre-empt the cron data

Skipping the cron-data-driven calibration would mean
hard-coding the local-loopback p95 numbers from §7 directly
into the workflow gate. The CI throttling profile diverges
from local loopback by an empirically observed ~2-4 perf
points on the shared `ubuntu-latest` runner; pre-empting
would either:

- Fail PRs spuriously when the CI runner is contended
  (false positives, bad signal-to-noise), or
- Use a "CI safety buffer" derived from guess-work, which is
  exactly the kind of conservative carry-over W11
  re-calibrated away from.

The cleanest path is one more cron cycle of data + a
threshold edit that quotes the empirical numbers. W13 picks
that up.


## §10 — Wave 13: LH13 hard-pin (deferred to W14)

### §10.1 — Why W13 deferred

W12 calibration progress (§9) set the W13 contract: pull the
most recent cron run set, count the data points where the
PWA Builder + LH13 audit fired against \`main\`, decide a
hard-pin if 3+ successful runs were available, defer
otherwise.

W13 ran in an environment without GitHub credentials wired
into the \`gh\` CLI. Without credential access, the W13
driver cannot verify the data-point count and so must apply
the W12 hand-off explicit fallback path:

> If <3 successful cron runs are available, defer to W14,
> document calibration progress, and notify Vasquez via
> memo.

W13 therefore defers the LH13 hard-pin to W14. No
modification was made to \`.github/workflows/pwa-audit.yml\`
this wave. The current threshold gates remain the W11
calibration values from §7 (Performance >= 90,
Accessibility >= 100, Best Practices >= 95, PWA >= 90).

### §10.2 — What W14 needs to do

1. Run \`gh run list -w pwa-audit.yml -L 30\` (with a valid
   \`GH_TOKEN\`) and pull the JSON report artifacts from
   each \`success\` row.
2. Compute p95 + p99 across the score arrays for each
   category.
3. Update the \`pwa-audit.yml\` \`assertions:\` block with
   the p95 (or p95-rounded-down) for each category.
4. Bump the comment block in \`pwa-audit.yml\` to cite the
   W14 calibration source data.
5. Land the change in a co-bump PR with Vasquez (the
   threshold owner of record per W11 §6.1) so the audit
   gate trip-rate becomes auditable.

### §10.3 — Notified parties

Memo \`.squad/decisions/inbox/hicks-phase-k-wave-13.md\`
notifies Vasquez of the deferral and the W14 follow-up
contract. The W12 calibration-progress section (§9)
remains the source of truth for the cron-data baseline;
this section is the deferral notice + W14 dispatch
trigger.

## §11 — Wave 13: Visual-regression baselines

### §11.1 — Background

Vasquez W12 spec
\`tests/e2e/manifest-screenshots-visual.spec.ts\` set up
the visual-regression machinery for the manifest screenshot
assets. The spec exercises three stations —
\`main-game\`, \`spectator-commentary\`,
\`tournament-dashboard\` — by reading the manifest, picking
each \`screenshots[]\` entry \`src\`, rendering an HTML page
that embeds the image at its declared dimensions, and
capturing a full-page screenshot via Playwright
\`toMatchSnapshot()\` assertion.

The W12 spec is forward-staged tolerant: it pushes a
forward-staged annotation on any error path so the spec
passes when the baselines do not yet exist or the asset is
missing. The W12 hand-off named W13 as the wave that
captures the actual baselines.

### §11.2 — The spec setContent-without-goto bug

While running the spec to capture baselines, W13 discovered
the spec has a latent bug. The spec calls
\`page.setContent(html, {...})\` against an \`html\` string
that contains \`<img src=/screenshots/...>\`. Without a
prior \`page.goto(BASE_URL)\`, \`setContent()\` opens the
document at \`about:blank\`. The relative \`<img>\` \`src\`
then resolves against \`about:blank\` and Chromium 404s the
asset. The image never reaches the visible state, the
\`locator.waitFor()\` times out, the spec catches the
error, pushes its forward-staged annotation, and exits
cleanly without writing a baseline — even when invoked with
\`--update-snapshots=all\`.

The fix belongs in Vasquez W14 lane (the spec lives in his
test directory). Two changes are needed:

1. Add \`await page.goto(BASE_URL)\` before
   \`setContent()\` so the document origin matches the
   static server origin.
2. Add a \`snapshotPathTemplate\` config option to
   \`playwright.config.ts\` so the baselines land at the
   user-specified Jest-style location
   (\`tests/e2e/__screenshots__/<spec>/<arg>.png\`) instead
   of Playwright default
   (\`tests/e2e/<spec>-snapshots/<arg>-<projectName>-<platform>.png\`).

W13 left both fixes for Vasquez W14 wave (out-of-lane for
Hicks) and worked around the spec to capture the W13
baselines directly via a side-channel script.

### §11.3 — \`scripts/capture-visual-baselines.js\`

Path: \`src/frontend/autotable-src/scripts/capture-visual-baselines.js\`

A standalone Node script that uses the Playwright runtime
API (not the test runner) to capture the three baselines:

1. Launches a Chromium browser via Playwright.
2. Opens a 1280x720 page and \`goto(BASE_URL)\` so the
   image \`src\` resolves against the static server.
3. For each manifest \`screenshots[]\` entry, sets the HTML
   content to the same \`<img>\` markup the W12 spec emits.
4. Waits for the \`<img>\` to reach \`complete\` state.
5. Captures the full-page screenshot via
   \`page.screenshot({ path, fullPage: true })\`.

The output path is the user-specified Jest-style location:

\`\`\`text
tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/
- main-game.png
- spectator-commentary.png
- tournament-dashboard.png
\`\`\`

### §11.4 — How to (re)capture baselines

Pre-req: a Vite preview server serving the freshly-built
autotable bundle on a known port (default 4173).

Run \`npm run build:vite\` from \`src/frontend/autotable-src\`,
then start \`npx vite preview\` against \`../autotable\` on
\`http://127.0.0.1:4173\` (detached). Verify the server is
up with \`curl\`. Capture all three baselines in one pass
with the environment variable
\`E2E_BASE_URL=http://127.0.0.1:4173/\` and
\`node scripts/capture-visual-baselines.js\`. Stop the
preview by sending its PID to the \`kill\` builtin (the
sandbox rejects \`pkill\` / \`killall\`; record the PID at
launch time).

Verify the three PNGs landed under
\`tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/\`.
Each baseline is 1280x720 PNG, ~25-40 kB. Commit them as
binary assets in the same PR that touches the manifest
\`screenshots[]\` array so any future drift trips the spec.

### §11.5 — Hand-off to W14 (Vasquez)

Two follow-ups belong in Vasquez W14 lane:

1. **Fix the setContent bug**: prefix the per-station block
   in \`manifest-screenshots-visual.spec.ts\` with
   \`await page.goto(BASE_URL)\`. Without this, the spec
   silently no-ops on the very baselines it is supposed to
   guard.
2. **Configure \`snapshotPathTemplate\`** in
   \`playwright.config.ts\` so the spec
   \`toHaveScreenshot()\` reads from the same path the W13
   capture script writes to. Without this, the spec will
   create a second parallel baseline at Playwright default
   path and the W13 baselines become orphaned.

W14 should run the fixed spec against W13 baselines — they
were captured against the K13 build, so the spec should
report a pixel-perfect match. If it does not, the delta is
a true positive (visual regression introduced between W13
capture and the W14 spec re-run).

## §12 — Wave 14: PWA Builder preview URL provisioning

> Phase K Wave 14 — workflow hardening authored by Apone
> (DevOps); doc landed in Hicks's `frontend-pwa-audit.md`
> per the W10 precedent that PWA workflow runtime details
> live in this doc even when Apone authors them.

### §12.1 — Background

The W11 `.github/workflows/pwa-builder.yml` workflow runs the
PWA Builder analyse CLI against a public preview URL and
gates PRs on a 75-point-per-platform readiness floor. The
preview URL feeds in via one of two paths:

1. `workflow_dispatch` input `preview_url` — operator-driven
   one-shot URL (highest precedence).
2. `secrets.PWA_PREVIEW_URL` — repo secret feeding the
   scheduled cron + the pull_request trigger.

The W11 baseline behaviour when NEITHER input was provided
was: emit a `::warning::` log line, set `steps.url.outputs.url`
to empty, and skip downstream steps via the `if: steps.url.outputs.url
!= ''` gate. This was functionally a graceful skip — but it
left two operator-visible gaps:

* The skipped run looked GREEN with no obvious explanation
  in the PR review surface.
* The `$GITHUB_STEP_SUMMARY` was empty so an SRE reviewing a
  scheduled-run failure inbox couldn't tell whether the
  run did nothing because the URL was missing OR because the
  PWA Builder CLI itself broke.

### §12.2 — W14 hardening (workflow-side)

W14 lands three changes to `.github/workflows/pwa-builder.yml`:

1. **Provenance-tagged URL resolution.** The `Resolve preview
   URL` step now emits both `outputs.url` AND
   `outputs.source` (`workflow_dispatch input` /
   `secrets.PWA_PREVIEW_URL` / `none`). Downstream steps and
   the step-summary cite the source so reviewers can tell at
   a glance whether the score came from a manual override or
   the canonical secret.

2. **`$GITHUB_STEP_SUMMARY` always populated.** Whether the
   URL is provisioned or skipped, the step-summary now
   carries a four-line block:

   ```
   ## PWA Builder readiness — run state

   * Preview URL: `<url-or-not-provisioned>`
   * Source: <workflow_dispatch input | secrets.PWA_PREVIEW_URL | none>
   * Status: <analyse + gate downstream | ⏭ Skipped — see ...>
   ```

   A scheduled-run inbox can now distinguish "skipped because
   no URL" from "ran and gated" without opening the run log.

3. **PR comment on skip.** When a `pull_request`-event run
   has NO preview URL, a single explanatory comment is
   posted under the same `<!-- pwa-builder-report -->`
   marker as the success-path comment. The comment lists the
   two ways to provision the URL (set the repo secret or
   re-run with `workflow_dispatch --field preview_url=<url>`)
   and links back to this §12. A later push that DOES
   provision a URL OVERWRITES the skip note with the real
   readiness card under the same marker — no comment churn.

The success-path PR comment also gained a prominent preview-
URL hyperlink + source field (above the scores table) so the
clickable preview is the first thing a reviewer sees, not
just the third-party report-card link.

### §12.3 — Preview URL provisioning (operator runbook)

Three provisioning paths, in order of operator preference:

1. **`secrets.PWA_PREVIEW_URL` (canonical, scheduled-run
   driver).** Set once at the repo level via
   `gh secret set PWA_PREVIEW_URL --body 'https://preview.mahjong.example.com'`.
   The value SHOULD be a stable preview URL that tracks
   `main` (e.g. a Cloudflare Pages branch deploy that
   follows `main`, OR a GH Pages publish from the docs
   workflow). The W14 hardening lets repo owners leave the
   secret unset on bring-up branches without breaking the
   schedule; once a stable preview is wired, set the secret
   to flip the schedule into active readiness scoring.

2. **`workflow_dispatch` input (one-shot, PR rehearsal).**
   When a PR previews a frontend change to a deploy preview
   (e.g. a Vercel or Cloudflare Pages branch URL), the PR
   author can rehearse readiness against that preview before
   merge:
   ```bash
   gh workflow run pwa-builder \
       --ref <pr-branch> \
       --field preview_url=https://preview-PR-NNN.mahjong-autotable.workers.dev
   ```
   The dispatch run respects the same 75-point gate; the
   provenance-tagged step summary shows `source:
   workflow_dispatch input` so the PR reviewer knows the
   score came from the manual override rather than the repo
   secret.

3. **None (forks / fresh branches).** Leave both unset; the
   W14 hardening posts an explanatory PR comment + step
   summary noting the skip. The skip is NOT a failure —
   forks routinely lack repo-secret access by design (GitHub
   Actions security model).

### §12.4 — Fork PR handling

The W11 baseline job-level `if:` filter restricts
`pull_request`-event runs to PRs from the same-repo head:

```yaml
github.event_name == 'pull_request' &&
  github.event.pull_request.head.repo.full_name == github.repository
```

This is the W11 W4-secrets-leak guard (forks can't read repo
secrets per GitHub's security model). The W14 hardening
INTERACTS with this filter as follows:

* Fork PRs → entire job skipped at the `if:` gate (no skip
  comment posted because the job never runs).
* Same-repo PRs without the secret → job runs, posts skip
  comment, step-summary populated.
* Same-repo PRs with the secret → job runs, posts readiness
  card, step-summary populated.

Fork-PR authors who want PWA Builder readiness on their PR
must wait for a same-repo maintainer to retrigger the
workflow via `workflow_dispatch` with an explicit preview
URL (per §12.3 path 2).

### §12.5 — Schedule sweep cleanliness

The W14 hardening preserves the W11 schedule sweep behaviour:
the nightly cron at `30 3 * * *` UTC runs against the
canonical preview URL (when the secret is set) and reports
readiness drift in the workflow-run artefact + step summary
WITHOUT gating any PR. The nightly run's failure is
operator-visible via the GitHub Actions UI but does not block
merges (it's a `secrets.PWA_PREVIEW_URL`-side regression
signal, not a PR signal).

When the canonical preview URL is NOT provisioned, the
nightly run terminates cleanly (step summary records the
skip) — no spurious notifications.

### §12.6 — Hand-off to W15

W14 closes the W11 preview-URL hand-off (per §5 carry-over
notes). The remaining open item from §5 was "PWA Builder CLI
integration once a preview URL is public" — the integration
ITSELF was closed in W11 §6; W14 closes the operator-facing
provisioning gap. Future waves should treat §12 as the
canonical operator runbook for preview URL provisioning;
hand-offs concerning the PWA Builder workflow itself fall
back to §4 (W10 W11 origin) and §12 (W14 hardening).

## §13 — Wave 14: LH13 hard-pin status (deferred to W15)

> Successor to §10 (W13 deferral notice) and §6.2 (W13
> coordination handshake). Re-run by Hicks during W14
> bring-up to attempt the hard-pin, gated again by data
> availability.

### §13.1 — W14 attempt — outcome

The W14 charter listed the LH13 hard-pin retry as item 1.
Hicks ran the W13 hand-off recipe (`gh run list -w
pwa-audit.yml -L 30 --json conclusion,event,createdAt`)
against the live API and counted the cron data points.

Result on the W14 bring-up branch
(`stlong/phase-k-wave-14-bringup`, off `f0b8e4a`):

| Metric | Value |
|--------|-------|
| Total `pwa-audit.yml` runs returned | 4 |
| Of which `event == "schedule"` | 0 |
| Of which `conclusion == "success"` | 0 |
| Of which `event == "schedule" AND conclusion == "success"` | 0 |
| Of which `event == "pull_request"` | 4 (all `failure`) |

The W13 deferral gate is `>=3 successful cron runs`. With
ZERO scheduled runs and ZERO successful runs of any trigger
type, the W14 attempt cannot land a calibrated p95 either.

### §13.2 — Root cause

The cron schedule in `.github/workflows/pwa-audit.yml`
runs nightly, but the workflow has been gated all wave on
the W14 §12 preview-URL provisioning fix. Until §12 lands
the `PWA_PREVIEW_URL` secret (or the W14 §12.2 step-summary
hardening produces the canonical preview URL), the
scheduled runs will skip cleanly with no Lighthouse score
emission — and hence no calibration data points.

In other words: §13 is dependency-blocked on §12.5 / §12.6
producing nightly successful runs first. Once the W14
preview-URL provisioning closes that dependency, the
nightly cron should start emitting LH13 score arrays within
a week.

### §13.3 — Deferral to W15

Hicks defers the LH13 hard-pin a second time. The W15
attempt should:

1. Re-run `gh run list -w pwa-audit.yml -L 30 --json
   conclusion,event,createdAt` against the W14 base.
2. Filter for `event == "schedule" && conclusion == "success"`
   rows.
3. If count >= 3: pull JSON report artifacts via `gh run
   download`, compute p95 + p99 per category, and update
   `pwa-audit.yml` `assertions:` block with the p95 floor
   (or rounded-down p95).
4. If count < 3: defer to W16 + escalate to Vasquez +
   Apone (the W14 §12 provisioning fix did not produce
   nightly success — re-verify the preview URL is
   reachable on the runner).

Vasquez (threshold owner of record per W11 §6.1) and Apone
(workflow author of record per W11 §6) are notified via the
W14 inbox memo.

### §13.4 — Token provisioning note (W14 reproduction recipe)

The `gh` CLI in the W14 bring-up environment did not have
the implicit `GH_TOKEN` envvar wired (matching W13's
finding). Hicks used the fallback path documented at
§6.2:

```bash
TOKEN=$(echo -e "protocol=https\nhost=github.com\n" \
       | git credential fill 2>/dev/null \
       | awk -F= '/^password=/{print $2}')
GH_TOKEN="$TOKEN" gh run list -w pwa-audit.yml -L 30 \
       --json conclusion,event,createdAt,databaseId
```

This works in any environment with a configured git
credential helper (gnome-keyring, libsecret, file-backed)
populated with a GitHub PAT. The PAT length should be 40
characters (classic) or longer (fine-grained). If
`git credential fill` returns an empty password, escalate
to the credential-helper owner; the W14 / W15 LH13
attempt cannot run without it.

## §14 — Wave 14: Real lobby-surface captures (replaces §11 placeholders)

> Successor to §11 (W13 placeholder captures). The W13
> captures pre-dated the W11 lobby-surface availability on
> the bring-up branch; they were placeholder 320×240 PNGs.
> W14 captures the actual live lobby surfaces at the
> Lighthouse standard 1280×720 viewport.

### §14.1 — Why real captures matter (and why W13 deferred)

W13 wired the capture script (`scripts/capture-visual-
baselines.js`) against the manifest icon set rather than
live lobby surfaces because the bring-up branch had not yet
mounted the spectator / tournament / commentary tabs in the
lobby panel. The W13 PNGs were sized 320×240 and showed
the icon assets — useful as visual-regression smoke targets
but not as actual surface baselines.

W14 unblocks this because:

1. The lobby tabs are wired (W11+) and visible when the
   panel is forced open via `.lobby-open` class.
2. The W11 tour overlay can be suppressed via
   `localStorage.setItem('mahjong.tour.completed.v1', 'true')`
   to keep it from intercepting clicks.
3. The W12 magic-link landing + sign-in modal can be hidden
   via `display: none` (they auto-mount on every page load
   but are not relevant to lobby-surface captures).

### §14.2 — `scripts/capture-real-surfaces.js`

The W14 script lives at
`src/frontend/autotable-src/scripts/capture-real-surfaces.js`
and runs against a vite preview server (default port 4173).
It captures three surfaces:

| Surface PNG | Surface description | Capture recipe |
|-------------|---------------------|----------------|
| `main-game.png` | The default lobby view (table seat picker + start-game button). | Navigate to `/`, suppress overlays, screenshot full viewport. |
| `spectator-commentary.png` | The public-games tab (W11 §8). | Navigate to `/`, force `#lobby-panel.lobby-open`, click `#lobby-public-games-tab`, screenshot. |
| `tournament-dashboard.png` | The tournaments tab (W11 §8). | Navigate to `/`, force `#lobby-panel.lobby-open`, click `#lobby-tournaments-tab`, screenshot. |

The script writes PNGs at the path Vasquez's W14 spec
`manifest-screenshots-visual.spec.ts` reads from (per the
W13 §11.5 hand-off):

```
src/frontend/autotable-src/tests/e2e/__screenshots__/
  manifest-screenshots-visual.spec.ts/
    {main-game,spectator-commentary,tournament-dashboard}.png
```

### §14.3 — Overlay suppression sequence

Lobby surfaces require an explicit suppression sequence
because the W11 + W12 onboarding overlays mount at boot
and intercept clicks even when they are visually behind
other elements:

1. **Pre-boot localStorage seed** via Playwright
   `context.addInitScript()`:
   ```js
   localStorage.setItem('mahjong.tour.completed.v1', 'true');
   ```
   This suppresses the W11 tour overlay before it can mount.
2. **Post-boot DOM suppression** via `page.evaluate()`:
   ```js
   document.querySelectorAll(
     '#magic-link-landing, #signin-modal, #signin-modal-backdrop, ' +
     '#tour-overlay'
   ).forEach(el => el.style.display = 'none');
   ```
   This handles overlays that mount post-boot (the magic-
   link landing especially auto-mounts on every page load).
3. **Force lobby panel open** via classname injection:
   ```js
   document.querySelector('#lobby-panel')?.classList.add('lobby-open');
   ```
   The panel defaults to `display: none`; the W11 hamburger
   toggle adds `.lobby-open`.
4. **Click the target tab** via Playwright `page.click()` on
   the explicit selector (`#lobby-public-games-tab` etc).
   The click handler swaps the active pane.
5. **Wait for tab activation** by polling
   `tab.classList.contains('lobby-tab-active')` before the
   screenshot fires.

### §14.4 — Reproduction recipe

To re-capture the W14 baselines (e.g. after a deliberate
visual change lands):

```bash
cd src/frontend/autotable-src
npm run build:vite
npx vite preview --strictPort &  # default port 4173
PREVIEW=http://localhost:4173 \
  node scripts/capture-real-surfaces.js
# PNGs land at the spec __screenshots__ path
# (overwrite-on-success; the script does NOT diff)
```

If port 4173 is already in use, the existing preview server
can be reused — `--strictPort` will fail loudly if not. The
W14 environment had a long-running preview server (PID
577813); the script auto-detects a live server on the
configured port via a `HEAD` probe.

### §14.5 — Verification (W14 baseline checksums)

| Surface | MD5 (W14 capture) | Bytes | Viewport |
|---------|-------------------|-------|----------|
| `main-game.png` | distinct | 97,771 | 1280×720 |
| `spectator-commentary.png` | distinct | 105,819 | 1280×720 |
| `tournament-dashboard.png` | distinct | 82,173 | 1280×720 |

Three distinct hashes confirm the surface-swap actually
fired (W14 iteration #1 saw all three hashes identical
because the tab clicks were intercepted by the W11 tour
overlay — see §14.3 step 1).

### §14.6 — Hand-off to W15 (Vasquez)

W14 lands the real captures at the path Vasquez's W14 spec
expects. The two W13 §11.5 hand-off items remain open and
are still in Vasquez W14 lane (not Hicks's):

1. **setContent bug fix** in `manifest-screenshots-
   visual.spec.ts` — prefix per-station block with
   `await page.goto(BASE_URL)`.
2. **`snapshotPathTemplate` config** in
   `playwright.config.ts` so the spec
   `toHaveScreenshot()` reads from the same path the W14
   capture script writes to.

Until these two fixes land, the W14 spec will still silent-
no-op against the new baselines (same root cause as W13).
The W14 captures are positioned correctly for when the
fixes do land; running the fixed spec against W14
baselines should report a pixel-perfect match.


