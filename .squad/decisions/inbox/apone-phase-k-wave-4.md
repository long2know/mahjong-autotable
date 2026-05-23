# Apone (DevOps) — Phase K Wave 4 decision memo

**Date:** 2026-05-27
**Branch:** `stlong/phase-k-wave-4-bringup` (from `main` post-Wave-3 merge `974a7a9`).
**Author:** Apone (DevOps) — `apone@squad.mahjong`.

## Scope

Four items, per the assignment from Stephen:

1. SLSA Level 3 in-toto provenance for every published image.
2. ESO secret mounts for `Auth.JwtSigningKeys` array (rotation-state-named SSM convention).
3. Kyverno `validationFailureAction: Enforce` hard-pin for `mahjong-prod`.
4. HSTS preload (`preload` directive + 2-year `max-age`) on prod Ingress + `gitleaks` secrets-scanning workflow.

Backend gate (1152 / 0 / 0 from Wave-3) preserved — Wave-4 scope is pure DevOps + infra + docs.

## Decisions

### 1. SLSA L3 — separate workflow over inlining

Same rationale as Wave-1's choice to split `sign-image.yml` from `docker-build.yml`. The SLSA reusable workflow (`slsa-framework/slsa-github-generator/.github/workflows/generator_container_slsa3.yml@v2.0.0`) MUST be the L3 isolation boundary — running it inline in `docker-build.yml` would collapse the builder into the build runner and forfeit the L3 guarantee. Three-job shape: `resolve-digest` (read-only, mirrors `sign-image.yml`'s pattern) → `provenance` (reusable workflow, isolated runner pool) → `attach-to-release` (gh release upload on tag pushes only). Workflow pinned to `@v2.0.0` per the generator's pinning requirement (fully-qualified tag, not shorthand or sha).

### 2. ESO JWT-keys secret — SEPARATE from the omnibus

Did NOT extend `overlays/prod/secret-template.yaml`. Created a NEW `mahjong-jwt-keys` ExternalSecret in `infra/k8s/overlays/prod/jwt-keys-secret.yaml` instead. Two reasons:

- **Rotation-data-plane independence.** JWT key rotation is high-frequency (annual baseline; emergency on minutes' notice). The omnibus secret carries DB connection strings + OAuth client secrets + Sentry DSN — rotating ANY of those needs the omnibus JSON re-shaped. Putting JWT keys on their own ExternalSecret means rotation touches only the JWT keys SSM parameters; no risk of fat-fingering the omnibus JSON.
- **Refresh-interval split.** Omnibus is on 1 h refresh (the slow-rotating secrets); JWT keys are on 15 min refresh (emergency rotation must propagate in minutes, not hours). One ExternalSecret can only have one refresh interval — splitting was the cleanest path.

### 3. Rotation-state SSM names (NOT array indices)

Used `/mahjong/prod/auth/jwt/key-{active,previous,archive}` for the SSM parameter names rather than the array-shaped `auth__jwtsigningkeys__{0,1,2}` originally specified. The translation from rotation-state name → indexed env var happens at the ESO `data:` template layer. Operator NEVER has to compute "which numeric index holds value X today?" — they cycle values BETWEEN named parameters; ESO re-binds at materialise time. The `__N` double-underscore env-var binding (ASP.NET Core config-array convention) is preserved end-to-end on the cluster side — Bishop's W4/W5 binding sees the standard indexed shape.

### 4. Kyverno hard-pin — SECOND policy, not a patch on the first

Did NOT patch the Wave-3 `verify-mahjong-images` ClusterPolicy to flip `validationFailureAction: Enforce` globally — that would break the staging audit-only experimentation surface. Instead, shipped a SECOND ClusterPolicy (`enforce-prod-mahjong-images`) scoped exclusively to `mahjong-prod` with `Enforce` + no overrides + single-purpose `match:` block. Multiple Kyverno policies on the same image just compose (both must verify before admission). Listed as a `resource:` in the prod kustomization rather than a `patch:` because it's an independent cluster object, not a strategic-merge target. Two policies on the same image means a signed image admits; an unsigned image rejects even if the Wave-3 policy was accidentally demoted to Audit.

### 5. HSTS preload — Ingress-layer (NOT C# middleware)

Pinned the preload-eligible header at the ingress layer (`nginx.ingress.kubernetes.io/configuration-snippet`) rather than extending the in-process `SecurityHeadersMiddleware`. Three reasons:

- The middleware already emits `max-age=31536000; includeSubDomains` controlled by the `Security:EnableHsts` config key (Wave-1 surface). Adding a `Security:HstsPreload` second key would work but doubles the surface area for a one-time change.
- Operators probing the header BEFORE submitting to hstspreload.org need the header firing from the SAME layer end-users hit — the Ingress is that layer.
- Pinning at the Ingress means a future refactor of `SecurityHeadersMiddleware` cannot accidentally weaken the preload-eligible value (browsers' baked-in preload pins are irreversible-by-design for months).

`force-ssl-redirect: true` + `ssl-redirect: true` are also pinned explicitly in the same patch — defense-in-depth against a global ConfigMap edit.

### 6. gitleaks — second layer to GitGuardian

GitGuardian (already README-recommended) is a SaaS layer that scans pushed branches. `gitleaks` runs inside CI on every PR, with a pinned ruleset version (no silent vendor drift) and SARIF → Code Scanning. Two layers, two failure modes — same `report and block` floor. Concurrency-grouped on `secrets-scan-${{ github.ref }}` so PR refreshes cancel in-flight prior runs (matches the pattern in other security workflows).

## Files

### Added (selective)
- `.github/workflows/slsa-provenance.yml`
- `.github/workflows/secrets-scan.yml`
- `infra/k8s/overlays/prod/jwt-keys-secret.yaml`
- `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`
- `infra/k8s/overlays/prod/hsts-patch.yaml`
- `docs/slsa-provenance.md`
- `docs/hsts-preload.md`
- `.squad/decisions/inbox/apone-phase-k-wave-4.md` (this file — force-added).

### Modified
- `infra/k8s/overlays/prod/kustomization.yaml` (wired the three new files in via `resources:` + `patches:`).
- `docs/jwt-rotation.md` (§1, §3, §4, §5, §7 — ESO + rotation-state-named SSM convention).
- `docs/admission-policy.md` (new §5.3 — Wave-4 canary procedure).
- `CHANGELOG.md` — new `[0.13.0]` section + compare-link footnote.
- `.squad/agents/apone/history.md` (appended — force-added).

### Out-of-scope / NOT STAGED
- `.copilot/skills/error-recovery/`
- `.github/workflows/squad-*.yml` (pre-session ×7)
- `.tool-actionlint/`
- `.work/`

## Lint / build gates

- `actionlint` clean on the two new workflows + verified unchanged behaviour on the existing ones.
- `yaml.safe_load_all` clean on all new + modified YAML manifests.
- `bash -n` clean on all in-workflow `run:` scripts.
- Backend test gate not re-run (`src/**` untouched; baseline 1152/0/0 from Wave-3 preserved).

## Handoff to Wave 5+

1. **Bishop:** code-side `Auth.JwtSigningKeys` binding still pending. The data-plane is now ready — once `IConfiguration.GetSection("Auth:JwtSigningKeys").Get<string[]>()` feeds `TokenValidationParameters.IssuerSigningKeys`, the W4 ESO-materialised values flow through with zero further DevOps work.
2. **Stephen (operator):** seed the three SSM parameters BEFORE applying the prod overlay:
   ```
   openssl rand -base64 48 | aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-active   --type SecureString --value file:///dev/stdin
   openssl rand -base64 48 | aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-previous --type SecureString --value file:///dev/stdin
   openssl rand -base64 48 | aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-archive  --type SecureString --value file:///dev/stdin
   ```
   Then `kubectl apply -k infra/k8s/overlays/prod/` and `kubectl apply -f infra/k8s/overlays/prod/jwt-keys-secret.yaml`.
3. **Stephen (operator):** HSTS preload submission is manual — `docs/hsts-preload.md` is the runbook. The 2-week dry-run gate MUST pass before clicking submit at hstspreload.org/.
4. **Vasquez (audit):** new gitleaks SARIF lands under `category: gitleaks` in the Security tab. New SLSA provenance is verifiable via `slsa-verifier verify-image ghcr.io/long2know/mahjong-autotable@<digest> --source-uri github.com/long2know/mahjong-autotable` against any release ≥ v0.13.0.
5. **Future Phase K wave (W5+):**
   - Apone-W5: extend the Wave-4 SLSA provenance workflow to ALSO attest the SBOM (`slsa-github-generator` v2 supports attaching multiple subjects in one predicate).
   - Apone-W5: wire `kyverno verify-images` with an `attestations:` block that requires the SLSA predicate alongside the cosign signature (currently the Kyverno policy verifies the signature only).
   - Apone-W5: extend `staging` overlay with its own `jwt-keys-secret.yaml` (Wave-4 only shipped prod — staging still uses the omnibus's singular `Auth__JwtSigningKey`).
   - Apone-W5: integrate `gh-org-secret-scanner` for org-wide visibility of historical commits (the Wave-4 workflow scans diffs + history on `main`; an org-wide retroactive sweep is the next defense layer).

## Patterns locked for future DevOps work

- **Four-layer supply-chain enforcement** (workflow → release-gate → admission → SLSA provenance). Each layer has a distinct bypass; together they form defense-in-depth. The signer-identity regex stays as the cross-layer invariant — change it in ONE place, change it in ALL FIVE: `sign-image.yml`, `verify-signature.yml`, `kyverno-cosign-verify.yaml`, `kyverno-enforce-patch.yaml` (Wave-4), and the `--source-uri` arg in `docs/slsa-provenance.md` §4.
- **Two-policy Kyverno pattern.** Cluster default for global behaviour; supplemental Enforce-scoped policy in the prod overlay for the hard-pin. Multiple policies on the same image compose; no precedence conflicts.
- **Rotation-state-named SSM parameters** (not numeric indices). Operators cycle values BETWEEN named parameters; ESO re-binds to the framework's indexed shape at materialise time. Reusable for any future rotation surface (HMAC keys, signing certs, refresh tokens).
- **Two-secret split for high-frequency-rotated values.** Omnibus secret for the slow-rotating commodity values (DB, OAuth, Sentry); per-purpose secrets (JWT keys, TURN creds) for the high-frequency rotators. Keeps rotation surface area minimal.
- **HSTS preload at the ingress layer.** Pinned-at-the-wire defense against in-process middleware refactors that could weaken the header.
- **Defense-in-depth secrets scanning.** GitGuardian SaaS + gitleaks in-CI = two layers, two failure modes. SARIF categories distinct so findings don't overlay.
- **SLSA-generator pinning is a fully-qualified `@vX.Y.Z` tag.** Not a shorthand. Not a sha. Bumping is a coordinated change with `slsa-verifier` end-to-end re-verification on the merge commit.
