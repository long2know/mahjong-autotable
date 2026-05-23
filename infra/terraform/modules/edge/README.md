# `edge/` — Route 53 + ACM + WAFv2 + (optional) CloudFront

> Phase K Wave 7 — Apone (DevOps).

Reusable Terraform module that provisions the **edge surface** for
`mahjong-autotable`:

| Layer | Resources |
|---|---|
| DNS | Route 53 hosted zone (opt-in) + ALIAS A record at apex |
| TLS | ACM certificate (DNS-validated) — regional + optional CloudFront |
| WAF | WAFv2 web ACL (REGIONAL + optional CLOUDFRONT) with AWS managed rule sets + per-IP rate limit |
| Logs | S3 bucket (`aws-waf-logs-*`) + Athena workgroup for query |
| CDN | (Optional) CloudFront distribution wired from caller's origin input |

The module is **domain-bound**: instantiate it from the primary env
(`infra/terraform/main.tf`) once `mahjong.example.com` (or the
real domain) is registered. It does NOT register the domain — that's a
manual operator step at the registrar.

## Usage

```hcl
# In `infra/terraform/main.tf` once the domain is registered.
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"
}

module "edge" {
  source = "./modules/edge"

  providers = {
    aws.us_east_1 = aws.us_east_1
  }

  environment = "prod"
  region      = "us-east-1"
  domain_name = "mahjong.example.com"
  additional_subject_alt_names = [
    "www.mahjong.example.com",
    "api.mahjong.example.com",
  ]

  # Bind to the ALB the EKS ALB-controller provisioned. Read these
  # from the ALB resource or a data source.
  alb_dns_name = aws_lb.public.dns_name
  alb_zone_id  = aws_lb.public.zone_id

  # CloudFront opt-out by default — apex points at the ALB
  # directly. Flip on once a CDN footprint is justified.
  cloudfront = {
    enabled                  = false
    origin_domain_name       = ""
    price_class              = "PriceClass_100"
    minimum_protocol_version = "TLSv1.2_2021"
  }

  common_tags = local.common_tags
}

# Bind the WAFv2 ACL to the ALB (done in the env-level main.tf —
# this module doesn't manage the ALB).
resource "aws_wafv2_web_acl_association" "alb" {
  resource_arn = aws_lb.public.arn
  web_acl_arn  = module.edge.regional_web_acl_arn
}
```

## Why a separate module (rather than inline in `main.tf`)?

* **Domain-bound surface.** This module only exists when the
  domain is registered. Inlining in `main.tf` would force every
  env (incl. `dr-us-west-2`) to carry a `var.create_edge` toggle
  + null-check every resource.
* **Two-region constraint.** CloudFront ACM certs MUST be issued
  in us-east-1 regardless of the rest of the stack's region.
  Modularising this forces the caller to plumb the
  `aws.us_east_1` alias explicitly (impossible to miss; fails
  fast on `terraform init` if the alias isn't passed).
* **Logging is the audit surface.** Vasquez's W7 audit anchor:
  `waf_logs_bucket_name` + `athena_workgroup_name` outputs are
  the canonical citation for "where do WAF blocks land".

## Inputs

See `variables.tf` for the authoritative list. The most common
inputs:

| Input | Why |
|-------|-----|
| `environment` | Resource tagging + name suffixes. |
| `domain_name` | Apex domain. Lowercase FQDN; validator rejects trailing dots. |
| `create_hosted_zone` | Set false when the registrar pre-created the zone. |
| `alb_dns_name` + `alb_zone_id` | Apex ALIAS targets when CloudFront is off. |
| `waf_managed_rule_groups` | Defaults to AWS Common + Known Bad Inputs + IP reputation (W7 baseline). |
| `waf_rate_limit_per_5min` | Per-IP rate limit; W7 baseline = 1000. |
| `logs_retention_days` | S3 lifecycle expiry; default 90d. |
| `cloudfront` | Opt-in CloudFront distribution wiring. |

## Outputs

| Output | Use |
|--------|-----|
| `hosted_zone_id` | Pass-through for nested-record use. |
| `hosted_zone_name_servers` | Point the registrar at these (NS-record handoff). |
| `regional_acm_certificate_arn` | Bind to your ALB's HTTPS listener. |
| `regional_web_acl_arn` | The caller binds via `aws_wafv2_web_acl_association`. |
| `cloudfront_*` | All empty unless `cloudfront.enabled`. |
| `waf_logs_bucket_name` + `athena_workgroup_name` | Audit anchors. |
| `apex_fqdn` | Resolved apex record FQDN. |

## Module invariants

1. **WAF logs bucket name MUST start with `aws-waf-logs-`.** AWS
   rejects WAF logging to any bucket without the prefix. The
   default `logs_bucket_name` derivation enforces this; if you
   override, the prefix is your responsibility.
2. **CloudFront cert is always in us-east-1.** AWS constraint.
   The `aws.us_east_1` provider alias is REQUIRED — passing it
   when `cloudfront.enabled = false` is fine (the alias just
   isn't used).
3. **Rate-limit rule is BLOCK action.** Block, not count. If a
   legitimate burst needs to bypass, lift the limit in the
   variable — don't switch the action to count. A blocked
   request is loud (4xx + WAF log entry); a counted request is
   silent.
4. **W7 baseline logs-retention is 90 days.** Validator clamps
   to [7, 3653]. The audit policy in `docs/secrets-scanning.md`
   currently requires ≥30 days of WAF logs for forensic
   reconstruction; raise the default if that minimum changes.

## DR rehearsal

The edge module is **single-region**. The DR module
(`modules/dr-replication/`) handles Route 53 failover records;
both modules share the SAME hosted zone but emit different
records (the DR module's `PRIMARY` + `SECONDARY` failover pair vs.
this module's `ALIAS A` at apex). When the DR module is also
present in the env stack:

* The edge module owns `<domain_name>` ALIAS A → ALB.
* The DR module owns the failover pair on a SEPARATE name
  (typically `app.<domain>` or `<domain>` itself with set_identifier
  flipping primary/secondary).

The two are independently `terraform destroy`-able; deleting one
does NOT cascade-delete the other's records.
