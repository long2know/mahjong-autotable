# Mahjong Autotable — documentation index

This directory holds every operator-facing document for running and
maintaining the Mahjong Autotable. Read **top to bottom** if you're
new; reach for the topical index below if you've been here before.

If you ship code that changes the operational surface area (a new
env var, a new endpoint, a new deploy step), update the relevant
doc in the same PR and bump the entry in this index.

---

## Start here

| Doc                                                          | When you read it                                                                 |
| ------------------------------------------------------------ | -------------------------------------------------------------------------------- |
| [`architecture.md`](./architecture.md)                       | First — get the 5-minute mental model of how the pieces fit together              |
| [`docker.md`](./docker.md)                                   | "I want to `docker run` the thing on my Linux box." Single-image quickstart       |
| [`deployment.md`](./deployment.md)                           | Single-Docker-image deployment notes (Stephen's canonical target)                |
| [`production-deployment-runbook.md`](./production-deployment-runbook.md) | The end-to-end production runbook — pre-flight, deploy, rollback, incidents |
| [`known-limitations.md`](./known-limitations.md)             | What V1 explicitly doesn't do (so you don't file a bug)                          |

## Deployment surfaces

| Topic                              | Doc                                                              |
| ---------------------------------- | ---------------------------------------------------------------- |
| Single-container Docker            | [`docker.md`](./docker.md)                                       |
| systemd unit (non-k8s deploys)     | [`systemd.md`](./systemd.md)                                     |
| Kubernetes manifests + overlays    | [`kubernetes.md`](./kubernetes.md)                               |
| Reverse-proxy (nginx, Traefik)     | [`reverse-proxy.md`](./reverse-proxy.md)                         |
| Cloudflare edge config             | [`cloudflare.md`](./cloudflare.md)                               |
| Database providers + migrations    | [`database-providers.md`](./database-providers.md)               |
| Backup + restore                   | [`backup-restore.md`](./backup-restore.md)                       |

## Security + supply chain

| Topic                                  | Doc                                                          |
| -------------------------------------- | ------------------------------------------------------------ |
| Secret inventory (env vars)            | [`secrets.md`](./secrets.md)                                 |
| Secret management + rotation           | [`secret-management.md`](./secret-management.md)             |
| SBOM + Trivy CVE gate                  | [`sbom.md`](./sbom.md)                                       |
| CSP + security headers (in code)       | `src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs` |

## Observability + runtime ops

| Topic                              | Doc                                                              |
| ---------------------------------- | ---------------------------------------------------------------- |
| Prometheus + JSON structured logs  | [`observability.md`](./observability.md)                         |
| Sentry error reporting             | [`sentry.md`](./sentry.md)                                       |
| Log rotation policy                | [`log-rotation.md`](./log-rotation.md)                           |
| CI pipeline (build / publish)      | [`ci.md`](./ci.md)                                               |
| Load test harness + Wave-10 results| [`load-test-results.md`](./load-test-results.md)                 |

## Game rules + spec material

| Topic                              | Doc                                                              |
| ---------------------------------- | ---------------------------------------------------------------- |
| Rule presets (Changsha + others)   | [`rules/`](./rules/)                                             |
| Architectural specs + audits       | [`specs/`](./specs/), [`audits/`](./audits/)                     |

---

## Conventions

- **Operator-tunable config** lives in `appsettings.json` /
  `appsettings.Production.json` and is overridable via env vars
  using ASP.NET Core's standard `Section__Subsection__Key` mapping
  (double underscore). Every supported key is documented in the
  relevant topic doc.
- **Sensitive config** (`Auth__Google__ClientSecret`,
  `ConnectionStrings__Postgres`, etc.) must come from a secret
  store (k8s `Secret`, sealed-secrets, your-favourite-vault).
  Never commit plaintext.
- **Doc PRs.** When a doc changes the contract (a new env var, a
  changed default), bump the corresponding entry in
  [`known-limitations.md`](./known-limitations.md) if the change
  papers over a previous limitation.

## Wave-10 changes (Phase J final pass)

- New: [`production-deployment-runbook.md`](./production-deployment-runbook.md) — the end-to-end runbook.
- New: [`load-test-results.md`](./load-test-results.md) — load-test harness output + SLO assessment.
- Updated: [`sbom.md`](./sbom.md) — multi-arch image notes (Wave 10).
- Updated: this README — first index emitted for the docs tree.
