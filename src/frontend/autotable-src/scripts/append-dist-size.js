#!/usr/bin/env node
/* eslint-disable */
// Phase K Wave 7 — Append a chunk-size entry to `dist-size.json`.
//
// Owned by Hicks (frontend).  Triggered by `vite.config.ts` via its
// `closeBundle` hook (so every `npm run build:vite` updates the
// ledger automatically) and re-runnable standalone with:
//
//   node scripts/append-dist-size.js [--wave K7]
//
// Behaviour
// ---------
// • Scans the dist directory (`../autotable/`) for content-hashed
//   chunks emitted under the canonical `[name].[hash:8].js` pattern.
// • Groups them under stable chunk keys (stripping the hash).  The
//   keys match what `vite.config.ts:manualChunks` emits.
// • Reads the current wave name from $WAVE_NAME or `--wave` arg or
//   falls back to the existing `current` field.
// • If the wave already exists in `history`, updates that entry in
//   place (idempotent across local re-runs).  Otherwise appends a
//   new entry to the tail of `history` and bumps `current`.
//
// The file is committed; Vasquez's W7 spec reads it directly.

const fs = require('fs');
const path = require('path');

const DIST = path.resolve(__dirname, '..', '..', 'autotable');
const LEDGER = path.resolve(__dirname, '..', 'dist-size.json');

// Stable keys mirror parcel + vite chunk names.  The big/small
// three-renderer split was a parcel artefact (parcel emitted two
// renderer chunks).  Vite produces a single renderer chunk + one
// asset for the big chunk; we record whichever turns up.
const KEY_PATTERNS = [
  // Eager autotable-src lobby chunk.
  { key: 'autotable-src-eager', re: /^autotable-src\.[0-9a-f]+\.js$/ },
  // Game URL boot path.
  { key: 'game-bootstrap', re: /^game-bootstrap\.[0-9a-f]+\.js$/ },
  // Scene coordinators.
  { key: 'scene-shell',    re: /^scene-shell\.[0-9a-f]+\.js$/ },
  { key: 'scene-effects',  re: /^scene-effects\.[0-9a-f]+\.js$/ },
  // Three.js renderer.  Vite emits one chunk; parcel emitted two.
  { key: 'three-renderer-big',   re: /^three-renderer\.[0-9a-f]+\.js$/, max: true },
  { key: 'three-renderer-small', re: /^three-renderer\.[0-9a-f]+\.js$/, min: true },
  // Module addons.
  { key: 'GLTFLoader',     re: /^GLTFLoader\.[0-9a-f]+\.js$/ },
  // Phase K Wave 8 — manualChunks splits GLTFLoader into its own
  // chunk under the kebab-case `gltf-loader` name (matches the
  // chunkFileNames `[name].[hash:8].js` template).  Match both
  // legacy + W8 layout so the ledger keeps a stable key across
  // the bundler swap.
  { key: 'gltf-loader',    re: /^gltf-loader\.[0-9a-f]+\.js$/ },
  { key: 'stats-module',   re: /^stats(?:\.module)?\.[0-9a-f]+\.js$/ },
  // W6 UI surfaces.
  { key: 'commentary-panel',     re: /^commentary-panel\.[0-9a-f]+\.js$/ },
  { key: 'spectator-livestream', re: /^spectator-livestream\.[0-9a-f]+\.js$/ },
  // W7 vendored HLS.js chunk (split via manualChunks).
  { key: 'hls',            re: /^hls\.[0-9a-f]+\.js$/ },
  // Tournaments + lazy UI.
  { key: 'tournaments',    re: /^tournaments\.[0-9a-f]+\.js$/ },
  { key: 'chat',           re: /^chat\.[0-9a-f]+\.js$/ },
  { key: 'voice',          re: /^voice\.[0-9a-f]+\.js$/ },
  { key: 'audit',          re: /^audit\.[0-9a-f]+\.js$/ },
  { key: 'history',        re: /^history\.[0-9a-f]+\.js$/ },
  { key: 'tour',           re: /^tour\.[0-9a-f]+\.js$/ },
  // Phase K Wave 14 — new lazy overlays for the W14 action keywords.
  // Each is a sub-10 kB chunk loaded on demand by `action-router.ts`.
  { key: 'bracket-listing', re: /^bracket-listing\.[0-9a-f]+\.js$/ },
  { key: 'replays-listing', re: /^replays-listing\.[0-9a-f]+\.js$/ },
  { key: 'admin-cost',      re: /^admin-cost\.[0-9a-f]+\.js$/ },
  // Phase K Wave 15 — Phase L renderer hello-world spike chunk
  // (Hicks W15).  Carries the hand-rolled WebGL2 scaffold under
  // `src/renderer-webgl2/`.  Only ships when `?renderer=webgl2-hello`
  // is on the URL.  Baseline number is the W15 "hello world cost";
  // see `docs/phase-l-renderer-implementation.md`.
  { key: 'renderer-webgl2', re: /^renderer-webgl2\.[0-9a-f]+\.js$/ },
  // Phase K Wave 15 — Bishop W15 commentary cost forecast overlay
  // surfaced via `?action=cost-forecast&days=<n>`.  Admin-only.
  { key: 'admin-cost-forecast', re: /^admin-cost-forecast\.[0-9a-f]+\.js$/ },
  // Phase K Wave 16 — Hicks bundle-audit §3.1 + §3.5 split-outs.
  // `action-router` lazy-loads only when `?action=*` is on the
  // URL (was statically imported into autotable-src-eager in W15
  // and earlier).  `sentry-shim` is the wrapper module gated on
  // `import.meta.env.PROD || localStorage.SENTRY_DEBUG` — the
  // 342 KB SDK chunk (`sentry`) is still emitted under the same
  // name, but only the wrapper now reaches the eager graph on
  // the dev cold path.  The `sentry-shim` pattern picks the
  // SMALLEST sentry-named chunk (vite emits two: wrapper ~2 KB
  // and SDK ~342 KB) so the wave-over-wave shim cost is visible
  // even when the SDK chunk's hash flips.
  { key: 'action-router',  re: /^action-router\.[0-9a-f]+\.js$/ },
  { key: 'sentry-shim',    re: /^sentry\.[0-9a-f]+\.js$/, min: true },
  { key: 'sentry',         re: /^sentry\.[0-9a-f]+\.js$/, max: true },
  // Phase K Wave 17 — Hicks bundle-audit §3.2 split-outs.  Three
  // former eager modules (lobby-mounted at initLobby() time) are
  // now lazy-loaded behind their UI activation surface.  Each
  // entry's chunk is named after the source module so the manual-
  // chunks rollup config doesn't need touching.  See
  // `docs/frontend-bundle-audit.md §3.2`.
  { key: 'leaderboard',    re: /^leaderboard\.[0-9a-f]+\.js$/ },
  { key: 'settings-drawer', re: /^settings-drawer\.[0-9a-f]+\.js$/ },
  { key: 'profile-page',   re: /^profile-page\.[0-9a-f]+\.js$/ },
  // Phase K Wave 18 — Hicks bundle-audit §3.3 split-outs.
  //
  // `admin-panel` carries the W18 operator UI for Bishop's three
  // W17 CRUD surfaces (replay retention, JWKS rotation, SignalR
  // retention) as a single lazy chunk.  Loaded on
  // `?action=admin-panel`; W18 ceiling ≤ 40 KB.
  //
  // `pwa`, `reconnect`, and `spectator-follow` are the §3.3
  // bundle-audit lazifications: each was previously eager in
  // autotable-src-eager and now ships as a small lazy chunk gated
  // on a synchronous probe (`'serviceWorker' in navigator`,
  // `?rejoin=` on URL, `?seat=-1`/spectator-class on body
  // respectively).  Each chunk is well under 10 KB on disk.
  { key: 'admin-panel',     re: /^admin-panel\.[0-9a-f]+\.js$/ },
  { key: 'pwa',             re: /^pwa\.[0-9a-f]+\.js$/ },
  { key: 'reconnect',       re: /^reconnect\.[0-9a-f]+\.js$/ },
  { key: 'spectator-follow', re: /^spectator-follow\.[0-9a-f]+\.js$/ },
];

function parseArgs() {
  const argv = process.argv.slice(2);
  const out = { wave: process.env.WAVE_NAME || null };
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === '--wave' && argv[i + 1]) {
      out.wave = argv[i + 1];
      i += 1;
    }
  }
  return out;
}

function collectChunks() {
  if (!fs.existsSync(DIST)) {
    throw new Error(`dist not found: ${DIST}`);
  }
  const files = fs.readdirSync(DIST).filter(f => f.endsWith('.js'));
  const sized = files
    .map(f => ({ name: f, size: fs.statSync(path.join(DIST, f)).size }))
    .sort((a, b) => b.size - a.size);

  const chunks = {};
  for (const pat of KEY_PATTERNS) {
    const matches = sized.filter(s => pat.re.test(s.name));
    if (matches.length === 0) continue;
    let pick;
    if (pat.max === true && matches.length > 1) {
      // Take the largest (matches parcel's "big" sub-chunk).
      pick = matches[0]; // already sorted desc
    } else if (pat.min === true) {
      // Only meaningful when there are 2+; otherwise skip.
      if (matches.length < 2) continue;
      pick = matches[matches.length - 1];
    } else {
      pick = matches[0];
    }
    chunks[pat.key] = pick.size;
  }
  return chunks;
}

function main() {
  if (!fs.existsSync(LEDGER)) {
    console.error(`[dist-size] ledger missing: ${LEDGER}`);
    process.exit(1);
  }
  const ledger = JSON.parse(fs.readFileSync(LEDGER, 'utf8'));
  const args = parseArgs();
  const wave = args.wave || ledger.current || 'K?';

  const chunks = collectChunks();
  if (Object.keys(chunks).length === 0) {
    console.warn('[dist-size] no chunks matched any known pattern — skipping ledger update');
    return;
  }

  const entry = {
    wave,
    bundler: 'vite',
    recordedAt: new Date().toISOString(),
    chunks,
  };

  const idx = (ledger.history || []).findIndex(h => h.wave === wave);
  if (idx >= 0) {
    ledger.history[idx] = entry;
  } else {
    ledger.history = (ledger.history || []).concat([entry]);
  }
  ledger.current = wave;

  fs.writeFileSync(LEDGER, JSON.stringify(ledger, null, 2) + '\n');
  console.log(`[dist-size] recorded wave ${wave} — ${Object.keys(chunks).length} chunk(s)`);
  console.log(
    Object.entries(chunks)
      .sort((a, b) => b[1] - a[1])
      .map(([k, v]) => `  • ${k.padEnd(28)} ${v.toLocaleString()} B`)
      .join('\n')
  );
}

main();
