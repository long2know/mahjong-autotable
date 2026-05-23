# Phase K Wave 5 — Apone (DevOps).
#
# VPC module — hand-rolled (no aws-vpc upstream module) so the
# module surface stays minimal + auditable. Three public + three
# private subnets across the first three AZs of the chosen region.
# NAT gateway sized to one per AZ in prod for HA; single shared
# NAT in staging to halve the NAT egress bill.
#
# CIDR shape (with the default 10.0.0.0/16):
#
#   public subnets:  10.0.0.0/20, 10.0.16.0/20, 10.0.32.0/20
#   private subnets: 10.0.128.0/20, 10.0.144.0/20, 10.0.160.0/20
#
# The 64-IP gap between 10.0.32.0/20 and 10.0.128.0/20 is RESERVED
# for future dedicated DB subnets / VPC endpoints / privatelink.
# Renumbering subnets later is extremely disruptive (every existing
# ENI must be re-bound) — leave room from the start.

locals {
  vpc_name = "mahjong-${var.environment}"
  # Single NAT for staging / DR-warm; per-AZ for prod.
  nat_count = var.environment == "prod" ? length(local.azs) : 1
}

resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = local.vpc_name
  }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = {
    Name = "${local.vpc_name}-igw"
  }
}

# ── Subnets ───────────────────────────────────────────────────────

resource "aws_subnet" "public" {
  count = length(local.public_subnets)

  vpc_id                  = aws_vpc.this.id
  cidr_block              = local.public_subnets[count.index]
  availability_zone       = local.azs[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name = "${local.vpc_name}-public-${local.azs[count.index]}"
    # Required by AWS-LBC + the EKS-attached ALB controller so
    # ingress controllers can discover the public subnets.
    "kubernetes.io/role/elb"                                              = "1"
    "kubernetes.io/cluster/${coalesce(var.cluster_name, local.vpc_name)}" = "shared"
  }
}

resource "aws_subnet" "private" {
  count = length(local.private_subnets)

  vpc_id            = aws_vpc.this.id
  cidr_block        = local.private_subnets[count.index]
  availability_zone = local.azs[count.index]

  tags = {
    Name = "${local.vpc_name}-private-${local.azs[count.index]}"
    # Required by AWS-LBC for internal-LB subnet discovery.
    "kubernetes.io/role/internal-elb"                                     = "1"
    "kubernetes.io/cluster/${coalesce(var.cluster_name, local.vpc_name)}" = "shared"
  }
}

# ── NAT ───────────────────────────────────────────────────────────

resource "aws_eip" "nat" {
  count  = local.nat_count
  domain = "vpc"

  tags = {
    Name = "${local.vpc_name}-nat-eip-${count.index}"
  }

  depends_on = [aws_internet_gateway.this]
}

resource "aws_nat_gateway" "this" {
  count = local.nat_count

  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id

  tags = {
    Name = "${local.vpc_name}-nat-${count.index}"
  }

  depends_on = [aws_internet_gateway.this]
}

# ── Route tables ──────────────────────────────────────────────────

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.this.id
  }

  tags = {
    Name = "${local.vpc_name}-public-rt"
  }
}

resource "aws_route_table_association" "public" {
  count = length(aws_subnet.public)

  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table" "private" {
  count = length(local.private_subnets)

  vpc_id = aws_vpc.this.id

  # If single-NAT (staging), every private RT points at the only
  # NAT; if multi-NAT (prod), each AZ's private RT points at the
  # same-AZ NAT to avoid cross-AZ NAT egress charges.
  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.this[local.nat_count == 1 ? 0 : count.index].id
  }

  tags = {
    Name = "${local.vpc_name}-private-rt-${local.azs[count.index]}"
  }
}

resource "aws_route_table_association" "private" {
  count = length(aws_subnet.private)

  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

# ── VPC endpoints (cost-saver for RDS / S3 from EKS) ──────────────

# Gateway endpoint for S3 — eliminates NAT-egress charges for S3
# pulls (ECR layer cache, SBOM uploads, RDS backups streaming to
# the operator's bucket, etc.).
resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.this.id
  service_name      = "com.amazonaws.${var.region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = aws_route_table.private[*].id

  tags = {
    Name = "${local.vpc_name}-s3-endpoint"
  }
}
