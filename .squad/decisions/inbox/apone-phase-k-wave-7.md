# Apone — Phase K Wave 7 decision memo

> Author: Apone (DevOps)
> Date: 2026-06-11
> Branch: `stlong/phase-k-wave-7-bringup`

## Mission

Wave 7 carries forward the W6 platform-bringup work by shipping
the **operator-driven release-distribution surfaces** + the
**public-facing edge** + the **automated signer-identity
invariant** + the **RS256 JWT SSM-side bringup** (Bishop's
code-side RS256 cutover lands in the W7 backend lane and
consumes the binding shipped here).

Six DevOps-lane deliverables:

1. **Helm chart-of-charts** (`helm/mahjong/`) — umbrella + three
   subcharts running parallel to the existing Kustomize tree.
2. **Edge Terraform module** (`infra/terraform/modules/edge/`) —
   Route53 + ACM + WAFv2 + opt-in CloudFront, with the
   us-east-1 provider-alias convention from W6 `dr-replication/`.
3. **GHCR→ECR mirror workflow** — signature-preserving mirror
   using `crane copy` + `cosign copy`.
4. **Mobile External Testing workflow** — operator-driven
   `workflow_dispatch` promotion of the most-recent Internal
   build to TestFlight External Groups + Play Closed Testing.
5. **Six-file signer-identity invariant pre-commit hook** —
   automates the four-file W6 lock-step + adds two new tracked
   surfaces (slsa-provenance workflow + slsa-provenance doc).
6. **RS256 JWT SSM provisioning** — separate ExternalSecret on
   prod + staging overlays with `Auth__JwtRsaKeys__N` binding.

## Decisions

### 1. Helm umbrella `alias:` on every dependency — not optional

**Decision.** Add `alias:` to every dependency in
`helm/mahjong/Chart.yaml`. The umbrella `values.yaml` uses
short keys (`api`, `coturn`, `postgresSidecar`); the subcharts
are canonically named `mahjong-api`, `mahjong-coturn`,
`mahjong-postgres-sidecar`. Without the aliases, Helm routes
umbrella values by chart NAME, and overrides like
`api.persistence.enabled: false` are silently ignored.

**Alternatives considered.**

* **Rename subcharts to match the umbrella short keys.**
  Rejected — the subcharts' canonical `name:` matches their
  intended publishable identity (a future `helm push` to a
  chart registry SHOULD publish `mahjong-api`, not `api`,
  because `api` collides with countless other charts).
* **Keep umbrella keys long (`mahjong-api.persistence.enabled`).**
  Rejected — readability hit on every operator who reads
  values files, and the prod/staging overrides become noisy.
* **Don't use a chart-of-charts; ship three independent
  charts.** Rejected — operators have to remember the install
  order and manage three release lines; the helm semantics
  for hooks (`pre-upgrade`) are per-release.

The W7 initial bringup hit this trap once (prod render
produced PVCs despite the override); the fix was the `alias:`
wiring. The trap is now documented in `docs/helm-charts.md`
§1.1 as a Helm-side surprise for future W8+ partner-chart
consumers.

### 2. Helm runs PARALLEL to Kustomize, not as a replacement

**Decision.** Both `helm/mahjong/` and `infra/k8s/base/` +
`infra/k8s/overlays/` ship in this repo. The W7 acceptance
gate is **parity** (both paths render equivalent objects); the
CI deploy path stays on Kustomize, helm is the operator-driven
point-install + partner-deploy surface.

**Alternatives considered.**

* **Migrate everything to helm; drop Kustomize.** Rejected —
  CI deploy path is hardened against the Kustomize tree
  (`docs/production-deployment-runbook.md` references
  `kubectl apply -k`); migrating CI is a larger scope than W7
  warrants. Re-evaluate at W9.
* **Migrate everything to Kustomize; drop helm.** Rejected —
  operators who consume the chart externally (W8 partner
  scenario) expect helm; releases-to-customers ecosystem is
  helm-dominant.
* **Keep Kustomize but generate the helm chart from it.**
  Rejected — `kustomize-to-helm` transformation tools are
  immature; W7 hand-authoring is one-time work, ongoing
  parity-checking is a per-wave checklist item (CI parity
  gate is W8).

Documented in `docs/helm-charts.md` §3 with the decision matrix.

### 3. Edge module — `aws.us_east_1` provider alias is non-negotiable

**Decision.** The edge module declares `configuration_aliases =
[aws.us_east_1]` so callers must pass an explicitly-aliased
provider. CloudFront ACM certificates MUST live in us-east-1
regardless of the primary AWS region (AWS hard constraint —
CloudFront only reads ACM from us-east-1).

**Alternatives considered.**

* **Provision a separate ACM cert resource INSIDE the module
  using a hard-coded `provider = aws` reference, and pin the
  primary stack to us-east-1.** Rejected — the W6 primary
  stack happens to be in us-east-1 today, but the convention
  is "the module works wherever the primary lives, and
  CloudFront's ACM goes to us-east-1 anyway." Pinning the
  primary region inside the module violates that convention.
* **Document the us-east-1 requirement in the README and let
  callers wire it however they want.** Rejected — without the
  `configuration_aliases` declaration, callers can pass a
  provider with the wrong region and `terraform apply`
  succeeds but produces broken CloudFront-cert references
  (the cert ARN refers to us-east-1 even when the underlying
  cert resource is in us-west-2 — silent breakage at
  `terraform apply` time, loud breakage at the first
  CloudFront viewer request).

Same pattern as W6 `dr-replication/`'s `aws.us_west_2` alias.
The W7 spec calls this "the explicit-provider convention." See
`docs/terraform.md` §5.4 for the standalone-validation rig
pattern that flows from this decision.

### 4. CloudFront is opt-in via `cloudfront = null`

**Decision.** The edge module's `cloudfront` input is an OBJECT
that, when `null`, suppresses the CloudFront distribution +
the apex Route53 ALIAS that points at it; the apex falls back
to ALB-direct via Route53 ALIAS. When non-null, the module
provisions the distribution + retargets the apex to the
CloudFront DNS name.

**Alternatives considered.**

* **Separate module `edge-with-cloudfront/` and `edge-no-cloudfront/`.**
  Rejected — code duplication, and the two modules would have
  to share 80% of their input variable definitions. Future
  bug-fixes would need two code paths.
* **Boolean flag `enable_cloudfront`.** Rejected — once flagged
  on, the module would need per-cloudfront-input default-fall-
  through (price class, allowed methods, etc.), and the default
  values for those don't have a "reasonable everywhere" answer.
  An object input forces the caller to make the choices
  explicit.
* **Always-on CloudFront.** Rejected — staging doesn't need a
  CDN (test traffic is in-team + one-hop test targets are
  cleaner without CDN-cache surprises); CloudFront's fixed
  cost ($0/mo + per-request) is real if you're running it for
  zero benefit.

Documented in `infra/terraform/modules/edge/variables.tf` (the
`cloudfront` variable's description + default `null`) and
`docs/terraform.md` §5.3 (usage example for both shapes).

### 5. GHCR→ECR mirror — `crane copy` + `cosign copy`, never docker

**Decision.** The mirror workflow uses `crane copy` for the
manifest and `cosign copy` for the `.sig` + `.att` sidecars.
The workflow's verify step asserts `crane digest <dest>` ==
`crane digest <src>` BEFORE the sigs are copied; any future
"fix" that swaps in docker-based mirroring fails this assertion.

**Alternatives considered.**

* **`docker pull && docker tag && docker push`.** Rejected —
  dockerd re-encodes the gzip stream on push, producing
  different layer digests (different gzip header bytes →
  different sha256). Different layer digests cascade up to
  different manifest digest. The cosign `.sig` sidecar at the
  destination registry doesn't resolve (it's at
  `<src-digest>.sig`, not `<dest-digest>.sig`). End result:
  unsigned-looking image at the destination, image admission
  fails closed.
* **`skopeo copy`.** Rejected — skopeo CAN preserve digests
  but doesn't natively understand cosign sidecars; the
  workflow would still need `cosign copy` for the `.sig` +
  `.att`. Net complexity is no better than `crane` + `cosign`.
* **`oras copy`.** Rejected — oras is OCI-artefact-focused;
  the docker-manifest-list shape is a second-class citizen.
  crane's docker-manifest-list support is first-class
  (`go-containerregistry` is the de-facto Go SDK).

The W7 workflow uses `imjasonh/setup-crane@v0.4` pinned to
crane v0.20.2 and `sigstore/cosign-installer@v3` pinned to
cosign v2.4.1 (same cosign version as `sign-image.yml` —
locked because cosign minor versions have had compat issues).

Documented in `docs/ghcr-to-ecr-mirror.md` §3.

### 6. Mobile External Testing is operator-driven, NOT auto-promoted

**Decision.** The External Testing workflow is `workflow_dispatch`-only;
NO `push` / `tag` triggers. Operator MUST invoke it explicitly
with `tag` + `release_notes` inputs.

**Alternatives considered.**

* **Auto-promote every Internal tag to External.** Rejected —
  Apple Beta App Review (~24 h on first External build of a
  new version) cannot be cancelled by re-triggering; pushing
  half-baked builds to External erodes tester goodwill.
* **Auto-promote on a separate tag prefix (e.g. `mobile-ext-vX.Y.Z`).**
  Considered — would address the "explicit gating" concern
  while keeping the path automated. Rejected because (a)
  operators already have to author release notes (External
  testers see them), so the `workflow_dispatch` input flow
  isn't a burden; (b) the W6 Internal flow uses `mobile-vX.Y.Z`
  tags and an `mobile-ext-vX.Y.Z` tag would diverge the
  Internal-to-External version-number relationship.

The workflow soft-fails on missing secrets (fork PRs can't
access them); operator-driven dispatches from `main` always
have them.

### 7. Six-file signer-identity invariant — `always_run: true` in pre-commit

**Decision.** The pre-commit hook runs on EVERY commit
(`always_run: true, pass_filenames: false`), independent of
which files are staged. The hook then inspects the full
six-file set.

**Alternatives considered.**

* **Hook scoped to staged files** (`files: <regex>`). Rejected
  — drift is a CROSS-FILE property. A partial commit that
  changes only one file shouldn't trigger the hook (under
  staged-file scoping) even if that change makes the file
  drift from the other five.
* **Hook ONLY on files in the six-file set.** Same trap as
  above — if a developer commits unrelated work (say a
  documentation typo fix), the hook doesn't run, and a
  PREVIOUS commit's drift goes unflagged at the next
  commit-time gate.
* **Hook as a CI gate only, not pre-commit.** Considered for
  W8 ride-along. Pre-commit is the W7 primary surface
  because (a) it catches drift BEFORE the commit lands in
  the developer's local branch (cheaper than CI); (b) CI
  parity ride-along is on the W8 action-item list and will
  catch fork PRs.

Documented in `docs/signer-identity-invariant.md` §5.

### 8. Path-divergence note from W7 task spec

**Spec said.** `infra/k8s/policies/kyverno-enforce-patch.yaml`
(the fifth tracked surface in the six-file invariant).

**Reality.** The path is `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`
— the enforce patch lives in the prod overlay, not under
`policies/`. The `policies/` directory carries the
cluster-wide `kyverno-cosign-verify.yaml`; the prod-specific
enforce patch (which mirrors the cluster-wide verifier but
sets `validationFailureAction: Enforce` for the prod
namespace) lives in the overlay.

**Decision.** Pre-commit hook + invariant doc use the real
path. The W7 spec is treated as an authoring-time hint, not
an authoritative path — the W8 hand-off memo flags the
divergence for the W8 implementer (especially relevant if
W8 touches the kyverno files in a way that warrants a
review of the W7 hook's tracked-file list).

### 9. RS256 JWT — SEPARATE Secret from W4 HS256, not a merge

**Decision.** Provision RS256 keys into a NEW Secret
(`mahjong-jwt-rsa-keys` / `mahjong-jwt-rsa-keys-staging`),
NOT by extending the W4 `mahjong-jwt-keys` Secret with RSA
entries. Mount via a NEW `envFrom` patch on each overlay's
`kustomization.yaml` (so HS256 and RS256 mounts co-exist
during the cutover window).

**Alternatives considered.**

* **Extend the W4 Secret with `Auth__JwtRsaKeys__N` entries
  alongside the HS256 `Auth__JwtSigningKeys__N`.** Rejected
  — HS256 keys are opaque random bytes; RS256 keys are
  PEM-encoded RSA privates. A single Secret would force the
  W7 binding's value parser to disambiguate per-index. And
  rotation cadences differ (HS256 30-day vs RS256 90-day);
  ESO would re-sync both even when only one is rotating,
  inflating the ESO logs.
* **Inline RSA private key in `appsettings.json`.** Rejected
  — appsettings is committed source; private keys never
  originate in this repo (W5 secrets-history sweep
  invariant).
* **AWS KMS asymmetric keypair instead of SSM SecureString PEM.**
  Considered — see Bishop's W7 plan for the KMS-side
  hardening path. NOT shipped in W7 because (a) KMS-Sign
  per-token-mint is a hot-path optimisation surface that
  hasn't been profiled; (b) all RSA keys living in SSM
  during W7 keeps the rotation runbook identical to the
  W4 HS256 procedure (operator muscle memory). KMS-backed
  path is on the W8 / W9 roadmap.

The new `envFrom` patch is `optional: true` so the deployment
starts before the operator has bootstrapped the RSA SSM
parameters — the W4 HS256 path stays canonical until the
operator flips `Auth:DefaultAlgorithm=RS256` in the
ConfigMap.

The prior `docs/jwt-rotation.md` §8.3 text described
extending the W4 Secret; W7 rewrites §8.3 to match the
actual implementation. The `jwt-ssm-runbook.md` reference
(line 24 of jwt-rotation.md) is unchanged — out of W7 scope.

### 10. June 2026 retro is the Q2 quarterly — adds §3a DR report

**Decision.** `docs/retro-2026-06.md` includes a §3a "DR
rehearsal report (Q2 2026 — quarterly)" between the regular
§3 (Lessons learned) and §4 (Action items). The next
quarterly is September 2026.

**Alternatives considered.**

* **Keep monthly retros short, do quarterly retros in a
  separate doc.** Rejected — monthly retros already capture
  most of the quarterly content; a separate doc would
  duplicate the metric-movement table and the W6/W7 actions.
* **Skip the DR rehearsal report; do it ad-hoc when a full
  live rehearsal runs.** Rejected — the W6 retro committed
  the squad to a Q2 partial rehearsal + a Q3 full live one;
  reporting back on the partial is the audit-trail surface
  for the commitment.

Quarterly retros are flagged in §6 (Cadence + template
notes) — the template-anchor convention is set so the W9
retro (next quarterly) follows the same shape.

## Verification

* `helm lint helm/mahjong/` clean.
* `helm template` on both overlays + `yaml safe_load_all` parses
  both renders.
* `terraform fmt -recursive infra/terraform/` clean.
* `terraform validate` PASSES on primary stack, DR env, AND
  edge module via the `.work/tf-edge-validate/` test rig (both
  cloudfront-on and cloudfront-off shapes).
* `actionlint` v1.7.7 clean on new + modified workflows.
* `python3 scripts/check_signer_identity.py` exits 0 with all
  six surfaces ✓; drift-detection smoke test exits 1 with
  "DRIFT" in stdout.
* `kustomize v5.4.3 build infra/k8s/overlays/{prod,staging}`
  renders clean (836 + 849 lines); RSA Secret + envFrom mount
  present in both.
* Backend gate 1422/0/0 preserved (no `src/**` touched in W7).

## Apone-lane scope discipline

Touched ONLY DevOps-lane paths: `.github/workflows/`, `helm/`,
`infra/`, `scripts/check_signer_identity.py`, `.pre-commit-config.yaml`,
`docs/{helm-charts,ghcr-to-ecr-mirror,signer-identity-invariant,terraform,mobile-release,jwt-rotation,slsa-provenance,retro-2026-06}.md`,
`CHANGELOG.md`, `.squad/agents/apone/history.md`,
`.squad/decisions/inbox/apone-phase-k-wave-7.md`. NO `src/**`,
NO `tests/**`, NO mobile source code. Pre-push `git status
--short` confirms zero out-of-lane staging.
