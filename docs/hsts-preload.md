# HSTS preload submission

> Phase K Wave 4 — Apone (DevOps).

This runbook covers the operator-driven procedure for getting the
production origin onto the [chromium HSTS preload list](https://hstspreload.org/).
The preload list is shipped baked-in to Chrome, Firefox, Safari,
Edge, and Opera, so any user opening `mahjong.example.com` for the
FIRST EVER TIME from a fresh browser will be forced over HTTPS —
even before our `Strict-Transport-Security` response header gets a
chance to install.

This is **NOT** a CI-automated step. The HSTS preload list is a
manual submission process at <https://hstspreload.org/>. The
chromium project owns the list and removal-after-submission is
deliberately slow (months) to protect end-users — so we treat
submission as a one-time gated operator action.

## 1. Prerequisites

Before submitting, the production origin MUST satisfy ALL of the
following (the form at <https://hstspreload.org/> will refuse the
submission otherwise):

| # | Requirement | How we satisfy it |
|---|-------------|-------------------|
| 1 | Serve a valid certificate on the apex domain | cert-manager + Let's Encrypt via the ingress (see `infra/k8s/base/ingress.yaml`). |
| 2 | Redirect HTTP → HTTPS on the same host | `nginx.ingress.kubernetes.io/force-ssl-redirect: "true"` (set in `infra/k8s/overlays/prod/hsts-patch.yaml`). |
| 3 | All subdomains served over HTTPS | TURN at `turn.mahjong.example.com` is TLS-only on 5349 (Wave-3 work). |
| 4 | `Strict-Transport-Security` header on the apex with `max-age` ≥ 31536000, `includeSubDomains`, `preload` | `infra/k8s/overlays/prod/hsts-patch.yaml` sets `max-age=63072000; includeSubDomains; preload`. |

## 2. Pre-submission dry-run (2-week cooling-off period)

The chromium project recommends a **2-week minimum** of serving the
fully-preload-eligible header before submitting. The motivation:
once you submit, removal is slow (the next chromium milestone
cycles in ~6 weeks; mobile browsers pin even longer). A wrong
header pins a downtime risk for months.

Dry-run procedure:

1. **Deploy the prod overlay** with the HSTS patch:

    ```bash
    kubectl apply -k infra/k8s/overlays/prod/
    ```

2. **Confirm the header** from outside the cluster:

    ```bash
    curl -sI https://mahjong.example.com | grep -i strict-transport
    # Expected exactly:
    # Strict-Transport-Security: max-age=63072000; includeSubDomains; preload
    ```

3. **Confirm subdomains** are HTTPS-only (no `http://` redirect
   target anywhere):

    ```bash
    curl -sI -o /dev/null -w '%{http_code} %{redirect_url}\n' http://turn.mahjong.example.com
    # Expected: 301 https://turn.mahjong.example.com/  (or equivalent 308)
    ```

4. **Wait at least 14 days**, monitoring `/api/csp-report`
   violations (Wave-3) + Sentry for any mixed-content errors. If
   ANY downstream consumer reports broken assets (a forgotten
   `http://` CDN, an old hard-coded subdomain), STOP and fix
   BEFORE submitting — preloading with a broken downstream is the
   one mistake that genuinely cannot be reversed quickly.

5. **Run the chromium pre-flight tool** locally:

    ```bash
    # Either via the JSON API:
    curl -s "https://hstspreload.org/api/v2/preloadable?domain=mahjong.example.com" | jq

    # Or via the web form:
    open https://hstspreload.org/?domain=mahjong.example.com
    ```

   Expected: `"errors": []`. Any error listed there will block
   the submission form too — fix at the ingress layer (or the
   relevant subdomain's listener) BEFORE moving on.

## 3. Submission

Once §2 passes for 14+ consecutive days **AND** the daily
verification workflow (§3a) has been green for 14+ consecutive
days:

1. **Open** <https://hstspreload.org/?domain=mahjong.example.com>
2. **Tick** the two confirmation boxes:
   * "I am the owner of this site, or have their permission to
     preload HSTS."
   * "I understand that preloading my site through this form will
     prevent users from accessing it via HTTP."
3. **Click** "Submit to the HSTS preload list."
4. **Record the submission date** in this file's CHANGELOG below.

## 3a. Verification workflow (Wave 5)

[`.github/workflows/hsts-readiness-check.yml`](../.github/workflows/hsts-readiness-check.yml)
ships a continuous probe that fires daily at 13:00 UTC and on
demand via `workflow_dispatch`. It:

1. `curl -I`s the production origin (`https://mahjong.example.com/`
   by default, overridable via the repo variable `HSTS_PROBE_URL`
   or the dispatch `url:` input).
2. Asserts the response includes EXACTLY:

   ```
   Strict-Transport-Security: max-age=63072000; includeSubDomains; preload
   ```

3. On failure: opens (or updates) a sticky GitHub issue titled
   `HSTS readiness: production header missing the preload directive`
   with the observed value, expected value, and triage steps. The
   workflow run also goes RED so the daily cron failure is visible
   in the Actions tab.
4. On recovery (next pass after a fail): closes the sticky issue
   automatically with a recovery comment.

### 3a.1 Operating the probe

| Need | Action |
|------|--------|
| Run the probe on demand | Actions → `hsts-readiness-check` → Run workflow. Optionally supply a `url:` to probe a non-default origin. |
| Suppress issue creation (e.g. during planned outage) | Run workflow with `open-issue-on-failure: false`. |
| Change the probe URL permanently | Set the repo variable `HSTS_PROBE_URL` (Settings → Secrets and variables → Actions → Variables). The workflow picks it up on the next run. |
| Mute the failure alert | Close the sticky issue manually; the workflow will RE-open it on the next failure run. |

### 3a.2 Pre-submission gate

The submission to <https://hstspreload.org/> is a one-way door
(removal takes ~6 weeks to propagate). Treat 14 consecutive
green daily runs of `hsts-readiness-check` as a HARD gate
before clicking "Submit": if the probe has ever gone red in the
past 14 days, restart the 14-day clock.

### 3a.3 Post-submission monitoring

After submission, the daily probe becomes the early-warning
system for in-process header drift. Any failure during
post-submission is a P0 incident: the in-process header is the
backstop while the preload-list removal request goes through
(which takes ~6 weeks). The sticky-issue alert is the on-call
prompt to escalate.

## 4. Post-submission monitoring

After submission:

| Cadence | Action |
|---------|--------|
| Weekly for 6 weeks | Re-run §2 step 2 + 3 to confirm header + form-validity haven't drifted. |
| On next Chrome / Firefox / Safari major release | Confirm the domain has shipped to the production preload list. The chromium source-of-truth is <https://chromium.googlesource.com/chromium/src/+/HEAD/net/http/transport_security_state_static.json>; `grep mahjong.example.com` will return a single line once shipped. |
| If a regression slips through | Open a removal request at <https://hstspreload.org/removal/> — removal takes ~6 weeks to propagate. Plan accordingly. |

## 5. Why a 2-year `max-age`

The hstspreload.org form **requires** `max-age` ≥ 31536000
(1 year). We set 63072000 (2 years) for two reasons:

* The chromium project actively **recommends** the 2-year value as
  a defense against rare clock-skew scenarios where a 1-year max
  could lapse just before a browser update refreshes the preload
  list.
* Once a domain is in the preload list, the in-process header is
  belt-AND-suspenders: a 2-year header is the suspenders, the
  preload list is the belt. If the preload list ever drops the
  domain (e.g. browser-side cleanup), the still-served 2-year
  header keeps users on HTTPS for the transition window.

## 6. CHANGELOG

| Date | Action | By |
|------|--------|----|
| 2026-05-27 | Wave-4 HSTS-preload header patch shipped (`infra/k8s/overlays/prod/hsts-patch.yaml`). Pre-submission dry-run window starts. | Apone (DevOps) |
| 2026-05-28 | Wave-5 daily readiness probe shipped (`.github/workflows/hsts-readiness-check.yml`). 14-day gate restarts on first probe run. | Apone (DevOps) |
| _pending_ | Operator dry-run + submission at https://hstspreload.org/ | Stephen |
| _pending_ | Chrome / Firefox / Safari preload-list confirmation | Stephen |

## 7. Cross-references

* [`infra/k8s/overlays/prod/hsts-patch.yaml`](../infra/k8s/overlays/prod/hsts-patch.yaml) — the Ingress annotation that emits the preload-eligible header.
* [`src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs`](../src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs) — the in-process middleware that emits the 1-year header in dev/staging (Wave 3).
* [`docs/reverse-proxy.md`](reverse-proxy.md) — nginx reverse-proxy reference for non-k8s deploys.
* <https://hstspreload.org/> — chromium-owned submission portal.
* <https://chromium.googlesource.com/chromium/src/+/HEAD/net/http/transport_security_state_static.json> — preload-list source of truth.
