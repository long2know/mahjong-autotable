# Lighthouse 13 `--form-factor=desktop` validation error — W18 root-cause fix

> Phase K Wave 18 — Apone (DevOps). Companion to:
> [`.github/workflows/pwa-audit.yml`](../.github/workflows/pwa-audit.yml)
> (the workflow this fix lands in), [`docs/lh13-soft-pin-rationale.md`](./lh13-soft-pin-rationale.md)
> (the W17 rationale for soft-pinning Lighthouse 13.x), and
> the W17 PR ship-note in `CHANGELOG.md` 0.26.0 ("LH13 root
> cause identified").
>
> Audience: SRE / on-call who reads the `pwa-audit.yml`
> nightly + per-PR run logs and the Hudson `lighthouse-
> scores-prod` panel. The fix is one workflow line; this doc
> records the diagnostic chain + the verification protocol.

## 1. Why this doc exists

Wave 17 broadened the SLSA-3 SHA-pin discipline + landed
Mobile CI Android signing groundwork + captured the W17
us-east-1 dry-run. Apone's W17 coordinator role included
three manual `gh workflow run pwa-audit.yml` invocations to
calibrate the Lighthouse 13 score baseline against the
post-K17 build. One of those three runs completed and FAILED
with this exact runtime error:

> Runtime error encountered: Screen emulation mobile setting
> (true) does not match formFactor setting (desktop). See
> https://github.com/GoogleChrome/lighthouse/blob/main/docs/
> emulation.md

That message is Lighthouse 13's STRICT-MODE validation
rejecting the workflow's `--form-factor=desktop` flag
combined with the default screen-emulation profile (which
defaults to `mobile: true`).

Lighthouse 12.x WARNED on this combination but proceeded
with the desktop form-factor. Lighthouse 13.x ERRORS and
exits non-zero — the W17 manual invocation surfaced the W17
LH13 soft-pin (per `docs/lh13-soft-pin-rationale.md`) as
W18's first PR-blocker.

## 2. Root cause

Lighthouse's `--form-factor` flag controls TWO independent
runtime concerns that historically defaulted together:

1. **The `formFactor` field** in the emitted JSON report —
   the score-aggregation logic uses this to apply
   form-factor-specific scoring rubrics.
2. **The `screenEmulation` config block** — viewport size,
   device-pixel-ratio, and `mobile` flag that Chrome's
   DevTools-Protocol emulation engine consumes.

In Lighthouse 12.x, passing `--form-factor=desktop` IMPLICITLY
flipped `screenEmulation.mobile` to `false`. In Lighthouse
13.x, the implicit flip was REMOVED — the operator MUST
pass BOTH flags explicitly or accept the strict-mode
validation error.

The PR-readiness diff is:

```diff
            --only-categories=performance,accessibility,best-practices,seo \
            --form-factor=desktop \
+           --screenEmulation.mobile=false \
            --throttling-method=provided \
```

The new `--screenEmulation.mobile=false` flag pairs with
`--form-factor=desktop` and clears Lighthouse 13's strict-mode
gate. The change is ADDITIVE — no existing behaviour changes;
Lighthouse 12.x ignores the new flag (or treats it as a no-op
since the default was already `false` when `form-factor=
desktop` was set).

## 3. The fix

Single-line workflow edit in
`.github/workflows/pwa-audit.yml`:

```yaml
      - name: Run Lighthouse 13
        run: |
          set -euo pipefail
          npx lighthouse http://127.0.0.1:4173/ \
            --quiet \
            --chrome-flags="--headless=new --no-sandbox --disable-gpu" \
            --output=json \
            --output=html \
            --output-path=./.lighthouse-report \
            --only-categories=performance,accessibility,best-practices,seo \
            --form-factor=desktop \
            --screenEmulation.mobile=false \
            --throttling-method=provided \
            --max-wait-for-load=45000 || { ... }
```

`actionlint` validation: PASS (no schema change; the new
flag is part of the `run:` block shell command, not the
workflow schema).

## 4. Verification protocol

### 4.1 Static — `actionlint`

```bash
.tool-actionlint/actionlint .github/workflows/pwa-audit.yml
```

Exit code `0` confirms the workflow parses cleanly.

### 4.2 Static — `grep`

```bash
grep -n "form-factor\|screenEmulation" .github/workflows/pwa-audit.yml
```

Expected output:

```
153:            --form-factor=desktop \
154:            --screenEmulation.mobile=false \
```

The two flags MUST appear in adjacent order — Lighthouse's
CLI parser is whitespace-tolerant but the pair belongs
together for readability.

### 4.3 Dynamic — manual workflow trigger

Post-merge of the W18 PR, the operator runs two manual
invocations to confirm the LH13 runtime no longer errors:

```bash
gh workflow run pwa-audit.yml --ref main
# Wait 60s to avoid the workflow's concurrency-cancel guard.
sleep 60
gh workflow run pwa-audit.yml --ref main
```

Both runs MUST complete with `success` (NOT
`failure`/`cancelled`). The `Run Lighthouse 13` step's log
should contain neither the W17 strict-mode error nor a
non-zero exit; the downstream `Manifest + PWA installability
lint` step should consume the freshly-written
`.lighthouse-report.report.json`.

### 4.4 Dynamic — cron-driven calibration

The W18 deliverable expects the next THREE cron runs of
`pwa-audit.yml` (the workflow runs nightly + per-PR) to
all complete with `success`. Apone's W19 hand-off includes
the calibration-data appendix to
`docs/lh13-soft-pin-rationale.md` once the three cron runs
land — the appendix records the actual LH13 Performance +
Accessibility + Best-Practices + SEO scores against the
post-K17 build so W19+ can flip the soft-pin to a hard pin
if the scores trend stable.

## 5. Why this wasn't caught at LH13 cutover

The W17 LH13 soft-pin landed with the rationale documented
in `docs/lh13-soft-pin-rationale.md`: the LH12 → LH13 bump
removed the `pwa` score category, requiring a downstream
post-processing step (`scripts/manifest-lint.js`) to
synthesize a PWA-like score from the four remaining
categories. The W17 wave-author tested the bump with the
DEFAULT form-factor (mobile) — the strict-mode validation
on `form-factor=desktop` was NOT exercised because the
test invocation used the default mobile form-factor.

The W17 PR's nightly cron run was the FIRST run to
exercise the desktop form-factor; it failed silently in
the nightly schedule (the workflow's `if:
github.event_name == 'pull_request'` guard suppressed PR
comment emission for cron-triggered runs).

Apone's W17 coordinator role caught the failure via the
three manual workflow-run probes (the manual `workflow_dispatch`
trigger surfaces the run in `gh run list --workflow=pwa-audit.yml`,
making the failure visible without waiting for the next
PR-triggered run).

The W18 fix is one line; the diagnostic + ship work
captured here is the W17 → W18 hand-off evidence.

## 6. W18 cron-run calibration table

The following table is filled in by Apone's W19 wave-author
once the three post-W18 cron runs complete. Each row
records the workflow run ID + the four LH13 scores
emitted by the `Run Lighthouse 13` step's
`.lighthouse-report.report.json` artifact.

| Run # | Run ID  | Triggered (UTC)     | Performance | Accessibility | Best-Practices | SEO   | PWA-synth (manifest-lint) |
|-------|---------|---------------------|-------------|---------------|----------------|-------|----------------------------|
| 1     | _TBD_   | _post-W18 cron_     | _TBD_       | _TBD_         | _TBD_          | _TBD_ | _TBD_                      |
| 2     | _TBD_   | _post-W18 cron_     | _TBD_       | _TBD_         | _TBD_          | _TBD_ | _TBD_                      |
| 3     | _TBD_   | _post-W18 cron_     | _TBD_       | _TBD_         | _TBD_          | _TBD_ | _TBD_                      |

Once the table fills with three rows of `success`-status data,
the W19 wave-author appends a verdict line:

> "Three post-W18 cron runs landed successfully; the W17 LH13
>  soft-pin (per `docs/lh13-soft-pin-rationale.md`) graduates
>  to a hard pin against the calibrated baseline."

## 7. What this fix does NOT change

* The Lighthouse version SOFT-PIN at `^13` in
  `.github/workflows/pwa-audit.yml` is unchanged — the W17
  soft-pin rationale (per `docs/lh13-soft-pin-rationale.md`)
  holds; the W18 fix is a flag addition, NOT a version
  rollback to LH12.
* The four-category scoring scope (performance,
  accessibility, best-practices, seo) is unchanged.
* The Manifest + PWA installability lint step
  (`scripts/manifest-lint.js`) is unchanged.
* The PR sticky-comment shape is unchanged — the
  `Compose PR comment body` step renders the same fields.
* The Hudson `lighthouse-scores-prod` panel ingestion is
  unchanged — the W18 fix restores the post-K17 score
  emission that the W17 strict-mode error had been silently
  blocking.

## 8. Cross-references

* `.github/workflows/pwa-audit.yml` — the workflow this fix
  lands in (line 154, new `--screenEmulation.mobile=false`
  flag).
* `docs/lh13-soft-pin-rationale.md` — W17 LH12 → LH13 bump
  rationale; this doc's §4.4 calibration table feeds the
  hard-pin decision.
* `CHANGELOG.md` — 0.27.0 (W18) records the fix; 0.26.0
  (W17) recorded the root-cause identification.
* `tests/ci/lane-map.json` — `pwa_audit_workflow_shared`
  lane-discipline entry; `pwa-audit.yml` is co-authored by
  Hicks (frontend asset author) and Apone (workflow runtime
  owner) — primary is Apone. The W18 fix is workflow-runtime
  (Apone-primary).
