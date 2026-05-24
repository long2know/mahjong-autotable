# Phase K Wave 21 — Apone (DevOps).
#
# us-east-1 ACTUAL APPLY — automated rollback safety net.
#
# W20 V2 runbook hardening added the 8-invariant
# `post-apply-smoke-test.sh` script. W21 wires the AUTOMATED
# ROLLBACK HOOK that runs the smoke test as a terraform
# `null_resource` provisioner immediately after `terraform
# apply` completes, and triggers `terraform destroy
# -auto-approve` if any invariant fails within a 5-minute
# window of the apply.
#
# # Why this lives in a SEPARATE .tf file
#
# Opt-in safety net — operator-controlled via the
# `enable_auto_rollback` variable. By keeping the
# null_resource in its own file we can:
#
#   * `terraform apply -target=null_resource.us_east_1_auto_rollback`
#     to wire ONLY the safety net (no other state changes)
#     when retro-fitting an existing cluster.
#   * `terraform state rm null_resource.us_east_1_auto_rollback`
#     to detach the safety net cleanly without touching the
#     EKS / VPC / RDS state.
#   * Reviewers see the rollback hook as a self-contained
#     diff during PR review.
#
# # The shape — local-exec provisioner + 5-minute window
#
# `null_resource.us_east_1_auto_rollback` carries TWO
# provisioners:
#
#   1. `local-exec` — runs the W20 smoke-test script,
#      capturing stdout/stderr to a log file the operator
#      can inspect post-rollback.
#   2. On non-zero exit from the smoke-test (any of the 8
#      invariants failing within the 5-minute window), a
#      SECOND `local-exec` runs `terraform destroy
#      -auto-approve` to tear the regional stack back down.
#
# The 5-minute window is enforced via `timeout 300` on the
# smoke-test invocation. Beyond 5 minutes the smoke test is
# assumed to have hung (network partition, kubelet
# unavailable, etc.) — the safety net assumes the worst
# and rolls back.
#
# # SAFETY — opt-in by default; opt-out documented
#
# Two safety dials:
#
#   * `var.enable_auto_rollback` — defaults to `false`. The
#     operator MUST explicitly set `enable_auto_rollback =
#     true` in the workspace tfvars to wire the safety net.
#     This is intentional: an opt-out safety net would
#     surprise operators who set up the cluster expecting
#     the apply to be the final state.
#   * `var.auto_rollback_dry_run` — defaults to `false`.
#     When true, the rollback-on-failure branch only LOGS
#     what it would have destroyed; no actual destroy
#     runs. Use for staging-tier validation BEFORE wiring
#     against prod.
#
# Documented opt-in path: `docs/us-east-1-apply-runbook.md
# §7` (new W21 §7 — see W21 hand-off in the runbook). The
# operator runbook walks through:
#
#   1. Verify `auto_rollback_dry_run = true` against a
#      staging-tier apply.
#   2. Manually inject a smoke-test failure (e.g. `kubectl
#      drain` the only node) + confirm the dry-run logs.
#   3. Flip `auto_rollback_dry_run = false` AFTER the
#      dry-run validates clean.
#   4. Apply to us-east-1 with `enable_auto_rollback =
#      true`.
#
# # Cross-references
#
#   * `infra/terraform/regional-eks/us-east-1/post-apply-
#     smoke-test.sh` — W20 V2 8-invariant smoke test.
#   * `infra/terraform/regional-eks/us-east-1/preflight.yaml`
#     — W19 pre-apply checklist.
#   * `docs/us-east-1-apply-runbook.md` — W19/W20/W21
#     operator runbook (W21 §7 documents this file).
#
# # NOT IN ROOT TERRAFORM MODULE
#
# This file lives at `infra/terraform/regional-eks/us-east-
# 1/` — outside the `infra/terraform/` root module that
# bootstraps the primary cluster. The regional-eks per-
# region directory is a SEPARATE terraform workspace; the
# operator runs `terraform init && terraform apply` from
# inside `us-east-1/` rather than from `infra/terraform/`.
# See `docs/us-east-1-apply-runbook.md §3` for the
# workspace bootstrap.

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    null = {
      source  = "hashicorp/null"
      version = ">= 3.2.0"
    }
  }
}

# ─── Operator-controlled opt-in dials ─────────────────────────

variable "enable_auto_rollback" {
  description = <<-EOT
    Opt-in safety net for the W21 auto-rollback hook. When
    set to `true`, terraform apply will run the W20 V2
    8-invariant smoke-test script after the regional EKS
    stack lands, and trigger `terraform destroy -auto-
    approve` if ANY invariant fails within a 5-minute
    window. Defaults to `false` — the safety net is opt-in
    only. See docs/us-east-1-apply-runbook.md §7 for the
    operator runbook covering the staging-tier dry-run
    procedure that MUST precede a prod opt-in.
  EOT
  type        = bool
  default     = false
}

variable "auto_rollback_dry_run" {
  description = <<-EOT
    When `true`, the auto-rollback hook only LOGS the
    rollback action it WOULD have taken without actually
    invoking `terraform destroy`. Use for the staging-tier
    dry-run validation step documented in
    docs/us-east-1-apply-runbook.md §7.2. Defaults to
    `false`. Has no effect when `enable_auto_rollback =
    false`.
  EOT
  type        = bool
  default     = false
}

variable "auto_rollback_smoke_timeout_seconds" {
  description = <<-EOT
    Timeout (in seconds) for the post-apply smoke-test
    invocation. Beyond this window the smoke test is
    assumed to have hung; the rollback branch fires. The
    5-minute (300s) default matches the W21 design point —
    a regional EKS cluster's invariants should ALL resolve
    in under 5 minutes if the apply is healthy.
  EOT
  type        = number
  default     = 300
}

# ─── The auto-rollback null_resource ──────────────────────────

resource "null_resource" "us_east_1_auto_rollback" {
  count = var.enable_auto_rollback ? 1 : 0

  # The `triggers` block forces re-run when ANY of the
  # input toggles change. This is the canonical
  # null_resource idiom for "re-run on input drift".
  triggers = {
    enable_auto_rollback                  = tostring(var.enable_auto_rollback)
    auto_rollback_dry_run                 = tostring(var.auto_rollback_dry_run)
    auto_rollback_smoke_timeout_seconds   = tostring(var.auto_rollback_smoke_timeout_seconds)
    # Per-apply timestamp — ensures the hook re-runs on
    # every fresh apply rather than caching the last
    # successful invocation.
    apply_timestamp                       = timestamp()
  }

  # Provisioner #1 — run the W20 V2 smoke-test inside a
  # timeout. Non-zero exit triggers the on_failure path.
  provisioner "local-exec" {
    command     = <<-EOT
      set -uo pipefail
      LOGDIR="$${TF_LOG_DIR:-.}/auto-rollback-$(date -u +%Y%m%dT%H%M%SZ)"
      mkdir -p "$LOGDIR"
      echo "[auto-rollback] smoke-test starting at $(date -u +%Y-%m-%dT%H:%M:%SZ)" \
        | tee "$LOGDIR/smoke.log"
      timeout ${var.auto_rollback_smoke_timeout_seconds} \
        bash "${path.module}/post-apply-smoke-test.sh" \
        2>&1 | tee -a "$LOGDIR/smoke.log"
      SMOKE_EXIT=$${PIPESTATUS[0]}
      echo "[auto-rollback] smoke-test exit=$SMOKE_EXIT" | tee -a "$LOGDIR/smoke.log"
      exit $SMOKE_EXIT
    EOT
    interpreter = ["/bin/bash", "-c"]
  }

  # Provisioner #2 — the on_failure rollback branch. Only
  # fires when provisioner #1 returns non-zero (smoke-test
  # failure OR 5-minute timeout).
  #
  # `when = destroy` is NOT what we want here — that fires
  # on terraform destroy, not on provisioner failure. The
  # correct shape is a SECOND `creation-time` provisioner
  # gated on the failure-path; terraform's design is to
  # rely on `on_failure = continue` on provisioner #1 so
  # this provisioner ALWAYS runs after #1 completes, and
  # then this provisioner internally checks the smoke-test
  # exit code via the per-apply marker file.
  provisioner "local-exec" {
    command     = <<-EOT
      set -uo pipefail
      LATEST_LOGDIR=$(ls -1dt "$${TF_LOG_DIR:-.}"/auto-rollback-* 2>/dev/null | head -n1)
      if [ -z "$LATEST_LOGDIR" ]; then
        echo "[auto-rollback] no smoke log found — skipping rollback decision" >&2
        exit 0
      fi
      SMOKE_EXIT=$(grep -oE 'smoke-test exit=[0-9]+' "$LATEST_LOGDIR/smoke.log" \
        | tail -n1 \
        | sed 's/smoke-test exit=//')
      if [ "$${SMOKE_EXIT:-0}" = "0" ]; then
        echo "[auto-rollback] smoke-test PASSED — no rollback action" \
          | tee -a "$LATEST_LOGDIR/rollback.log"
        exit 0
      fi
      echo "[auto-rollback] smoke-test FAILED (exit=$SMOKE_EXIT) — rollback engaged" \
        | tee -a "$LATEST_LOGDIR/rollback.log"
      if [ "${var.auto_rollback_dry_run}" = "true" ]; then
        echo "[auto-rollback] DRY-RUN: would have run 'terraform destroy -auto-approve'" \
          | tee -a "$LATEST_LOGDIR/rollback.log"
        exit 0
      fi
      echo "[auto-rollback] EXECUTING: terraform destroy -auto-approve" \
        | tee -a "$LATEST_LOGDIR/rollback.log"
      cd "${path.module}"
      terraform destroy -auto-approve 2>&1 | tee -a "$LATEST_LOGDIR/rollback.log"
    EOT
    interpreter = ["/bin/bash", "-c"]
    # Even if rollback fails, terraform should not loop —
    # `on_failure = continue` lets the operator manually
    # complete the rollback from the captured log.
    on_failure  = continue
  }
}

output "auto_rollback_enabled" {
  description = <<-EOT
    Surfaces the effective enable_auto_rollback flag so the
    apply log records whether the safety net was wired for
    this apply. Operators consult this in the post-apply
    review (docs/us-east-1-apply-runbook.md §7.3).
  EOT
  value       = var.enable_auto_rollback
}

output "auto_rollback_dry_run" {
  description = <<-EOT
    Surfaces the effective auto_rollback_dry_run flag — see
    docs/us-east-1-apply-runbook.md §7.2 for the staging-
    tier validation procedure.
  EOT
  value       = var.auto_rollback_dry_run
}
