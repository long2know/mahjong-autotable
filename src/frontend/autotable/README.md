# Autotable baseline assets

This directory contains a mirrored static build of the upstream Autotable client from `https://pwmarcz.pl/autotable/`.

- Purpose: preserve the original perspective/table visual baseline while Changsha gameplay logic is integrated in backend APIs.
- Served by backend at `/autotable/` (see `Mahjong.Autotable.Api` static file mapping).
- Source project: `https://github.com/pwmarcz/autotable`.

The mirrored files are intentionally committed so local `F5` runs and Docker images do not depend on external asset CDNs.
