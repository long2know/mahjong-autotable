# Apone — Phase K Wave 10 memo

**Branch:** `stlong/phase-k-wave-10-bringup`
**Date:** 2026-08-09
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Squad-git-lock path cutover (`/tmp/` → `.work/`),
Redis ElastiCache Terraform module (Bishop W10 unblocker),
Argo Rollouts cluster install runbook (W9 hand-off), JWT SSM
runbook §3 quarterly rotation walkthrough, container-scan-
remediation workflow, prod-health-check workflow,
CHANGELOG bump to **0.19.0** + retro 2026-08.

---

## Decisions

### D1 — Squad git-lock path cutover from `/tmp/` to `.work/`

**Why:** the W9 §3.6 cutover plan called out three problems with
`/tmp/squad-git-lock`:

  (a) ephemeral wipe on reboot / inactivity — second agent
      attaches to a brand-new lock instead of the existing one;
  (b) world-writable shared with non-squad processes that may
      hold unrelated flocks against the file;
  (c) several agent runtimes hard-prohibit writes under `/tmp/`
      (Scribe + Vasquez noted in their W8 retros).

W9 staged the infrastructure (`.work/.gitkeep` + `.gitignore`
`.work/*` + `!.work/.gitkeep`) but kept the live path on `/tmp/`
to avoid mid-wave mutex split. **W10 flips.**

**What changed:**

* `docs/agent-handoff-protocol.md` §3.6 — heading rewritten to
  "**W10 cutover COMPLETE**"; bullets past-tensed.
* `docs/agent-handoff-protocol.md` §3.7 — canonical commit
  pattern snippet flipped `9>/tmp/squad-git-lock` →
  `9>.work/squad-git-lock`.
* `.squad/decisions.md` — EDIT(W10) blockquote notes at the
  top of the W6 / W7 / W8 wave summaries pointing readers at
  the new path. Original wave content unchanged (it remains
  historically accurate for that wave).
* Historical `.squad/agents/*/history.md` files INTENTIONALLY
  exempt per §3.6's retro-exemption rule.

**Carry-forward invariant:** every cutover-plan section MUST
include a `git grep <old-path>` step before declaring complete.
W9 didn't, and §3.7's snippet was stale W10 cycle 0.

---

### D2 — Redis ElastiCache module (`infra/terraform/modules/redis/`)

**Why:** Bishop's W10 `RedisIdempotencyStore` runtime needs an
external Redis with auth + TLS. Existing modules cover edge
(WAF + CloudFront), DR replication, GitHub OIDC — no Redis.

**Module shape:**

* Single-shard `aws_elasticache_replication_group` (canonical
  Redis primary + reader endpoint + N-replicas topology).
* `replica_count` + `multi_az_enabled` configurable; staging
  shape is 0 replicas + multi-AZ off (cheap).
* Custom `aws_elasticache_parameter_group` with
  `maxmemory-policy=allkeys-lru` — Bishop's store is a CACHE,
  not a primary; `volatile-lru` would silently drop
  idempotency keys with no TTL.
* Optional `random_password`-generated auth-token with
  `lifecycle.ignore_changes = [auth_token]` — quarterly
  rotation via SSM doesn't fight Terraform.
* TLS in transit (`transit_encryption_enabled=true`) + at-rest
  (`at_rest_encryption_enabled=true`).
* Security group with VPC-CIDR ingress + opt-in
  `allowed_security_group_ids` list.

**Sensitive outputs (kept out of plaintext state):**

* `redis_connection_string` (`redis://:<auth>@<endpoint>:<port>/0`)
* `redis_auth_token`

**Wired into staging env stack** at `cache.t4g.micro` with
0 replicas. Wave tag bumped W8 → W10 (staging stack was last
touched W8). Prod stack is **W11 hand-off**.

---

### D3 — Argo Rollouts cluster install runbook (W9 hand-off picked up)

**Why:** W9 shipped the chart-side canary surface
(`helm/mahjong/templates/canary-deployment.yaml` + 3 W9
AnalysisTemplates). The cluster install (CRDs, controller,
dashboard) was W9 → W10 hand-off. Without the controller, the
canary surface doesn't reconcile.

**Pins:**

* Helm chart `argo-rollouts` **2.37.7** (controller image
  `quay.io/argoproj/argo-rollouts:v1.7.2`).
* `kubectl argo rollouts` CLI plugin **v1.7.2** — kept in
  lock-step with the controller image.

**Dashboard access decision:** port-forward only at W10. NO
public ingress until W11+ ships an auth-aware OIDC SSO proxy
(decided with Vasquez 2026-08-05 inbox memo). Runbook
explicitly DOCUMENTS the constraint rather than leaving the
dashboard accidentally on `LoadBalancer` Service.

---

### D4 — JWT SSM runbook §3 cadence: 180d → 90d (quarterly)

**Why:** W10 squad-wide secret-management cadence review
settled on **90d** for all secrets (JWT signing keys, OAuth
client secrets, Redis auth-tokens, DB passwords). Aligning
JWT with the rest of the squad reduces the cognitive overhead
of "which secret rotates when".

**What §3 now contains:**

* §3.1 quarterly calendar (Q1/Q2/Q3/Q4 last-day-of-quarter
  cadence + named owner pattern).
* §3.2 full `aws ssm put-parameter` walkthrough with
  pre-flight + post-flight JWKS validation
  (`curl /.well-known/jwks.json | jq -r '.keys[].kid'`).
* §3.3 quarterly hand-off checklist.
* §3.4 quarterly rollback procedure (promote previous → active
  + archive → previous; the just-minted key is **intentionally
  discarded** — a key clients have rejected is a key we never
  want to revisit).

The cognitive load per rotation is LOWER under the new walk-
through even though cadence doubled.

**First quarterly rotation under the new cadence:** end of
September 2026 (Q3 2026). The W11 on-call SRE inherits.

---

### D5 — `container-scan-remediation.yml` workflow

**Why:** the W6 `container-scan.yml` is the merge GATE. Its
output (the `container-scan-findings-<run>` artefact) is the
paper trail. Until W10, turning that paper trail into ACTION
(open a GitHub issue, prepare a base-image bump suggestion)
was manual SRE work — typically 4-6 h median from CVE landing
to issue creation.

**What the workflow does:**

* Triggers: nightly cron @ 05:00 UTC (1 h after W6's 04:00
  UTC cron) + `workflow_run` on `container-scan` failure +
  `workflow_dispatch`.
* Downloads the W6 findings artefact via
  `github.rest.actions.downloadArtifact`.
* Python filter to HIGH+CRITICAL (or CRITICAL-only on operator
  override).
* Composes issue body with CVE table + base-image bump
  heuristic (counts CVE hits per target; if one target
  dominates, suggests a `FROM` bump) + W6 allowlist pointer.
* De-dups against existing open issues by title prefix
  `[container-scan] CVE remediation` + labels
  `security,automated`. Updates existing on hit; creates new
  on miss.

**Decision NOT to open PRs:** the squad reviews CVE
remediation before bumping (alpine v3.19 → v3.20 occasionally
ships breaking-glibc changes). The suggested bump in the
issue body is a HINT, not an auto-PR.

**Documented in:** `docs/secrets-scanning.md` §4 (NEW —
W10). Two-scanner taxonomy table + triage tree + base-image
bump walkthrough + allowlist as last resort + close-the-loop
matrix.

---

### D6 — `prod-health-check.yml` workflow

**Why:** the W6 Sentry + W7 Prometheus stack is **reactive** —
it fires after a request fails. W10 adds a **synthetic** probe
that runs every 5 minutes from GitHub-hosted runners and hits
the live edge. Synthetic probes catch failure modes the
reactive stack misses (e.g. JWKS publication breakage when no
client has yet tried the affected `kid`; metrics endpoint
returning 200 with an empty body after a partial restart).

**Endpoints probed:**

| Path                         | Assertion                                |
| ---------------------------- | ---------------------------------------- |
| `/healthz`                   | HTTP 200 + body `"status":"ok"`          |
| `/readyz`                    | HTTP 200 + latency < 1500 ms (default)   |
| `/metrics`                   | HTTP 200 + body size > 1024 B            |
| `/.well-known/jwks.json`     | HTTP 200 + `.keys | length >= 3`         |

**Cooldown design (3-strike open / 2-strike close):** a
single 5-minute probe failure marks the run as failed but
does NOT open an issue. Three CONSECUTIVE failures (~15 min
sustained outage) open the incident issue. Two CONSECUTIVE
clean runs close it. While the incident is open, additional
failures UPDATE the issue body rather than opening duplicates.

**State machine:** carried in a hidden HTML comment
(`<!-- prod-health-check:state strikes=N recoveries=M -->`)
embedded in the issue body. Machine-parseable + survives
manual triage edits + invisible in the rendered issue.

**Slack:** optional via `SLACK_WEBHOOK_URL` secret. Best-
effort (`curl --max-time 10 ... || echo "::warning::..."`).
Slack outage NEVER fails the workflow.

**Decision:** the probe is a BACKSTOP, NOT a pager
replacement. Sentry + Prometheus alerts fire FASTER for
app-side issues. The probe catches the cases the reactive
stack misses, and it gives the on-call a written record
when the reactive stack is itself degraded (Sentry sampling
drop, Prometheus scrape failure).

**Documented in:** `docs/production-deployment-runbook.md`
§8 (NEW — W10). Existing §8 Companion docs renumbered §9.

---

### D7 — CHANGELOG bump to 0.19.0 (NOT 0.18.0 per the W10 task prompt)

**Why:** the W10 task prompt said "CHANGELOG bump to 0.18.0".
**That version is already used by W9** (`[0.18.0] — Phase K
Wave 9 — 2026-07-23` block in the repo). Versions on this
project track the wave count (W1+W2=0.11.0, W3=0.12.0, …,
W9=0.18.0). W10 must be **0.19.0**.

**What changed:**

* `[Unreleased]` reset to "Wave 10 in flight".
* New `## [0.19.0] — Phase K Wave 10 — 2026-08-09 (PR pending)`
  block with theme paragraph + Added / Changed / Hand-offs-
  to-Wave-11 sections.
* W9 `[0.18.0]` annotation flipped `(PR pending)` →
  `(PR #55)` now that the W9 PR has merged.

**Carry-forward:** the version-arithmetic check should be
part of every changelog-bump pattern. Document the previous
version + the new version explicitly in the wave-decision
memo (D7 of this memo IS that documentation).

---

## Outstanding follow-ups (Wave 11+)

* Prod Redis stack instantiation (multi-AZ + ≥1 replica + KMS
  rotation review).
* Argo Rollouts dashboard ingress with auth-aware OIDC SSO
  proxy (Vasquez-led).
* Terraform CLI pin bump v1.9.8 → v1.15.x + re-validate all
  modules.
* First quarterly JWT rotation under the new 90d cadence:
  end of September 2026 (Q3 2026).
* Quarterly DR rehearsal: end of September 2026 (Q3 2026).
* Container-scan-remediation issue body size monitoring —
  W12 consider splitting by severity tier if body grows
  past ~50 KB.
* Synthetic edge probe (per-region matrix) — W12 candidate
  to extend `prod-health-check.yml`.

---

## Verification before commit

* `terraform fmt -recursive -check` — clean (modules/redis +
  envs/staging).
* `terraform init -backend=false && terraform validate` —
  clean (modules/redis: "Success! The configuration is
  valid."; envs/staging: clean with the new module wired).
* `actionlint v1.7.7` — clean on both new workflows.
* No `src/**` / `tests/**` / mobile changes — pure DevOps +
  docs + infra.
* `git status --short` pre-push check — only Apone-lane paths
  staged.

---

## Identity + git mechanics (W10 cutover — first use)

```bash
( flock -w 120 9 || exit 1
  git fetch origin stlong/phase-k-wave-10-bringup
  git rebase origin/stlong/phase-k-wave-10-bringup
  git status --short | head -20
  git add <enumerate apone-lane paths>
  git -c user.name="Apone (DevOps)" -c user.email="apone@squad.mahjong" \
      commit -m "Phase K Wave 10 — Apone DevOps lane …"
  git log -1 --format='%an <%ae>'
  git push origin stlong/phase-k-wave-10-bringup
) 9>.work/squad-git-lock
```

Per-invocation identity (no `git config` calls); flock at the
NEW W10 path; rebase INSIDE the flock; selective `git add`;
post-commit identity verification before push.
