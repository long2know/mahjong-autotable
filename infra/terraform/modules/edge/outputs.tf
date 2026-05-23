# Phase K Wave 7 — Apone (DevOps).

output "hosted_zone_id" {
  description = "Route 53 hosted zone ID — passes through to the caller (e.g. for nested-record use)."
  value       = local.hosted_zone_id
}

output "hosted_zone_name_servers" {
  description = "NS records the registrar must point at. Empty list when the module did not create the zone."
  value       = var.create_hosted_zone ? aws_route53_zone.this[0].name_servers : []
}

output "regional_acm_certificate_arn" {
  description = "ARN of the validated regional ACM cert — bind this to your ALB's HTTPS listener."
  value       = aws_acm_certificate_validation.regional.certificate_arn
}

output "cloudfront_acm_certificate_arn" {
  description = "ARN of the validated CloudFront ACM cert (us-east-1). Empty when cloudfront.enabled = false."
  value       = var.cloudfront.enabled ? aws_acm_certificate_validation.cloudfront[0].certificate_arn : ""
}

output "regional_web_acl_arn" {
  description = "ARN of the regional WAFv2 ACL. The caller binds this to an ALB via `aws_wafv2_web_acl_association` (the module doesn't manage the ALB)."
  value       = aws_wafv2_web_acl.regional.arn
}

output "cloudfront_web_acl_arn" {
  description = "ARN of the CloudFront-scope WAFv2 ACL (us-east-1). Empty when cloudfront.enabled = false."
  value       = var.cloudfront.enabled ? aws_wafv2_web_acl.cloudfront[0].arn : ""
}

output "cloudfront_distribution_id" {
  description = "ID of the CloudFront distribution. Empty when cloudfront.enabled = false."
  value       = var.cloudfront.enabled ? aws_cloudfront_distribution.this[0].id : ""
}

output "cloudfront_distribution_domain_name" {
  description = "Domain name of the CloudFront distribution (e.g. `dXXXXX.cloudfront.net`). Empty when cloudfront.enabled = false."
  value       = var.cloudfront.enabled ? aws_cloudfront_distribution.this[0].domain_name : ""
}

output "waf_logs_bucket_name" {
  description = "S3 bucket holding WAF logs. Use as the data source location for the Athena workgroup."
  value       = aws_s3_bucket.waf_logs.bucket
}

output "waf_logs_bucket_arn" {
  description = "ARN of the WAF logs bucket."
  value       = aws_s3_bucket.waf_logs.arn
}

output "athena_workgroup_name" {
  description = "Athena workgroup name for WAF log queries."
  value       = aws_athena_workgroup.edge_logs.name
}

output "apex_fqdn" {
  description = "Fully-qualified domain name of the apex A record. Empty when neither CloudFront nor ALB DNS were supplied."
  value       = length(aws_route53_record.apex) > 0 ? aws_route53_record.apex[0].fqdn : ""
}
