# `infra/terraform/` — Mahjong Autotable AWS bootstrap

> Phase K Wave 5 — Apone (DevOps).

This module provisions the bare-minimum AWS footprint to host
`mahjong-autotable` on EKS + RDS Postgres + ECR with GitHub-Actions
OIDC federation. It is the "spin up a clean prod env in <30 min"
acceptance criterion that unblocks Wave-6 disaster-recovery
rehearsals and the future multi-region rollout.

## What this module provisions

| Resource | Default | Why |
|----------|---------|-----|
| 1 × VPC (`10.0.0.0/16` default) | 3 public + 3 private subnets across the first 3 AZs of the chosen region | Multi-AZ HA from day 1, with room (10.0.32.0/20 → 10.0.128.0/20 gap) for future DB / VPC-endpoint subnets without renumbering |
| EKS cluster (`mahjong-${environment}`) | 1.30 control plane, public+private endpoint, IRSA OIDC enabled | CI must reach the API; IRSA enables `ServiceAccount` → IAM-role federation for ESO + ALB controller etc. |
| Managed node group (`default`) | 3 × `t3.medium` (mixed-instance for Spot fallback), gp3 50 GB disks | One node per AZ floor; HPA scales to 10 |
| Cluster add-ons | `vpc-cni`, `coredns`, `kube-proxy`, `aws-ebs-csi-driver` (AWS-managed) | The bedrock; everything else (ALB controller, ESO, cert-manager, Kyverno) lands via helm in §3 |
| RDS Postgres (`mahjong-${environment}`) | `db.t4g.small` staging / `db.t4g.medium` prod, single-AZ staging / multi-AZ prod, gp3 20 GB→100 GB auto-scale, encrypted with a customer-managed KMS key | Single Postgres covers the app + tournament service; gp3 auto-scaling avoids midnight pager events |
| ECR repository (`mahjong-autotable`) | image-scan-on-push, lifecycle policy (keep last 30 tagged, expire untagged after 14 d) | AWS-native mirror for air-gapped pulls; primary registry remains GHCR |
| GitHub-Actions OIDC role (`mahjong-${environment}-github-deploy`) | scoped to `repo:long2know/mahjong-autotable` + main / `v*` / `environment:${env}` subjects | Federated cross-cloud auth without long-lived AWS access keys in GitHub Secrets |

## What this module deliberately does NOT provision

* **Cluster add-ons beyond AWS-managed** — ALB controller,
  cert-manager, External-Secrets-Operator, Kyverno — those land
  via `helm install` per §3 so the IAM/CRD coupling is auditable
  separately from the cluster infrastructure.
* **Route53 + ACM + WAF** — domain-bound, ship in a separate
  module once `mahjong.example.com` is registered.
* **S3 buckets for application state** — the app is stateless;
  replays + SBOMs + provenance ship to GHCR / Sigstore / GitHub
  Releases.
* **Multi-region replication** — out of scope for "<30 min clean
  env"; ships as a Wave-6+ extension.

## 1. Quick start

### 1.1 Pre-create the state backend (one-time per environment)

`terraform apply` cannot create the bucket it stores its own
state in (chicken-and-egg). Bootstrap the backend manually:

```bash
ENV=staging                                  # or prod / dr-us-west-2 / etc.
REGION=us-east-1
BUCKET=mahjong-tfstate-${ENV}
TABLE=mahjong-tflock-${ENV}

aws s3api create-bucket \
    --bucket "${BUCKET}" \
    --region "${REGION}"
aws s3api put-bucket-versioning \
    --bucket "${BUCKET}" \
    --versioning-configuration Status=Enabled
aws s3api put-bucket-encryption \
    --bucket "${BUCKET}" \
    --server-side-encryption-configuration '{
        "Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]
    }'
aws s3api put-public-access-block \
    --bucket "${BUCKET}" \
    --public-access-block-configuration '{
        "BlockPublicAcls":true,"IgnorePublicAcls":true,
        "BlockPublicPolicy":true,"RestrictPublicBuckets":true
    }'

aws dynamodb create-table \
    --table-name "${TABLE}" \
    --region "${REGION}" \
    --attribute-definitions AttributeName=LockID,AttributeType=S \
    --key-schema AttributeName=LockID,KeyType=HASH \
    --billing-mode PAY_PER_REQUEST
```

Then create `backend-${ENV}.hcl` (copy from `backend.example.hcl`):

```hcl
bucket         = "mahjong-tfstate-staging"
key            = "infra/terraform/staging.tfstate"
region         = "us-east-1"
dynamodb_table = "mahjong-tflock-staging"
encrypt        = true
```

### 1.2 Init + plan + apply

```bash
cd infra/terraform

ENV=staging
terraform init -backend-config=backend-${ENV}.hcl
terraform plan  -var-file=${ENV}.tfvars -out=${ENV}.tfplan
terraform apply ${ENV}.tfplan
```

Expect ~15-20 minutes (EKS cluster provisioning is the slow path).

### 1.3 Bind kubeconfig + smoke

```bash
$(terraform output -raw kubeconfig_update_command)
# e.g. → aws eks update-kubeconfig --region us-east-1 --name mahjong-staging --alias mahjong-staging

kubectl config use-context mahjong-staging
kubectl get nodes
# Expect: 3 Ready nodes
```

### 1.4 Seed the DB master password into SSM

```bash
ENV=staging
terraform output -raw db_master_password | \
    aws ssm put-parameter \
        --name "/mahjong/${ENV}/db/master_password" \
        --type SecureString --value file:///dev/stdin --overwrite
```

The application reads the connection string from
`mahjong/${ENV}/app` (the omnibus secret); update the omnibus
JSON to compose the password from the SSM SecureString.

## 2. Total stand-up time budget

| Step | Time |
|------|------|
| §1.1 backend bootstrap (one-time per env) | ~3 min |
| §1.2 `terraform init + plan` | ~1 min |
| §1.2 `terraform apply` (EKS is the bottleneck) | ~15-20 min |
| §1.3 kubeconfig + nodes Ready | ~1 min |
| §3 cluster add-ons (helm) | ~5 min |
| §4 first image push + Deployment | ~2 min |
| **TOTAL** | **~27-32 min** |

Within the Wave-6 "<30 min" acceptance criterion. The dominant
variable is EKS provisioning latency, which AWS controls.

## 3. Post-bootstrap cluster add-ons (helm, in order)

After `terraform apply` lands, install the cluster-side add-ons
the app + ops layer depends on. These are NOT in the terraform
module so the IAM/CRD coupling is auditable separately:

```bash
ENV=staging
CLUSTER=$(terraform output -raw cluster_name)
OIDC=$(terraform output -raw cluster_oidc_provider_arn)

# 1. External-Secrets-Operator (drives all the
#    ExternalSecret resources in infra/k8s/overlays/${ENV}/).
helm repo add external-secrets https://charts.external-secrets.io
helm repo update
helm install external-secrets external-secrets/external-secrets \
    --namespace external-secrets --create-namespace \
    --version 0.10.5

# 2. AWS-LBC (Ingress controller).
helm repo add eks https://aws.github.io/eks-charts
helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
    --namespace kube-system \
    --set clusterName="${CLUSTER}" \
    --set serviceAccount.create=true \
    --set serviceAccount.name=aws-load-balancer-controller

# 3. cert-manager (TLS certs).
helm repo add jetstack https://charts.jetstack.io
helm install cert-manager jetstack/cert-manager \
    --namespace cert-manager --create-namespace \
    --set installCRDs=true

# 4. Kyverno (cosign + SLSA admission policies — see infra/k8s/policies/).
helm repo add kyverno https://kyverno.github.io/kyverno/
helm install kyverno kyverno/kyverno \
    --namespace kyverno --create-namespace \
    --version 3.2.7
kubectl apply -f ../k8s/policies/kyverno-cosign-verify.yaml
```

Then apply the app overlay:

```bash
kubectl create namespace mahjong-${ENV}
kubectl apply -k ../k8s/overlays/${ENV}/
```

## 4. Mirror image from GHCR to ECR (optional)

For air-gapped pulls (account has no public egress to ghcr.io):

```bash
ECR=$(terraform output -raw ecr_repository_url)
TAG=v0.14.0

# Pull from GHCR.
docker pull ghcr.io/long2know/mahjong-autotable:${TAG}

# Re-tag + push to ECR.
docker tag ghcr.io/long2know/mahjong-autotable:${TAG} ${ECR}:${TAG}
aws ecr get-login-password --region us-east-1 | \
    docker login --username AWS --password-stdin ${ECR}
docker push ${ECR}:${TAG}
```

The cosign signature + SLSA provenance attached to the GHCR image
do NOT survive a `docker pull && docker push` to a different
registry — they're tied to the registry-and-digest tuple. For
ECR mirroring with signature preservation, use
[`crane copy`](https://github.com/google/go-containerregistry/blob/main/cmd/crane/doc/crane_copy.md)
or [`cosign copy`](https://docs.sigstore.dev/cosign/registry-copy/) instead.

## 5. Per-environment tfvars files

* `staging.tfvars` — cheap staging shape; multi-AZ off; deletion
  protection off.
* `prod.tfvars` — full HA shape; multi-AZ on; deletion protection
  on; tighter OIDC subject pinning.

To add a new environment (e.g. `dr-us-west-2`):

1. Copy `prod.tfvars` → `dr-us-west-2.tfvars`; edit `environment`,
   `region`, `vpc_cidr` (use a non-overlapping `/16` for future
   peering).
2. Copy `backend.example.hcl` → `backend-dr-us-west-2.hcl`; edit
   the four `EDITME` placeholders.
3. Pre-create the state bucket + lock table per §1.1.
4. `terraform init -backend-config=backend-dr-us-west-2.hcl`.
5. `terraform apply -var-file=dr-us-west-2.tfvars`.

Each environment has its own state file + lock table; modules
are stateless wrt each other so a `terraform destroy` on
staging cannot affect prod.

## 6. Teardown

```bash
ENV=staging
terraform destroy -var-file=${ENV}.tfvars
```

Prod teardown requires `db_deletion_protection = false` in the
tfvars file first (and a `terraform apply` to disable it) — the
two-step gate is intentional.

After `terraform destroy`, manually delete:

* The state bucket + lock table (terraform won't delete them
  because it can't unmount its own backend).
* Any manually-created SSM parameters under `/mahjong/${ENV}/*`.
* The KMS keys (in the 7-day deletion-pending state) if you
  want to immediately reclaim the alias names.

## 7. CI integration (Wave 6+, sketch)

```yaml
# Deploy workflow snippet (proposed; lands in a Wave-6 PR).
- name: Assume AWS deploy role
  uses: aws-actions/configure-aws-credentials@v4
  with:
    role-to-assume: arn:aws:iam::123456789012:role/mahjong-prod-github-deploy
    aws-region: us-east-1
- name: Sync EKS deployment
  run: |
    aws eks update-kubeconfig --region us-east-1 --name mahjong-prod
    kubectl apply -k infra/k8s/overlays/prod/
```

The role ARN to configure comes from
`terraform output github_deploy_role_arn`.

## 8. Cross-references

* [`infra/k8s/overlays/`](../k8s/overlays/) — kustomize overlays
  for `staging` + `prod` + `turn`.
* [`infra/k8s/policies/kyverno-cosign-verify.yaml`](../k8s/policies/kyverno-cosign-verify.yaml) — admission policy (cosign + SLSA attestation).
* [`docs/kubernetes.md`](../../docs/kubernetes.md) — the cluster-side runbook.
* [`docs/secret-management.md`](../../docs/secret-management.md) — ESO + SSM patterns.
* [`docs/slsa-provenance.md`](../../docs/slsa-provenance.md) — supply-chain runbook.
* [Hashicorp Terraform docs](https://developer.hashicorp.com/terraform/docs).
* [AWS EKS user guide](https://docs.aws.amazon.com/eks/latest/userguide/).
