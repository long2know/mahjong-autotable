# Phase K Wave 7 — Apone (DevOps).
#
# Edge module — Route 53 hosted zone + records, ACM cert (DNS
# validated), WAFv2 web ACL with managed rule sets + custom rate
# limit, S3 logging bucket + Athena workgroup for query, and an
# optional CloudFront distribution wired from inputs.
#
# Layout:
#
#   * Route 53 zone / records  — `aws.default` provider.
#   * ACM (regional)           — `aws.default` provider; bound to the
#                                regional ALB.
#   * ACM (CloudFront, opt-in) — `aws.us_east_1` provider; AWS
#                                requires CloudFront certs in
#                                us-east-1.
#   * WAFv2 (REGIONAL scope)   — `aws.default` provider; bound to
#                                the ALB. ALB binding (the
#                                `aws_wafv2_web_acl_association`)
#                                lives in the CALLER (env-level
#                                main.tf) because the module
#                                doesn't manage the ALB.
#   * WAFv2 (CLOUDFRONT scope) — `aws.us_east_1` provider when
#                                cloudfront.enabled (AWS constraint
#                                for CloudFront-attached ACLs).
#   * S3 bucket for WAF logs   — `aws.default` provider. Bucket
#                                name MUST start with `aws-waf-logs-`
#                                (AWS constraint).
#   * Athena workgroup         — `aws.default` provider. Result
#                                location is a sub-prefix of the
#                                same S3 bucket so analysts don't
#                                need a second bucket.

locals {
  module_tags = merge(var.common_tags, {
    "Module"      = "edge"
    "Wave"        = "phase-k-wave-7"
    "Environment" = var.environment
  })

  # Bucket name: operator-supplied or auto. AWS requires the
  # `aws-waf-logs-` prefix when the bucket receives WAF logs.
  logs_bucket_name = (
    var.logs_bucket_name != ""
    ? var.logs_bucket_name
    : "aws-waf-logs-${var.environment}-${replace(var.domain_name, ".", "-")}"
  )

  athena_workgroup_name = (
    var.athena_workgroup_name != ""
    ? var.athena_workgroup_name
    : "mahjong-edge-${var.environment}"
  )

  hosted_zone_id = (
    var.create_hosted_zone
    ? (length(aws_route53_zone.this) > 0 ? aws_route53_zone.this[0].zone_id : "")
    : var.existing_hosted_zone_id
  )
}

# ── Route 53 hosted zone ─────────────────────────────────────────

resource "aws_route53_zone" "this" {
  count = var.create_hosted_zone ? 1 : 0
  name  = var.domain_name

  tags = local.module_tags

  # Don't auto-create a comment that would change on every plan.
  comment = "Managed by mahjong-autotable edge module (W7)"
}

# ── ACM cert (regional) ───────────────────────────────────────────
#
# Used by the ALB / API Gateway / NLB the regional WAFv2 ACL is
# attached to. DNS-validated. Lifecycle `create_before_destroy` so
# a rename / SAN-list change doesn't kick the ALB off-cert.

resource "aws_acm_certificate" "regional" {
  domain_name               = var.domain_name
  subject_alternative_names = var.additional_subject_alt_names
  validation_method         = "DNS"

  tags = merge(local.module_tags, {
    "Name" = "${var.environment}-${var.domain_name}-regional"
  })

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_route53_record" "regional_acm_validation" {
  for_each = {
    for dvo in aws_acm_certificate.regional.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  }

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = local.hosted_zone_id
}

resource "aws_acm_certificate_validation" "regional" {
  certificate_arn         = aws_acm_certificate.regional.arn
  validation_record_fqdns = [for r in aws_route53_record.regional_acm_validation : r.fqdn]
}

# ── ACM cert (CloudFront — us-east-1 ONLY) ────────────────────────
#
# CloudFront-attached ACM certs MUST live in us-east-1 (AWS
# constraint). Provisioned only when cloudfront.enabled.

resource "aws_acm_certificate" "cloudfront" {
  count = var.cloudfront.enabled ? 1 : 0

  provider                  = aws.us_east_1
  domain_name               = var.domain_name
  subject_alternative_names = var.additional_subject_alt_names
  validation_method         = "DNS"

  tags = merge(local.module_tags, {
    "Name" = "${var.environment}-${var.domain_name}-cloudfront"
  })

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_route53_record" "cloudfront_acm_validation" {
  for_each = var.cloudfront.enabled ? {
    for dvo in aws_acm_certificate.cloudfront[0].domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  } : {}

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = local.hosted_zone_id
}

resource "aws_acm_certificate_validation" "cloudfront" {
  count = var.cloudfront.enabled ? 1 : 0

  provider                = aws.us_east_1
  certificate_arn         = aws_acm_certificate.cloudfront[0].arn
  validation_record_fqdns = [for r in aws_route53_record.cloudfront_acm_validation : r.fqdn]
}

# ── S3 bucket for WAF logs ────────────────────────────────────────

resource "aws_s3_bucket" "waf_logs" {
  bucket        = local.logs_bucket_name
  force_destroy = false

  tags = merge(local.module_tags, {
    "Name"    = local.logs_bucket_name
    "Purpose" = "waf-logs"
  })
}

resource "aws_s3_bucket_ownership_controls" "waf_logs" {
  bucket = aws_s3_bucket.waf_logs.id
  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_public_access_block" "waf_logs" {
  bucket                  = aws_s3_bucket.waf_logs.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "waf_logs" {
  bucket = aws_s3_bucket.waf_logs.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_versioning" "waf_logs" {
  bucket = aws_s3_bucket.waf_logs.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "waf_logs" {
  bucket = aws_s3_bucket.waf_logs.id

  rule {
    id     = "expire-after-retention"
    status = "Enabled"

    filter {}

    expiration {
      days = var.logs_retention_days
    }

    noncurrent_version_expiration {
      noncurrent_days = 30
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}

# ── WAFv2 web ACL (REGIONAL) ──────────────────────────────────────
#
# Bound to the regional ALB. The actual association resource
# (`aws_wafv2_web_acl_association`) is the caller's responsibility
# because this module doesn't manage the ALB.

resource "aws_wafv2_web_acl" "regional" {
  name        = "${var.environment}-mahjong-regional"
  description = "Edge WAF for mahjong-autotable (${var.environment}, regional). Managed rule sets + per-IP rate limit."
  scope       = "REGIONAL"

  default_action {
    allow {}
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "${var.environment}-mahjong-regional"
    sampled_requests_enabled   = true
  }

  # Managed rule sets — block-action by default; count-only mode when
  # `count_only = true` (useful for canarying a new rule).
  dynamic "rule" {
    for_each = { for r in var.waf_managed_rule_groups : r.name => r }
    content {
      name     = rule.value.name
      priority = rule.value.priority

      override_action {
        # `none` = use the rule's default action (block); `count` =
        # log only (no block). Mutually exclusive — exactly one
        # nested block.
        dynamic "none" {
          for_each = rule.value.count_only ? [] : [1]
          content {}
        }
        dynamic "count" {
          for_each = rule.value.count_only ? [1] : []
          content {}
        }
      }

      statement {
        managed_rule_group_statement {
          name        = rule.value.name
          vendor_name = "AWS"
        }
      }

      visibility_config {
        cloudwatch_metrics_enabled = true
        metric_name                = rule.value.name
        sampled_requests_enabled   = true
      }
    }
  }

  # W7 custom rate-limit rule — 1000 req per IP per 5min.
  rule {
    name     = "rate-limit-per-ip-5min"
    priority = 100

    action {
      block {}
    }

    statement {
      rate_based_statement {
        limit              = var.waf_rate_limit_per_5min
        aggregate_key_type = "IP"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "rate-limit-per-ip-5min"
      sampled_requests_enabled   = true
    }
  }

  tags = local.module_tags
}

# ── WAFv2 web ACL (CLOUDFRONT) — opt-in ───────────────────────────

resource "aws_wafv2_web_acl" "cloudfront" {
  count = var.cloudfront.enabled ? 1 : 0

  provider    = aws.us_east_1
  name        = "${var.environment}-mahjong-cloudfront"
  description = "Edge WAF for mahjong-autotable (${var.environment}, CloudFront)."
  scope       = "CLOUDFRONT"

  default_action {
    allow {}
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "${var.environment}-mahjong-cloudfront"
    sampled_requests_enabled   = true
  }

  dynamic "rule" {
    for_each = { for r in var.waf_managed_rule_groups : r.name => r }
    content {
      name     = rule.value.name
      priority = rule.value.priority

      override_action {
        dynamic "none" {
          for_each = rule.value.count_only ? [] : [1]
          content {}
        }
        dynamic "count" {
          for_each = rule.value.count_only ? [1] : []
          content {}
        }
      }

      statement {
        managed_rule_group_statement {
          name        = rule.value.name
          vendor_name = "AWS"
        }
      }

      visibility_config {
        cloudwatch_metrics_enabled = true
        metric_name                = rule.value.name
        sampled_requests_enabled   = true
      }
    }
  }

  rule {
    name     = "rate-limit-per-ip-5min"
    priority = 100

    action {
      block {}
    }

    statement {
      rate_based_statement {
        limit              = var.waf_rate_limit_per_5min
        aggregate_key_type = "IP"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "rate-limit-per-ip-5min"
      sampled_requests_enabled   = true
    }
  }

  tags = local.module_tags
}

# ── WAFv2 logging configuration ───────────────────────────────────
#
# WAF logs every match to the S3 bucket. The destination ARN
# format is `arn:aws:s3:::<bucket-name>`.

resource "aws_wafv2_web_acl_logging_configuration" "regional" {
  log_destination_configs = [aws_s3_bucket.waf_logs.arn]
  resource_arn            = aws_wafv2_web_acl.regional.arn

  # Redact sensitive fields BEFORE they hit S3 (the obvious one is
  # the Authorization header; PII in URI paths is harder to predict
  # but Authorization is the deterministic win).
  redacted_fields {
    single_header {
      name = "authorization"
    }
  }
  redacted_fields {
    single_header {
      name = "cookie"
    }
  }
}

resource "aws_wafv2_web_acl_logging_configuration" "cloudfront" {
  count = var.cloudfront.enabled ? 1 : 0

  provider                = aws.us_east_1
  log_destination_configs = [aws_s3_bucket.waf_logs.arn]
  resource_arn            = aws_wafv2_web_acl.cloudfront[0].arn

  redacted_fields {
    single_header {
      name = "authorization"
    }
  }
  redacted_fields {
    single_header {
      name = "cookie"
    }
  }
}

# ── Athena workgroup for log queries ──────────────────────────────

resource "aws_athena_workgroup" "edge_logs" {
  name = local.athena_workgroup_name

  configuration {
    enforce_workgroup_configuration    = true
    publish_cloudwatch_metrics_enabled = true

    result_configuration {
      # Results land in a sub-prefix of the same bucket so the
      # operator doesn't need a second bucket. Athena requires the
      # result location be empty or non-existing when the workgroup
      # is created.
      output_location = "s3://${aws_s3_bucket.waf_logs.bucket}/athena-results/"
      encryption_configuration {
        encryption_option = "SSE_S3"
      }
    }
  }

  # `state = ENABLED` is the default; set explicitly so Terraform
  # plans show the value rather than an empty.
  state         = "ENABLED"
  force_destroy = false

  tags = merge(local.module_tags, {
    "Purpose" = "waf-log-query"
  })
}

# ── CloudFront distribution (opt-in) ──────────────────────────────

resource "aws_cloudfront_distribution" "this" {
  count = var.cloudfront.enabled ? 1 : 0

  enabled         = true
  is_ipv6_enabled = true
  price_class     = var.cloudfront.price_class
  web_acl_id      = aws_wafv2_web_acl.cloudfront[0].arn
  aliases         = concat([var.domain_name], var.additional_subject_alt_names)

  origin {
    domain_name = var.cloudfront.origin_domain_name
    origin_id   = "mahjong-${var.environment}-origin"

    custom_origin_config {
      http_port                = 80
      https_port               = 443
      origin_protocol_policy   = "https-only"
      origin_ssl_protocols     = ["TLSv1.2"]
      origin_read_timeout      = 60
      origin_keepalive_timeout = 60
    }
  }

  default_cache_behavior {
    target_origin_id       = "mahjong-${var.environment}-origin"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS", "PUT", "PATCH", "POST", "DELETE"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    # Forward everything — SignalR's negotiate / connect handshake
    # is cookie-bearing and Authorization-bearing; CloudFront's
    # `Managed-CachingDisabled` policy is the right fit for a
    # WebSocket-fronting distribution.
    cache_policy_id            = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad" # Managed-CachingDisabled
    origin_request_policy_id   = "216adef6-5c7f-47e4-b989-5492eafa07d3" # Managed-AllViewer
    response_headers_policy_id = "67f7725c-6f97-4210-82d7-5512b31e9d03" # Managed-SecurityHeadersPolicy
  }

  viewer_certificate {
    acm_certificate_arn      = aws_acm_certificate_validation.cloudfront[0].certificate_arn
    ssl_support_method       = "sni-only"
    minimum_protocol_version = var.cloudfront.minimum_protocol_version
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  tags = local.module_tags
}

# ── Apex Route 53 record ─────────────────────────────────────────
#
# When CloudFront enabled: ALIAS A → distribution.
# When CloudFront disabled + alb_dns_name supplied: ALIAS A → ALB.
# When both disabled: NO record (operator-driven).

resource "aws_route53_record" "apex" {
  count   = (var.cloudfront.enabled || var.alb_dns_name != "") ? 1 : 0
  zone_id = local.hosted_zone_id
  name    = var.domain_name
  type    = "A"

  alias {
    name = (
      var.cloudfront.enabled
      ? aws_cloudfront_distribution.this[0].domain_name
      : var.alb_dns_name
    )
    zone_id = (
      var.cloudfront.enabled
      ? aws_cloudfront_distribution.this[0].hosted_zone_id
      : var.alb_zone_id
    )
    evaluate_target_health = !var.cloudfront.enabled
  }
}
