# Phase K Wave 11 — Apone (DevOps).
#
# Example backend config for the prod edge + Redis stack.
# Pre-create the bucket + DynamoDB lock table in us-east-1
# BEFORE the first `terraform init`:
#
#   aws s3api create-bucket \
#       --bucket mahjong-tfstate-prod \
#       --region us-east-1
#   # us-east-1 buckets MUST NOT pass --create-bucket-configuration
#   # (AWS quirk — the default region is implicit).
#   aws s3api put-bucket-versioning \
#       --bucket mahjong-tfstate-prod \
#       --versioning-configuration Status=Enabled
#   aws s3api put-bucket-encryption \
#       --bucket mahjong-tfstate-prod \
#       --server-side-encryption-configuration '{
#           "Rules":[{"ApplyServerSideEncryptionByDefault":
#               {"SSEAlgorithm":"AES256"}}]}'
#   aws s3api put-bucket-public-access-block \
#       --bucket mahjong-tfstate-prod \
#       --public-access-block-configuration \
#           BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
#   aws dynamodb create-table \
#       --table-name mahjong-tflock-prod \
#       --attribute-definitions AttributeName=LockID,AttributeType=S \
#       --key-schema AttributeName=LockID,KeyType=HASH \
#       --billing-mode PAY_PER_REQUEST \
#       --region us-east-1
#
# Then:
#   cp backend.example.hcl backend.hcl     # edit if you change names
#   terraform init -backend-config=backend.hcl

bucket         = "mahjong-tfstate-prod"
key            = "infra/terraform/envs/prod/terraform.tfstate"
region         = "us-east-1"
dynamodb_table = "mahjong-tflock-prod"
encrypt        = true
