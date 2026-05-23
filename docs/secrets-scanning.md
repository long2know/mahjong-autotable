# Secrets scanning — defense-in-depth runbook

> Phase K Wave 5 — Apone (DevOps).

This runbook covers `mahjong-autotable`'s three-layer secrets-scanning
posture: the GitGuardian SaaS layer (README-recommended), the
in-CI `gitleaks` PR gate, and the on-demand `gitleaks` history
sweep. Each layer has a distinct failure mode; together they form
a defense-in-depth shape so a regression in any one layer leaves
the other two guarding the gate.

## 1. Layer overview

| # | Layer | Trigger | Scope | Failure mode |
|---|-------|---------|-------|--------------|
| 1 | GitGuardian SaaS app | every push event GitHub fires | full commit graph, vendor-controlled rule set | SaaS outage / rule rotation lag |
| 2 | `.github/workflows/secrets-scan.yml` (Wave 4) | every PR + push-to-main + nightly cron | PR diff (PR runs) + full history (main + cron), pinned `gitleaks-action@v2` ruleset | runner outage / action version drift |
| 3 | `.github/workflows/secrets-history-sweep.yml` (Wave 5, **THIS WORKFLOW**) | manual `workflow_dispatch` only | full commit graph from any ref, pinned `gitleaks` CLI version | requires operator initiation |

The first two are **continuous**; the third is **on-demand**.
Layer 3 is the "go look at every commit ever made" layer — it's
the one to run when GitGuardian's rule set adds a new detector
mid-quarter, or when the org rotates an entire credential class
and you need to know "did we ever leak one of these in the
past?".

## 2. Running the history sweep

### 2.1 From the GitHub UI

1. Open the Actions tab.
2. Pick **secrets-history-sweep** in the sidebar.
3. Click **Run workflow** (top-right of the run list).
4. Optionally:
   * **`ref`** — the branch, tag, or SHA to scan from. Defaults
     to `main`. Use a tag (e.g. `v0.13.0`) to bound the sweep to
     a release window.
   * **`severity-floor`** — `HIGH` (default, matches the W4
     gate) / `MEDIUM` / `LOW`. Surfacing MEDIUM / LOW is useful
     for the first run after enabling a new rule pack.
5. Click **Run workflow**.

The run takes 5–30 minutes depending on commit count. Watch the
job log; the **Step summary** at the end gives a finding count
and a pointer into the Security tab.

### 2.2 From the `gh` CLI

```bash
gh workflow run secrets-history-sweep.yml \
    --ref main \
    --field ref=main \
    --field severity-floor=HIGH

# Watch:
gh run watch
```

### 2.3 Locally (without GitHub)

If GitHub is unavailable or you want a faster turnaround, the
same command the workflow runs is reproducible locally:

```bash
brew install gitleaks    # macOS; on Linux, download the release tarball
cd path/to/mahjong-autotable
gitleaks detect \
    --source . \
    --report-format sarif \
    --report-path gitleaks-history.sarif \
    --no-banner \
    --verbose
# Open the SARIF file in VS Code's SARIF Viewer extension or pipe through `jq`.
```

## 3. What to do with findings

The default-rule gitleaks ruleset has a ~5% false-positive rate
on a mature repo (matches against test fixtures, encoded sample
keys in docs, etc.). Triage every finding through the same
three-step decision tree:

```
finding
   │
   ▼  Is it a real, live secret?
   ├── no  → add to .gitleaks.toml [allowlist] (regex or commit-id)
   │         + re-run the sweep to confirm clean
   │
   └── yes
       │
       ▼  Has it ever been valid in any environment?
       ├── no  (it was a placeholder / never deployed)
       │     → rewrite the commit to remove the value
       │       (force-push tag procedure below)
       │
       └── yes
             │
             ▼  ROTATE FIRST, then purge from history.
```

### 3.1 The rotate-then-purge order is non-negotiable

If you purge a leaked secret from git history BEFORE rotating
it in the issuing service, ANY clone of the repo that was made
between the leak landing and the purge still contains the
secret — and you've now told an attacker (via the rewrite commit
message) where to look. Always rotate the secret in the issuing
system FIRST, confirm the old credential is invalidated, THEN
rewrite git history.

### 3.2 Rotation procedure (per secret type)

| Secret class | Where to rotate |
|--------------|-----------------|
| AWS access keys | IAM → Users → Security credentials → Delete + create new access key. Update SSM / ESO secret. |
| OAuth client secrets (Google / GitHub / Microsoft) | Provider console → Credentials → Regenerate client secret. Update SSM `mahjong/{prod,staging}/app` JSON. See `docs/oauth-production-setup.md`. |
| `Auth.JwtSigningKeys` | Follow `docs/jwt-rotation.md` §3 (cycle key-active → key-previous → key-archive). |
| Cookie encryption key (`Auth.CookieEncryptionKey`) | Generate new key with `openssl rand -base64 64`; update SSM. Sessions will invalidate on next deploy. |
| TURN credentials | Follow `docs/turn-server-setup.md` HMAC-key rotation procedure. |
| Sentry DSN | Sentry project → Settings → Client Keys → Regenerate. Update SSM `mahjong/{prod,staging}/app`. |
| Database connection strings | Rotate DB user password via `psql` / RDS console; update SSM `mahjong/{prod,staging}/app`. |
| Generic / unknown | Treat as compromised; follow `docs/secret-rotation.md` general procedure. |

### 3.3 Force-push purge procedure

After the secret is rotated and the old value is confirmed dead:

```bash
# 1. Backup the repo state.
git tag pre-purge-backup-$(date +%Y%m%d)

# 2. Run git-filter-repo (NOT filter-branch — it's quadratic and the
#    upstream maintainers actively discourage it).
pip install git-filter-repo   # one-time, isolated env
git filter-repo \
    --replace-text <(echo "literal-leaked-value==>***PURGED***")

# 3. Force-push EVERY branch + tag the leaked value appears in.
#    NOTE: this is a coordination event — notify every collaborator
#    BEFORE pushing so they can rebase their local branches off the
#    new history. See https://git-scm.com/docs/git-rebase#_recovering_from_upstream_rebase.
git push --force-with-lease origin main
git push --force-with-lease origin --tags

# 4. Open a tracking issue + invalidate every CI cache:
gh cache list --all | awk '{print $1}' | tail -n +2 | xargs -L1 gh cache delete

# 5. Re-run the history sweep to confirm clean:
gh workflow run secrets-history-sweep.yml --ref main
```

### 3.4 What `git filter-repo` does NOT solve

* **Forks of the repo** still hold the leaked commit. There is
  no way to force a fork to rebase; treat the secret as fully
  compromised for the lifetime of the fork.
* **GitHub's REST API + GraphQL** caches commit content for
  ~24 hours; even after the force-push, the API may return the
  old content briefly. Wait 24 h before declaring "the leak is
  unreachable from this repo".
* **The Wayback Machine + similar archives** may have snapshotted
  the file. Submit a removal request manually.

The summary: rewriting history reduces the attack surface; it
does NOT eliminate it. ROTATION IS THE PRIMARY DEFENSE.

## 4. Operational cadence

| Cadence | Action | Owner |
|---------|--------|-------|
| Every PR | W4 `secrets-scan.yml` PR-diff scan + gate | automatic |
| Every push to main | W4 `secrets-scan.yml` full-main scan | automatic |
| Nightly 03:00 UTC | W4 `secrets-scan.yml` cron run | automatic |
| Quarterly | W5 `secrets-history-sweep.yml` manual run | Stephen / on-call |
| After any cross-organisation merge (e.g. upstream `pwmarcz/autotable` rebase) | W5 sweep run + manual triage | merger |
| Within 24h of any GitGuardian / gitleaks rule-pack release | W5 sweep run with `severity-floor: MEDIUM` to surface new-rule findings | on-call |
| After ANY secret is rotated for ANY reason | W5 sweep run to verify no other surface leaked the same value | rotator |

## 5. Triage SLA

| Severity | Triage time |
|----------|-------------|
| HIGH (cosign / AWS / OAuth / JWT signing key / DB password) | rotate within 1 hour; force-push purge within 24 hours |
| MEDIUM (Sentry DSN; analytics keys; non-prod test credentials) | rotate within 24 hours; purge within 7 days |
| LOW (placeholder strings, encoded sample keys) | add allowlist entry within 7 days |

## 6. Cross-references

* [`.github/workflows/secrets-scan.yml`](../.github/workflows/secrets-scan.yml) — Wave-4 PR / push gate.
* [`.github/workflows/secrets-history-sweep.yml`](../.github/workflows/secrets-history-sweep.yml) — Wave-5 history sweep (THIS workflow).
* [`docs/secrets.md`](secrets.md) — secret-management philosophy.
* [`docs/secret-management.md`](secret-management.md) — secret-management operator runbook.
* [`docs/secret-rotation.md`](secret-rotation.md) — rotation procedures per secret type.
* [`docs/jwt-rotation.md`](jwt-rotation.md) — JWT-key rotation specifically.
* <https://github.com/gitleaks/gitleaks> — upstream gitleaks docs.
* <https://github.com/newren/git-filter-repo> — supported tool for history rewrite.
* <https://docs.gitguardian.com/secrets-detection> — GitGuardian rule documentation.
