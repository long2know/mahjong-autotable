# Apone — Phase K Wave 8 decision memo

> Author: Apone (DevOps)
> Date: 2026-07-09
> Branch: `stlong/phase-k-wave-8-bringup`

## Mission

Wave 8 carries forward the W7 release-distribution + edge
surface work by **production-hardening** the surfaces that W7
brought online (staging-env cutover for the W7 edge module, CI
parity on the W7 pre-commit hooks, reconciliation of a W7 doc
path drift) and adding the next-tier release surfaces (mobile
**Production** track after W7's External, Helm canary deploys
via Argo Rollouts, DR rehearsal automation that codifies the
W6 manual runbook).

Seven DevOps-lane deliverables:

1. **Staging edge cutover** — `infra/terraform/envs/staging/`
   instantiating the W7 `modules/edge/` against staging EKS,
   with a documented cutover runbook + smoke test + rollback.
2. **CI pre-commit enforcement** — workflow that runs
   `pre-commit run --all-files` so a developer's `--no-verify`
   bypass no longer reaches `main`.
3. **Kyverno-enforce-patch canonical-path reconciliation** —
   doc fix + presence-check guard so the wrong-path file
   cannot reappear silently.
4. **Mobile Production track promotion** — env-gated, tag-
   driven `mobile-prod-v*.*.*` workflow promoting the most-
   recent External Testing build to App Store + Play
   Production.
5. **Helm canary via Argo Rollouts** — umbrella-level
   `Rollout` + `AnalysisTemplate` template, staging-only,
   5%→20%→50%→100% with Prometheus analysis.
6. **DR rehearsal automation workflow** — quarterly
   `workflow_dispatch` that walks §4.1–§4.4 of the W6 runbook
   end-to-end + writes a results report.
7. **CHANGELOG 0.17.0 + July 2026 retro + this memo.**

## Decisions

### 1. Staging WAF defaults to `COUNT`, not `BLOCK`

**Decision.** Add `waf_managed_rules_action` variable to the
staging env (default `COUNT`); prod stays `BLOCK`. The
managed rule groups sometimes fire on legitimate staging
traffic (Vasquez's test fixtures use synthesised payloads
that can trip the SQLi rule). `COUNT` records the would-be
block to CloudWatch + the S3 WAF log bucket without serving
a 403.

**Alternatives considered.**

* **`BLOCK` mode in staging, accept the false-positive
  outages.** Rejected — false-positive blocks would
  manifest as test-fixture failures that look like
  application bugs, not WAF bugs. The signal-to-noise on
  staging would tank.
* **Disable the managed rule groups in staging.** Rejected
  — we LOSE the staging soak entirely; the prod flip would
  have to rely on a synthetic load test instead of real
  traffic exposure.

**Trade-off.** Staging is technically less secure than prod
for the soak quarter. Accepted because (a) staging is
synthetic-only traffic, (b) the WAF events are still captured
for review, (c) the W11 prod flip criteria requires zero
unexpected COUNT events on staging.

### 2. Argo Rollouts over Flagger for canary

**Decision.** Use Argo Rollouts as the canary engine. The
`Rollout` CRD is a drop-in for `Deployment` (same
`spec.template`, same selector model); no service-mesh
dependency for replica-based canary. Vendor alignment with
the future Argo CD adoption (W10).

**Alternatives considered.**

* **Flagger.** Better out-of-the-box for traffic-split
  canary (drives Istio / Linkerd / nginx-canary natively),
  BUT requires an existing mesh OR sidecar injector. We
  don't run a mesh.
* **Roll-your-own with two Deployments + a Service selector
  flip.** Rejected — fragile under partial rollout; no
  built-in analysis primitives; the operator has to manage
  the progression by hand.

### 3. Co-existence guard fails closed; staging escape hatch only

**Decision.** The canary template `{{ fail }}`s if both
`api.enabled` and `canary.enabled` are true, UNLESS
`canary.coexistWithDeployment` is explicitly set. The escape
is for the cut-over window where the operator wants to soak
the Rollout alongside the existing Deployment.

**Alternatives considered.**

* **Allow co-existence by default; warn in docs.** Rejected
  — silent overlap means two replicasets fighting over the
  same pod-template selector and flapping replicas. An
  obvious `{{ fail }}` at template time is better than a
  subtle production incident.
* **Disable the escape entirely; require the operator to
  set `api.enabled=false` before enabling canary.**
  Rejected — the staging cutover wants a soak window where
  both run simultaneously to observe parity. The escape
  unlocks that without making it the default.

### 4. Mobile Production tag space disjoint from Internal

**Decision.** Production uses `mobile-prod-v*.*.*` tags
(disjoint from Internal `mobile-v*.*.*`). The workflow's tag
validation rejects a `mobile-prod-v*` unless a matching
`mobile-v*` (Internal) tag exists for the same semver.

**Alternatives considered.**

* **Reuse `mobile-v*` for all three surfaces (Internal /
  External / Production), differentiate by workflow
  dispatch.** Rejected — Git tags should be a permanent
  record of "this commit shipped to this surface". One tag
  per surface is the cleanest audit trail.
* **`mobile-v*` for Internal; `mobile-external-v*` +
  `mobile-prod-v*` for the others.** Rejected — too verbose;
  one cross-check (Internal exists before Production) is
  enough to enforce the promotion order.

### 5. DR rehearsal workflow generates report, does not commit it

**Decision.** The DR rehearsal workflow generates
`docs/dr-rehearsal-results-YYYY-Q#.md` + uploads it as a
workflow artefact + posts it to the step summary. It does
NOT push to the repo. The operator commits the result file
after the rehearsal.

**Alternatives considered.**

* **Push from the workflow with a write-scoped GITHUB_TOKEN.**
  Rejected — would expand the workflow's OIDC blast radius
  from `contents: read` to `contents: write`. For a once-a-
  quarter operation, the friction-vs-blast-radius trade
  favours read-only.
* **A separate `dr-results-bot` write-scoped OIDC role.**
  Rejected — doubles the OIDC role surface for a quarterly
  operation. The operator-commit path is fine.

### 6. Path-confusion guard codifies presence-check, not
       just regex-check

**Decision.** The W7 invariant script checks signer-identity
regex equality across N tracked file paths. W8 adds a
`PATH_CONFUSION_GUARDS` tuple of `(canonical, wrong, reason)`
triples + a `_check_path_confusion_guards()` function that
fails the script if the WRONG-path file exists at all
(regardless of its contents). The W7 mode-of-failure was a
wrong-path file that the regex extractor never looked at; the
W8 guard closes that mode.

**Alternatives considered.**

* **Doc fix only.** Rejected — fixes the W8 instance but
  any future doc drift re-opens the hole.
* **Make the regex extractor scan EVERY YAML file under
  `infra/k8s/`.** Rejected — too broad; would generate
  false positives on unrelated CRDs that happen to reference
  the signer regex (e.g., other kyverno policies).

### 7. CI pre-commit gate runs the SAME hooks as local, not a
       superset

**Decision.** The CI workflow runs `pre-commit run --all-
files` using the same `.pre-commit-config.yaml` as the local
install. No CI-only hooks; no local-only hooks. A divergence
between local and CI is a configuration bug.

**Alternatives considered.**

* **Add CI-only slow hooks (e.g., a full `terraform plan` on
  every PR).** Rejected — local + CI should converge. Slow
  hooks belong in their own CI step, not in the pre-commit
  surface.
* **Make CI advisory (don't fail the build).** Rejected —
  the whole point of W8 CI parity is that a `--no-verify`
  bypass no longer reaches `main`. Advisory mode defeats
  the gate.

## Cross-references

* `CHANGELOG.md` `[0.17.0]` — Wave 8 entry.
* `docs/retro-2026-07.md` — July 2026 retro (long-form on
  the §3 lessons learned).
* `.squad/decisions/inbox/apone-phase-k-wave-7.md` — W7
  decisions (the kyverno-path bug surfaced in W7, fixed in
  W8).
* `.squad/agents/apone/history.md` — W8 history entry.
