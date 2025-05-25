#!/bin/bash

# ==== CONFIGURATION ====
GCP_PROJECT_ID="dark-bit-459802-t7"
REGION="us-east4"
GCP_REPO="wrapper-ehs-api-repo"
IMAGE_NAME="wrapper-ehs-api"
SERVICE_NAME="ehs-api-service"
GITHUB_REPO="praveen034u/VirtualHeathAPI-Medplum"  # Format: username/repo


# ==== SET PROJECT EXPLICITLY ====
echo "Setting GCP project..."
gcloud config set project $GCP_PROJECT_ID

# ==== CREATE SERVICE ACCOUNT ====
echo "Creating service account..."
gcloud iam service-accounts create github-actions-deployer \
  --display-name "GitHub Actions Deployer" \
  --project=$GCP_PROJECT_ID  # Explicitly specify the project.

# Add a delay here
echo "Waiting for service account to be created..."
sleep 10  # Wait for 10 seconds (adjust as needed)

# ==== GRANT ROLES ====
echo "Granting IAM roles..."
gcloud projects add-iam-policy-binding $GCP_PROJECT_ID \
  --member="serviceAccount:github-actions-deployer@$GCP_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/run.admin"

gcloud projects add-iam-policy-binding $GCP_PROJECT_ID \
  --member="serviceAccount:github-actions-deployer@$GCP_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/artifactregistry.writer"

gcloud projects add-iam-policy-binding $GCP_PROJECT_ID \
  --member="serviceAccount:github-actions-deployer@$GCP_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/aiplatform.user"

# ==== GENERATE KEY ====
echo "Creating service account key..."
gcloud iam service-accounts keys create key.json \
  --iam-account=github-actions-deployer@$GCP_PROJECT_ID.iam.gserviceaccount.com \
  --project=$GCP_PROJECT_ID # Explicitly specify the project

GCP_SA_KEY=$(base64 -w 0 key.json)

# ==== AUTHENTICATE GITHUB CLI ====
echo "Authenticating GitHub CLI..."
gh auth login --web
gh repo set-default $GITHUB_REPO

# ==== SET GITHUB SECRETS ====
echo "Setting GitHub secrets..."
gh secret set GCP_PROJECT_ID --body "$GCP_PROJECT_ID"
gh secret set REGION --body "$REGION"
gh secret set GCP_REPO --body "$GCP_REPO"
gh secret set IMAGE_NAME --body "$IMAGE_NAME"
gh secret set SERVICE_NAME --body "$SERVICE_NAME"
gh secret set VERTEX_AI_ENDPOINT --body "$VERTEX_AI_ENDPOINT"
gh secret set GCP_SA_KEY --body "$GCP_SA_KEY"

# ==== CLEANUP ====
rm key.json

echo "✅ GitHub Actions secrets configured successfully!"
