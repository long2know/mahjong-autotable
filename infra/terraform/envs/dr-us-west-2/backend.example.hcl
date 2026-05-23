# Phase K Wave 6 — Apone (DevOps).
#
# Example backend config for the DR env. Pre-create the bucket +
# DynamoDB table in us-west-2 BEFORE the first `terraform init`:
#
#   aws s3api create-bucket \
#       --bucket mahjong-tfstate-dr-us-west-2 \
#       --region us-west-2 \
#       --create-bucket-configuration LocationConstraint=us-west-2
#   aws s3api put-bucket-versioning \
#       --bucket mahjong-tfstate-dr-us-west-2 \
#       --versioning-configuration Status=Enabled
#   aws dynamodb create-table \
#       --table-name mahjong-tflock-dr-us-west-2 \
#       --attribute-definitions AttributeName=LockID,AttributeType=S \
#       --key-schema AttributeName=LockID,KeyType=HASH \
#       --billing-mode PAY_PER_REQUEST \
#       --region us-west-2
#
# Then:
#   cp backend.example.hcl backend.hcl   # edit if you change names
#   terraform init -backend-config=backend.hcl

bucket         = "mahjong-tfstate-dr-us-west-2"
key            = "infra/terraform/envs/dr-us-west-2/terraform.tfstate"
region         = "us-west-2"
dynamodb_table = "mahjong-tflock-dr-us-west-2"
encrypt        = true
