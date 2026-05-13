# Autotable static bundle (generated)

This directory is the **Parcel build output** of `src/frontend/autotable-src/`.
The .NET backend serves it verbatim at `/autotable/`.

**Do not edit files in this folder.** Any change here will be clobbered on
the next `parcel build`. Edit the TypeScript / HTML / CSS sources under
`../autotable-src/` and rebuild.

## Rebuilding

```bash
cd ../autotable-src
npm install        # one-time
npx parcel build index.html about.html --public-url . --no-source-maps \
    --cache-dir .cache/build/ --dist-dir ../autotable
```

The same invocation runs continuously under the `autotable: watch` VS Code
task (see `.vscode/tasks.json`) — F5 from VS Code starts the backend and the
Parcel watcher together via the `F5 Full Stack (Backend + Autotable)` compound.

## Provenance

Vendored from `https://github.com/pwmarcz/autotable` master at the upstream
SHA recorded in `../autotable-src/UPSTREAM_SHA`. Upstream's MIT code license
lives in `../autotable-src/COPYING`; image and sound assets retain their
original CC BY-NC-SA / CC0 licenses as documented in `../autotable-src/about.html`.

Local-only modifications applied to upstream sources for our build:
- `index.html`: `perspective` and `tile-labels` checkboxes default to `checked`
  (better visual baseline; matches the previously shipped static bundle).
- `index.html` + `about.html`: Google Analytics tracking block (pwmarcz.pl
  property) removed.
