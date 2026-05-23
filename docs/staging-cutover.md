# Staging cutover — Edge module wired against the staging EKS ingress

> Phase K Wave 8 — Apone (DevOps).

Wave 7 shipped the [`edge` Terraform module](../infra/terraform/modules/edge/README.md)
unwired — it built the resources but no env stack instantiated it.
Wave 8 wires the module into a new `infra/terraform/envs/staging/`
stack and cuts staging over to the edge surface.

## 1. What changes

| Resource | Before W8 | After W8 |
|---|---|---|
| Staging DNS | Hand-written A record in the apex zone | `aws_route53_zone` (or alias record into the apex) managed by the `edge` module |
| Staging TLS | Manual ACM cert request per renewal | DNS-validated ACM cert, auto-renewed |
| Staging WAF | None | WAFv2 ACL in **count-only mode** on three managed rule groups + a 1000 req/IP/5min rate limit |
| Staging WAF logs | None | S3 bucket (`aws-waf-logs-staging-*`) + Athena workgroup |
| Cutover state | Tribal | `envs/staging/terraform.tfstate` (S3 backend) |

## 2. Apply order

The staging edge stack reads NO remote-state from the primary
stack — its only dependency is the staging EKS ingress controller
having published an ALB DNS name (passed in via tfvars).

```bash
# 0. Pre-flight: pre-create the state-backend bucket + DynamoDB
#    lock table in us-east-1. See backend.example.hcl in the env
#    directory.

# 1. Stand up the staging cluster (primary stack handles VPC + EKS
#    + RDS).
cd infra/terraform/
terraform init -backend-config=backend-staging.hcl
terraform plan  -var-file=staging.tfvars
terraform apply -var-file=staging.tfvars

# 2. Install the cluster baseline helm releases (ESO, AWS-LBC,
#    cert-manager, Kyverno, nginx-ingress).
# See docs/production-deployment-runbook.md §2 for the helm post-
# bootstrap sequence — the staging cutover uses the same sequence
# against the staging cluster.

# 3. Capture the ALB DNS name from the nginx-ingress controller:
kubectl -n ingress-nginx get svc ingress-nginx-controller \
    -o jsonpath='{.status.loadBalancer.ingress[0].hostname}'
# Save the hostname into envs/staging/terraform.tfvars (alb_dns_name).

# 4. Apply the staging edge stack.
cd infra/terraform/envs/staging/
cp backend.example.hcl backend.hcl
cp terraform.tfvars.example terraform.tfvars
# EDIT terraform.tfvars: alb_dns_name (from step 3), alb_zone_id
# (Z35SXDOTRQ7X7K for us-east-1 ALBs), domain_name (overrides the
# `staging.mahjong.example.com` default if your apex differs).
terraform init -backend-config=backend.hcl
terraform plan
terraform apply

# 5. If create_hosted_zone = true, copy the four NS records from
#    `terraform output hosted_zone_name_servers` into the apex
#    registrar (delegation).

# 6. Wait for DNS propagation + ACM validation (typically
#    < 5 min when the operator can write into the parent zone;
#    < 2 h when delegating to a child zone via the registrar).

# 7. Smoke-test (see §3).
```

## 3. Smoke test

End-to-end probe — the W8 acceptance criterion for staging
cutover is:

```bash
curl -fsSL https://staging.mahjong.example.com/healthz
```

Expected response:

```
HTTP/2 200
content-type: text/plain
ok
```

Detailed staged checks (run all on first cutover; re-run §3.1 +
§3.4 quarterly):

### 3.1 DNS resolves through the new zone

```bash
dig +short @1.1.1.1 staging.mahjong.example.com
# Expect: ALB DNS hostname OR an ALIAS that resolves to one.

dig +short NS staging.mahjong.example.com @1.1.1.1
# Expect: 4× ns-*.awsdns-* records (matches `terraform output
# hosted_zone_name_servers` exactly).
```

### 3.2 ACM cert is validated + presented by the ALB

```bash
# Cert ARN from the env's outputs:
ACM_ARN=$(terraform -chdir=infra/terraform/envs/staging \
    output -raw regional_acm_certificate_arn)
aws acm describe-certificate --certificate-arn "$ACM_ARN" \
    --query 'Certificate.Status'
# Expect: "ISSUED"

# Actual cert on the wire:
openssl s_client -connect staging.mahjong.example.com:443 \
    -servername staging.mahjong.example.com < /dev/null 2>/dev/null \
    | openssl x509 -noout -subject -dates
# Expect: CN matches the apex / SANs include the apex; NotAfter
# is > 30 days out (Amazon-issued certs auto-renew at 13mo).
```

### 3.3 WAF ACL is bound to the ALB

The edge module does NOT manage the `aws_wafv2_web_acl_association`
(the ALB lifecycle is the cluster's). The operator binds the ACL
once via:

```bash
WACL_ARN=$(terraform -chdir=infra/terraform/envs/staging \
    output -raw regional_web_acl_arn)
ALB_ARN=$(aws elbv2 describe-load-balancers \
    --query 'LoadBalancers[?contains(DNSName,`staging`)].LoadBalancerArn' \
    --output text)
aws wafv2 associate-web-acl \
    --web-acl-arn "$WACL_ARN" \
    --resource-arn "$ALB_ARN"
```

Verify the binding:

```bash
aws wafv2 get-web-acl-for-resource --resource-arn "$ALB_ARN" \
    --query 'WebACL.Name'
# Expect: "staging-mahjong-regional"
```

### 3.4 Healthz returns 200

```bash
curl -fsSL https://staging.mahjong.example.com/healthz
# Expect: HTTP/2 200 + body "ok".

# With verbose for first-cutover triage:
curl -v https://staging.mahjong.example.com/healthz 2>&1 | grep -E '^(<|>)' | head -30
```

### 3.5 WAF is COUNT-ONLY (NOT blocking)

W8 ships the staging WAF managed rule groups in count-only mode
so the rule sets prove they don't false-positive on real traffic
for one quarter BEFORE prod flips to blocking.

```bash
WAF_NAME="staging-mahjong-regional"
WAF_ID=$(aws wafv2 list-web-acls --scope REGIONAL \
    --query "WebACLs[?Name=='${WAF_NAME}'].Id" --output text)
WAF_LOCK=$(aws wafv2 list-web-acls --scope REGIONAL \
    --query "WebACLs[?Name=='${WAF_NAME}'].LockToken" --output text)
aws wafv2 get-web-acl --id "$WAF_ID" --name "$WAF_NAME" \
    --scope REGIONAL \
    --query 'WebACL.Rules[].{name:Name,override:OverrideAction}'
# Every managed-rule-group entry MUST show {"override": {"Count": {}}}.
# The custom rate-limit rule (priority 100) is the ONE rule that
# stays blocking even in staging.
```

The prod equivalent at `infra/terraform/main.tf` (or wherever the
prod edge module is wired) MUST keep `count_only = false` —
production stays blocking. This asymmetry is the W8 commitment
and is documented in `infra/terraform/envs/staging/main.tf`'s
`staging_waf_managed_rule_groups` local.

## 4. Rollback

The cutover is reversible:

```bash
# 1. Un-bind the WAF ACL from the ALB.
aws wafv2 disassociate-web-acl --resource-arn "$ALB_ARN"

# 2. Point the staging hostname back at the prior record (if any)
#    — either by directly editing the Route 53 record OR by
#    running `terraform destroy` against the staging env stack:
cd infra/terraform/envs/staging/
terraform destroy
```

`terraform destroy` releases the ACM cert + tears down the hosted
zone (if `create_hosted_zone = true`) + deletes the WAF ACL + S3
log bucket + Athena workgroup. The S3 logs bucket has
`force_destroy = false` so any non-empty lifecycle prefix blocks
the destroy — operator empties the bucket via
`aws s3 rm --recursive` if needed.

## 5. Promotion to production

The prod cutover is the SAME procedure with:

* `domain_name` set to the apex (e.g. `mahjong.example.com`)
* Every managed rule-group entry's `count_only = false`
  (blocking)
* `cloudfront.enabled = true` once Bishop's customer's geographic
  distribution warrants the CDN

The promotion is gated on the staging count-only rules running
clean (zero non-actionable counts) for one quarter — see
`docs/retro-2026-07.md` for the W8 commitment.

## 6. Cross-references

* [`infra/terraform/envs/staging/main.tf`](../infra/terraform/envs/staging/main.tf) — the env stack itself.
* [`infra/terraform/envs/staging/variables.tf`](../infra/terraform/envs/staging/variables.tf) — variable surface + defaults.
* [`infra/terraform/envs/staging/outputs.tf`](../infra/terraform/envs/staging/outputs.tf) — outputs consumed by the smoke-test runbook.
* [`infra/terraform/modules/edge/README.md`](../infra/terraform/modules/edge/README.md) — W7 edge module reference.
* [`docs/terraform.md`](terraform.md) §5 — edge module overview.
* [`docs/production-deployment-runbook.md`](production-deployment-runbook.md) — helm post-bootstrap sequence (steps shared with staging).
