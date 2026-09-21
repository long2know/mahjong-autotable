# Wierzbowski — History

## Core Context

- **Project:** `mahjong-autotable`, a .NET 10 server-authoritative Changsha Mahjong backend with a Three.js/Vite frontend.
- **User:** Stephen Long.
- **Joined:** 2026-08-11 as WebSocket Security Engineer.
- **Initial focus:** Close a live-proven spectator exploit that allowed an unseated connection to force another player's discard.

## Working Agreements

- Never trust client-provided seat indexes or `?seat=` hints for authorization.
- Reject unauthorized actions explicitly before any state/version mutation.
- Preserve server-internal bot actions and relay-variant compatibility.
- Require independent Frost re-review before release.

## 2026-09-14 — Exact published3.7 Docker consumer binding;8953 reconciliation HOLD

Completed source-only protocol_v9 integration against immutable published shared3.7 source18f4fdd8c9cc328e52d016798248b421a67edfa673861f589d7657440b3a226f (22 files), captured from verified archive87a09b300fe4a6e034810250ac762f8b80aa227e1314e1a9416d81b367ad02c7. Publication handoff: session-files/qualification/2026-09-12/apone-shared-http-revision/source-only-publication-v37/handoff.json e7d8031c39d722abe5cfcb4dff3aee55c48c791e0144ff0904377a373dd00247. Consumer manifest: session-files/qualification/2026-09-12/wierzbowski-protocol-integration/shared37-consumer-review-manifest.json a4f899236c302940cd63ad7d675b453f002ead962864992bce2291efd950c863; source0fb1cec5c2d3d5296f14a02a53f96a25d689ba0c7c56934b22e3e179487683ca. All253 offline controls pass; all245 previous controls/665 assertions preserved. Model/policy/recovery/transport are byte-identical to v8, including accepted NEW/.4 semantics. Exact source guard rejects moving/foreign helper before harness or activation.

Critical target constraint, not a host blocker: approved3.7 still fixes8950 in core URLs, pin/HTTP/socket closed schemas, Docker port mapping, direct HTTP collector, and independent WS URL binding. Actual8953 schema/URL inputs reject offline; no proof weakened or constants overridden. Exact report: evidence/shared37-target8953-constraint.json under the same integration namespace, SHA2563cb3ba4f8aa1094a0555df00596e9e9131ce80bca80d1ad1c3c0abc3f027c317. Asked Apone b309/coordinator for surgical reviewed8953 helper/canonical reconciliation. Observed moving catalog/root still advertised unapproved native3.8; it was not imported. Use the explicit immutable3.7 publication, not a moving catalog or delayed conversational reply, to establish available source-only refs.

Optional native work remains paused/unused, with prior artifacts unchanged and never relabeled as Docker. v7/v8 complete source/evidence namespaces preserved. No Docker/host/browser/HTTP/WS/game operations, app/shared-source edits, or Git mutation. APPROVED_SHARED_DIGEST remains None; no target admission, finalD17/image/endpoint proof, pilots, or counting authority.0/120.
