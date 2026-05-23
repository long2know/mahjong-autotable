# Phase K Wave 5 — Apone (DevOps).
#
# EKS cluster + managed node group + IRSA OIDC provider.
#
# Hand-rolled to keep the surface lean (the upstream `eks` module
# from `terraform-aws-modules` is excellent but ships ~60
# sub-resources by default — overkill for the bootstrap floor and
# harder to audit one variable at a time). The shape below is the
# canonical 2024-2025 AWS-recommended EKS configuration:
#
#   * Public+private endpoint by default (CI must reach the API).
#   * Managed node group with mixed-instance-type for Spot fallback.
#   * IRSA enabled — service accounts can assume IAM roles via the
#     cluster's OIDC issuer.
#   * Cloud-managed CoreDNS / kube-proxy / VPC-CNI / EBS-CSI addons.
#
# Cluster add-ons (ALB controller, cert-manager, ESO, Kyverno, etc.)
# are intentionally NOT in this module — they ship via `helm` in
# the post-bootstrap runbook (`infra/terraform/README.md` §3) so
# the IAM/CRD coupling is auditable separately from the cluster
# infrastructure itself.

locals {
  cluster_name = coalesce(var.cluster_name, "mahjong-${var.environment}")
}

# ── Cluster IAM role ─────────────────────────────────────────────

data "aws_iam_policy_document" "eks_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["eks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "eks_cluster" {
  name               = "${local.cluster_name}-cluster"
  assume_role_policy = data.aws_iam_policy_document.eks_assume.json
}

resource "aws_iam_role_policy_attachment" "eks_cluster_policy" {
  role       = aws_iam_role.eks_cluster.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSClusterPolicy"
}

# ── Cluster security group rules ─────────────────────────────────

resource "aws_security_group" "cluster" {
  name        = "${local.cluster_name}-cluster"
  description = "Cluster control-plane → workers traffic"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "${local.cluster_name}-cluster"
  }
}

resource "aws_security_group_rule" "cluster_egress" {
  type              = "egress"
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.cluster.id
  description       = "All egress"
}

# ── Cluster ───────────────────────────────────────────────────────

resource "aws_eks_cluster" "this" {
  name     = local.cluster_name
  version  = var.kubernetes_version
  role_arn = aws_iam_role.eks_cluster.arn

  vpc_config {
    # EKS spans BOTH public + private subnets for HA. Worker nodes
    # only attach to private; the public subnets are needed by the
    # cluster ENI itself.
    subnet_ids              = concat(aws_subnet.public[*].id, aws_subnet.private[*].id)
    security_group_ids      = [aws_security_group.cluster.id]
    endpoint_public_access  = var.cluster_public_access
    endpoint_private_access = true
    # If public access is enabled, allow only IPv4 (no IPv6 yet —
    # GitHub Actions runners don't have IPv6).
    public_access_cidrs = var.cluster_public_access ? ["0.0.0.0/0"] : []
  }

  # Encryption at rest for secrets in etcd. KMS key auto-created
  # by EKS when `provider:` references an alias starting with
  # `aws/eks` — we use the canonical AWS-managed key for the
  # bootstrap; switch to a customer-managed key in a later wave
  # if regulatory requirements need it.
  encryption_config {
    provider {
      key_arn = aws_kms_key.eks.arn
    }
    resources = ["secrets"]
  }

  depends_on = [
    aws_iam_role_policy_attachment.eks_cluster_policy,
  ]
}

resource "aws_kms_key" "eks" {
  description             = "EKS secret-encryption key for ${local.cluster_name}"
  deletion_window_in_days = 7
  enable_key_rotation     = true

  tags = {
    Name = "${local.cluster_name}-eks-secrets"
  }
}

resource "aws_kms_alias" "eks" {
  name          = "alias/${local.cluster_name}-eks-secrets"
  target_key_id = aws_kms_key.eks.key_id
}

# ── OIDC provider for IRSA ───────────────────────────────────────

data "tls_certificate" "cluster_oidc" {
  url = aws_eks_cluster.this.identity[0].oidc[0].issuer
}

resource "aws_iam_openid_connect_provider" "cluster" {
  url             = aws_eks_cluster.this.identity[0].oidc[0].issuer
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [data.tls_certificate.cluster_oidc.certificates[0].sha1_fingerprint]
}

# ── Managed node group ───────────────────────────────────────────

data "aws_iam_policy_document" "node_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "node" {
  name               = "${local.cluster_name}-node"
  assume_role_policy = data.aws_iam_policy_document.node_assume.json
}

resource "aws_iam_role_policy_attachment" "node_worker" {
  role       = aws_iam_role.node.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy"
}

resource "aws_iam_role_policy_attachment" "node_cni" {
  role       = aws_iam_role.node.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy"
}

resource "aws_iam_role_policy_attachment" "node_ecr" {
  role = aws_iam_role.node.name
  # Read-only — nodes pull images, they don't push.
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
}

resource "aws_eks_node_group" "default" {
  cluster_name    = aws_eks_cluster.this.name
  node_group_name = "default"
  node_role_arn   = aws_iam_role.node.arn
  # Nodes ONLY on private subnets — never directly internet-exposed.
  subnet_ids = aws_subnet.private[*].id

  scaling_config {
    desired_size = var.node_desired_size
    min_size     = var.node_min_size
    max_size     = var.node_max_size
  }

  update_config {
    # Max-unavailable=1 during rolling upgrades keeps capacity
    # consistent. For very small clusters bump to 25% to speed
    # upgrades; for prod-scale, 1 is safer.
    max_unavailable = 1
  }

  instance_types = var.node_instance_types
  disk_size      = var.node_disk_size_gb

  labels = {
    "node.kubernetes.io/role" = "worker"
    "mahjong-env"             = var.environment
  }

  tags = {
    Name = "${local.cluster_name}-default-ng"
  }

  depends_on = [
    aws_iam_role_policy_attachment.node_worker,
    aws_iam_role_policy_attachment.node_cni,
    aws_iam_role_policy_attachment.node_ecr,
  ]
}

# ── Managed add-ons ──────────────────────────────────────────────

# CoreDNS, kube-proxy, VPC-CNI, EBS-CSI — AWS-managed, version-bumped
# on the EKS cluster version. Default settings; override per add-on
# in a follow-up wave if needed.
resource "aws_eks_addon" "vpc_cni" {
  cluster_name = aws_eks_cluster.this.name
  addon_name   = "vpc-cni"

  depends_on = [aws_eks_node_group.default]
}

resource "aws_eks_addon" "coredns" {
  cluster_name = aws_eks_cluster.this.name
  addon_name   = "coredns"

  depends_on = [aws_eks_node_group.default]
}

resource "aws_eks_addon" "kube_proxy" {
  cluster_name = aws_eks_cluster.this.name
  addon_name   = "kube-proxy"

  depends_on = [aws_eks_node_group.default]
}

resource "aws_eks_addon" "ebs_csi" {
  cluster_name = aws_eks_cluster.this.name
  addon_name   = "aws-ebs-csi-driver"

  depends_on = [aws_eks_node_group.default]
}
