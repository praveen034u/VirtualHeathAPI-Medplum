# VirtualHeathAPI-Medplum

$ gcloud auth login

gcloud artifacts repositories create virtual-health-api-repo --repository-format=docker --location=us-east4

# run below command in google cloud shell
  gcloud projects add-iam-policy-binding dark-bit-459802-t7 \
  --member="serviceAccount:github-actions-deployer@dark-bit-459802-t7.iam.gserviceaccount.com" \
  --role="roles/artifactregistry.writer"

# run below command in google cloud shell
export PROJECT_ID="dark-bit-459802-t7"
export DEPLOYER_SA="github-actions-deployer@$PROJECT_ID.iam.gserviceaccount.com"
export COMPUTE_SA="$(gcloud projects describe $PROJECT_ID --format='value(projectNumber)')-compute@developer.gserviceaccount.com"

gcloud iam service-accounts add-iam-policy-binding $COMPUTE_SA \
  --member="serviceAccount:$DEPLOYER_SA" \
  --role="roles/iam.serviceAccountUser"

# git clone the project in Google Cloud IDE and then run the below command.
# run below command in google cloud shell
chmod +x setup_github_secrets.sh
./setup_github_secrets.sh
