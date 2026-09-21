# Apone — canonical apt failure diagnosed; fallback already running

## Availability correction
C01 was launched **September 9, 2026 at16:07:22 PDT**, not still assembling. Fresh checks at approximately17:03–17:06 PDT confirm healthy/SQLite connected, unchanged container/image/DLL/entry and222 matching application/asset files. URL `http://127.0.0.1:18190/autotable/`; container `mahjong-20260909-e2fb89d1-c01` / `29c572461acc8acf893000b79bfa5770acf8f9754c70f384e788d6e01c4fb6ec`, initPID1500131. Image `sha256:113c10b315f41483bdd0e1b0879ffdee08399e4f8b2dd91fc37671284ba10461`; DLL `836821f3819d881fc1ee7bc1a480f98b16adf32e7b37e21449b70d04f1b4cec5`. All gameplay lanes may use this sealed current-code local fallback under Hudson's browser schedule; no release-signing or further assembly wait.

## Actual local failure, not bad Ubuntu signatures
The canonical apt update error was reproduced in a uniquely named disposable container from the exact cached ASP.NET base, with unchanged Ubuntu trust keyring and signature verification enabled. It again exits100 with high-level invalid-signature/repository-not-signed messages. Debug output reports verifier exit1 with empty Good/Bad/NoPubKey fields.

Offline comparison of the SAME retained InRelease files shows:
- Root direct gpgv: **4/4 VALID signatures** (noble, updates, backports, security), unchanged shipped keyring.
- Root apt-key wrapper on noble: PASS.
- `_apt` uid42 direct gpgv on noble: PASS.
- `_apt` apt-key wrapper on that same file: **FAIL1 at chmod of its newly created GPG home: Operation not permitted**.

Working directories and even cached image keyring files present as ubuntu:ubuntu with777 modes; `_apt` cannot chmod its new directory. The real Docker root is `/data/docker.service`, driver **vfs**, backing filesystem **fuseblk**. This local ownership/permission behavior blocks apt's wrapper before signature verification; the high-level apt message is misleading. This corrects the earlier generic signature/mirror characterization; no changed trust roots or invalid archive signature was demonstrated.

## Resource checks
At00:03:44Z September10 (17:03:44 PDT September9): Docker/data filesystem502G free and only2% inodes used; root filesystem24G free;42Gi available memory. No current disk/inode/RAM exhaustion was indicated. Checks used the ACTUAL Docker root, not assumed /var/lib/docker.

## Safety / disposition
Only owned `--rm` diagnostic containers and the isolated Apone evidence directory were used. Offline comparisons used `--network none`. No package installation, allow-unauthenticated/trusted=yes, signature bypass, changed keyring, sandbox bypass, Docker-host configuration change, resource prune, shared container/data access, C01 rebuild or restart. Repository Dockerfile/workflows/package/config source remain unchanged. Canonical packaging remains locally blocked; its diagnosis does not block C01 gameplay acceptance. Existing UI lint remains baseline-red17 errors/10 warnings, with no unrelated cleanup.

## Persistent evidence
`session-files/completion-proof/2026-09-09/apone/canonical-apt-diagnostic-1700/diagnosis.json` SHA256 `00593f56901247c5b4d11625ac5402fc25d449fc1cf10c0614ef32cc687b471b`.
Sibling `evidence.sha256` verified all12 retained files, including signed downloads, cached base image identity, detailed gpgv/root/_apt logs and resource record. Original C01 manifest/proof records remain immutable. Full candidate coordination remains `sessions/2026-09-09-apone-candidate.md`.
