# Phase K Wave 12 — Apone (DevOps).
#
# Per-region Route 53 records + Route 53 health checks for the
# 4-region prod-health-check matrix (W11) — `us-east-1`,
# `us-west-2`, `eu-west-1`, `ap-southeast-1`.
#
# W11 shipped the synthetic probe matrix against a SINGLE shared
# hostname (`mahjong.example.com`) — every region's runner hit
# the same Anycast endpoint and the regional signal came purely
# from the runner-to-edge distance. The W11 retro called out
# this as a "low-resolution" signal and committed W12 to ship
# **per-region R53 records** so:
#
#   1. Latency-based routing picks the closest healthy regional
#      endpoint for end-user traffic (no PoP RTT penalty for an
#      EU user hitting the US ALB).
#   2. The W11 probe matrix can be re-pointed at the per-region
#      hostnames (e.g. `eu-west-1.mahjong.example.com`) so each
#      probe leg hits the regional endpoint directly — a CDN PoP
#      outage in ap-southeast-1 now produces a localised strike
#      instead of an Anycast-masked false-green.
#   3. Route 53 health checks gate the latency-based RR set —
#      an unhealthy region's record is automatically pulled
#      from the rotation (clients fall through to the next-
#      closest healthy region).
#
# Wiring:
#
#   * `var.regional_endpoints` — operator passes one entry per
#     region with the regional ALB DNS name + Route 53 zone ID.
#     Empty list (default) = the module skips everything in this
#     file — backwards-compatible with the W11 baseline. The W12
#     prod env stack populates the list once the regional EKS
#     clusters cut over.
#   * `aws_route53_health_check.regional` — one HTTP health check
#     per region, polling `${REGION_HOSTNAME}/api/v1/health` over
#     HTTPS every 30 s. 3-failure threshold matches the W11
#     prod-health-check workflow's strike threshold so the two
#     signals stay in lock-step.
#   * `aws_route53_record.regional_alias` — per-region ALIAS A
#     record (e.g. `us-east-1.mahjong.example.com`) pointing at
#     that region's ALB. Used by the W11 matrix's
#     `vars.PROD_BASE_URL_<REGION>` operator variables.
#   * `aws_route53_record.latency_apex` — latency-based RR set on
#     the apex (e.g. `mahjong.example.com`) — one record per
#     region, each with `set_identifier = <region>`, gated by
#     the per-region health check via `health_check_id`. Route 53
#     picks the lowest-latency HEALTHY record for the client.
#
# Apex precedence:
#
#   When `var.regional_endpoints` is non-empty AND any region is
#   declared, the latency-based RR set takes over the apex —
#   `aws_route53_record.apex` (in main.tf) is skipped via its
#   own count expression updated to check `local.use_latency_apex`.
#   This file owns that flag.

locals {
  # Operator-driven flag — latency-based apex takes precedence
  # when at least one regional endpoint is supplied. Empty list
  # falls back to the W11 apex behaviour (ALIAS to single ALB or
  # CloudFront).
  use_latency_apex = length(var.regional_endpoints) > 0
}

# ── Per-region Route 53 health checks ────────────────────────────
#
# One HTTPS health check per region, polling the regional probe
# path. AWS Route 53 health checkers run from multiple AWS regions
# (operator-tunable via `regions` — default = AWS's pre-selected
# 8-region health-checker fan-out).
#
# Why HTTPS (not TCP):
#
#   * We need to verify the app responds 2xx — a TCP-only check
#     would mark a 5xx-storming ALB as healthy.
#   * The `/api/v1/health` endpoint is the same probe used by
#     the W11 prod-health-check matrix — keeping the two probes
#     identical means a degradation surfaces in both signals
#     simultaneously (RR auto-failover + GitHub issue).

resource "aws_route53_health_check" "regional" {
  for_each = { for r in var.regional_endpoints : r.region => r }

  type              = "HTTPS"
  fqdn              = each.value.hostname
  port              = 443
  resource_path     = "/api/v1/health"
  request_interval  = 30
  failure_threshold = 3

  measure_latency = true

  # `enable_sni` REQUIRED for HTTPS — the health checker presents
  # the SNI header so the ALB serves the correct cert. Without it
  # the checker gets a default cert (or a 421 Misdirected) and
  # the check silently fails open.
  enable_sni = true

  tags = merge(local.module_tags, {
    "Name"   = "${var.environment}-mahjong-${each.value.region}"
    "Region" = each.value.region
  })
}

# ── Per-region ALIAS A records ───────────────────────────────────
#
# `${region}.${domain_name}` → that region's regional ALB. Used
# by:
#
#   * The W11 prod-health-check matrix when the operator flips
#     each `vars.PROD_BASE_URL_<REGION>` from the global default
#     to the regional hostname.
#   * Direct operator access during a regional incident (e.g.
#     `curl https://us-west-2.mahjong.example.com/api/v1/health`
#     bypasses the latency-RR set + lets the operator probe the
#     specific region without DNS games).

resource "aws_route53_record" "regional_alias" {
  for_each = { for r in var.regional_endpoints : r.region => r }

  zone_id = local.hosted_zone_id
  name    = each.value.hostname
  type    = "A"

  alias {
    name                   = each.value.alb_dns_name
    zone_id                = each.value.alb_zone_id
    evaluate_target_health = true
  }
}

# ── Latency-based apex RR set ────────────────────────────────────
#
# One record per region on the SHARED apex `domain_name`. Route 53
# picks the lowest-latency HEALTHY record at resolution time
# (gated by the per-region health check). When all regions are
# healthy: the closest one wins. When a region is unhealthy: it's
# pulled from the rotation and the next-closest healthy region
# answers.
#
# `set_identifier` MUST be unique per record in a RR set — using
# the region string is the canonical pattern.
#
# Apex precedence vs `aws_route53_record.apex` (main.tf):
#
#   Both records target the apex `domain_name` with the same A
#   type. AWS Route 53 forbids overlapping records — the
#   `aws_route53_record.apex` resource MUST be skipped when
#   `local.use_latency_apex = true`. The flag is owned by THIS
#   file; `main.tf`'s apex resource references it via the
#   `local.use_latency_apex` check in its count expression
#   (updated in W12).

resource "aws_route53_record" "latency_apex" {
  for_each = { for r in var.regional_endpoints : r.region => r }

  zone_id = local.hosted_zone_id
  name    = var.domain_name
  type    = "A"

  set_identifier  = each.value.region
  health_check_id = aws_route53_health_check.regional[each.value.region].id

  latency_routing_policy {
    region = each.value.region
  }

  alias {
    name                   = each.value.alb_dns_name
    zone_id                = each.value.alb_zone_id
    evaluate_target_health = true
  }
}
