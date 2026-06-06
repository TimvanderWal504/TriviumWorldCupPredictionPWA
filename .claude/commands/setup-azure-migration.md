# `/setup-azure-migration` — Scaffold Azure Deployment

**Role:** You are a backend infrastructure engineer tasked with scaffolding the AWS/AK12 → Azure migration for the Trivium World Cup 2026 prediction pool.

**Task:** Generate the Infrastructure-as-Code (Bicep), Azure-specific configuration files, and deployment documentation to move the application from self-hosted Docker Compose on AK12 (Proxmox) to Azure cloud with Container Apps, managed PostgreSQL, and GitHub Actions CI/CD.

**Deliverables:**
- `.infra/main.bicep` — parameterized Bicep template (ACR, Container Apps, Postgres, Key Vault)
- `.infra/main.parameters.json` — parameter template (user fills in names/choices)
- `src/TriviumWorldCup.Web/nginx/default.conf.azure` — Azure-specific nginx config (upstream repointed to internal ACA DNS)
- `.docs/AZURE_MIGRATION.md` — deployment guide (step-by-step for the user)
- `PROGRESS.md` — updated to reflect Azure migration as the next phase

---

## Phase 1: Codebase Read & Validation

**Before proposing anything, read these files in order:**

1. `docker-compose.yml` — understand the current multi-container setup (postgres, api, web, cloudflared, backup)
2. `src/TriviumWorldCup.Api/appsettings.json` — understand how the API loads secrets/connection strings (current Postgres endpoint)
3. `src/TriviumWorldCup.Web/nginx/default.conf` — understand the nginx upstream configuration, all proxy_pass routes
4. `.docs/ARCHITECTURE.md` (if exists) or `README.md` — understand the overall design
5. `PROGRESS.md` — understand the current project state and wave plan
6. `.github/workflows/deploy-azure.yml` — verify the workflow we created is present

**Validation:**
- Confirm that the API's database connection is configurable (env var or appsettings override).
- Confirm that nginx upstream `api` is currently set to `http://api:8080` or similar (Docker DNS).
- Confirm that the web + api + postgres are all on the same Docker network (check compose).
- Confirm that Postgres is a standard image with no custom modifications.

**If any of these files are missing or unclear, STOP and list what's ambiguous. Do not guess.**

---

## Phase 2: Generate Bicep Infrastructure Template

**Output file:** `.infra/main.bicep`

Generate a parameterized Bicep template that will:

1. **Create Azure Container Registry (ACR)**
   - Parameter: `acrName` (user provides, e.g., `triviumworldcupacr`)
   - SKU: Standard
   - Enable admin user (for docker login from CLI if needed)
   - Output: loginServer (e.g., `triviumworldcupacr.azurecr.io`)

2. **Create Log Analytics Workspace**
   - For Container Apps logs/monitoring
   - Auto-named based on resource group

3. **Create Azure Container Apps Environment**
   - Parameter: `acaEnvironmentName` (e.g., `twc-env`)
   - Tied to the Log Analytics workspace
   - **Internal DNS resolution:** services in the same environment reach each other by name (e.g., web reaches `api` at `http://api:8080`)

4. **Create two Container Apps within the environment:**

   **a) API Container App**
   - Name: parameter `apiAppName` (e.g., `twc-api`)
   - Image: parameter `apiImageTag` (default: `latest`)
   - Source: ACR (set up managed identity to pull)
   - Target port: 8080
   - Ingress: **internal only** (not publicly accessible)
   - Environment variables:
     - `ASPNETCORE_ENVIRONMENT=Production`
     - `FOOTBALL__APIKEY` — from Key Vault secret reference
   - Secrets (from Key Vault):
     - `ConnectionStrings__PostgreSQL` (the managed Postgres connection string)
   - CPU/memory: standard (.25 CPU, 0.5 GB for MVP; can adjust)
   - Scaling: 1 replica (no autoscale for MVP)

   **b) Web Container App**
   - Name: parameter `webAppName` (e.g., `twc-web`)
   - Image: parameter `webImageTag` (default: `latest`)
   - Source: ACR (same managed identity)
   - Target port: 80
   - Ingress: **external** (publicly accessible)
   - Environment variables:
     - `NODE_ENV=production` (or similar for the React vite build)
   - CPU/memory: same as API
   - Scaling: 1 replica
   - Output: external FQDN (e.g., `twc-web.niceorange-123.westeurope.azurecontainerapps.io`)

5. **Create Azure Database for PostgreSQL Flexible Server**
   - Parameter: `postgresAdminUser` (default: `postgres`)
   - Parameter: `postgresAdminPassword` (user must provide via secrets)
   - Parameter: `postgresSkuName` (e.g., `Standard_B1ms` for dev, user can override)
   - Database name: `triviumworldcup` (created automatically)
   - High availability: disabled (dev/MVP; user can enable in production)
   - Public access: disabled (only Azure resources can reach it; Container Apps reach via internal endpoint)
   - Output: fully-qualified connection string (e.g., `Server=twc-db-server.postgres.database.azure.com;Port=5432;Database=triviumworldcup;Username=postgres;...`)

6. **Create Azure Key Vault**
   - Parameter: `keyVaultName` (e.g., `twc-kv`)
   - Secrets stored:
     - `postgres-connection-string` (the managed Postgres output from above)
     - `football-api-key` (placeholder; user updates)
     - `vapid-public-key` and `vapid-private-key` (placeholder; user updates)
   - Access policy: grant Container Apps managed identity read permissions

7. **Outputs (display to user):**
   - ACR login server
   - Web app external FQDN (the public URL)
   - API app name (for internal referencing)
   - Postgres fully-qualified hostname + connection string
   - Key Vault name
   - Container Apps environment name

**Bicep Structure:**
- Top-level parameters section with descriptions and defaults
- Variables section (computed names, resource group location, etc.)
- Resources section (create in order: ACR → Log Analytics → ACA Environment → Postgres → Key Vault → Container Apps)
- Outputs section (all the URLs + connection strings the user needs)

**Key notes:**
- Use `Microsoft.ContainerRegistry/registries` for ACR
- Use `Microsoft.App/containerAppEnvironments` for ACA environment
- Use `Microsoft.App/containerApps` for the two apps (api + web)
- Use `Microsoft.DBforPostgreSQL/flexibleServers` for managed Postgres
- Use `Microsoft.KeyVault/vaults` for Key Vault
- All resource names should be concatenated with a prefix (e.g., `${resourcePrefix}-api`) to avoid conflicts
- Managed identities: use `Microsoft.ManagedIdentity/userAssignedIdentities` so ACA can authenticate to ACR without storing credentials

---

## Phase 3: Generate Azure nginx Configuration

**Output file:** `src/TriviumWorldCup.Web/nginx/default.conf.azure`

Create an Azure variant of the current `default.conf`. Key differences:

1. **Upstream repoint:** Instead of Docker DNS `http://api:8080`, point to the Container Apps internal address: `http://twc-api:8080` (where `twc-api` is the parameter `apiAppName`).
   - In the file, use a placeholder or variable: `set $api_backend http://<API_APP_NAME>:8080;`
   - Add a comment: `# On Azure Container Apps, internal services resolve by name within the environment`

2. **Keep resolver logic:** The `resolver 127.0.0.11 valid=10s ipv6=off;` from the current config may not apply on ACA (no Docker bridge), but it's harmless. You can simplify to just hostname resolution if needed, but a request-time resolver is still safe.

3. **Copy all routes from the current default.conf** (you read this in Phase 1): /api/*, /push, /e2e, static files, etc. Just swap the upstream reference.

4. **Document with a header comment:**
   ```
   # Azure Container Apps variant of default.conf
   # Key difference: api upstream is internal Container Apps DNS (twc-api)
   # rather than Docker Compose DNS (api).
   # Date generated: [today]
   ```

---

## Phase 4: Generate Deployment Guide

**Output file:** `.docs/AZURE_MIGRATION.md`

A step-by-step guide the user follows to deploy the infrastructure and application to Azure. Structure:

### 1. Prerequisites
- Azure subscription (with cost details, e.g., "this will cost ~$X/month for dev")
- Azure CLI installed (`az --version`)
- Docker installed locally (for testing image builds)
- Bicep CLI (comes with Azure CLI)
- GitHub account with repo access (already have)

### 2. Create a Resource Group (one-time, manual)
```
az group create --name <rg-name> --location <region>
```
(e.g., `az group create --name trivium-world-cup --location westeurope`)

### 3. Prepare Parameters
- Create `.infra/main.parameters.json` from the template
- Fill in: ACR name, ACA environment name, app names, Postgres admin password, region, SKU choices
- **Do NOT commit secrets to Git.** Use Azure Key Vault later.

### 4. Deploy Bicep
```
az deployment group create \
  --resource-group <rg-name> \
  --template-file .infra/main.bicep \
  --parameters @.infra/main.parameters.json
```

- This creates all Azure resources (ACR, ACA environment, two apps, managed Postgres, Key Vault)
- **Wait for deployment to complete** (5–10 min)
- Note the **outputs**: ACR login server, web app FQDN, Postgres connection string

### 5. Update the API Image with the Azure Postgres Connection String
- Copy the Postgres connection string from Bicep outputs
- Set it in the API Container App (via Azure Portal or CLI):
  ```
  az containerapp secret set \
    --name <api-app-name> \
    --resource-group <rg-name> \
    --secrets postgres-conn="Server=...;Port=5432;..."
  ```
- Update the app to reference the secret:
  ```
  az containerapp update \
    --name <api-app-name> \
    --resource-group <rg-name> \
    --secrets postgres-conn="Server=..." \
    --set-env-vars "ConnectionStrings__PostgreSQL=@postgres-conn"
  ```

### 6. Build and Push Images to ACR
In GitHub Actions (the workflow we created already handles this, but the user can also do it manually):
```
az acr login --name <acr-name>
docker buildx build -t <acr-name>.azurecr.io/twc-api:latest ./src/TriviumWorldCup.Api
docker push <acr-name>.azurecr.io/twc-api:latest
# (and same for web)
```

Or push via GitHub Actions (commit the code and the workflow pushes automatically).

### 7. Update Container Apps to Point to ACR
```
az containerapp update \
  --name twc-api \
  --resource-group <rg-name> \
  --image <acr-name>.azurecr.io/twc-api:latest

az containerapp update \
  --name twc-web \
  --resource-group <rg-name> \
  --image <acr-name>.azurecr.io/twc-web:latest
```

### 8. Set GitHub Secrets for CI/CD
In GitHub (repo Settings → Secrets and variables):
- `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (from Entra OIDC setup)
- `ACR_NAME`, `AZURE_RG`, `ACA_WEB_APP`, `ACA_API_APP` (as Variables)

(The GitHub Actions workflow we created uses these to build and deploy automatically on push to `main`.)

### 9. Test the Deployment
- Navigate to the web app's external FQDN (from Bicep outputs)
- Verify the app loads
- Check `/api/health` to confirm the API is reachable
- Sign in via mock auth (current; Entra comes later in TWC-20)
- Load fixtures/leaderboard to verify the database connection works

### 10. Next Steps
- (Optional) Set up Azure Application Insights for logging/monitoring
- (Optional) Enable autoscaling on Container Apps (e.g., 1–3 replicas based on CPU)
- (Future) Implement Entra ID integration (TWC-20 — MSAL on web + API, Entra app registration)
- (Optional) Custom domain (if not using the auto-generated `azurecontainerapps.io` domain)

### Troubleshooting
- **Container Apps not pulling image:** Check ACR managed identity permissions (should have AcrPull)
- **API can't connect to Postgres:** Verify the connection string is set in the app secrets; check Postgres firewall (should allow Azure resources)
- **Web app says "Cannot reach API":** Verify the nginx upstream points to `http://twc-api:8080` (not `http://api:8080`); check app logs in the Portal
- **Deployment hangs:** Check Azure Portal for resource creation status; look for quota/quota-exceeded errors

---

## Phase 5: Update PROGRESS.md

**Action:** Update the existing `PROGRESS.md` to reflect the Azure migration as the next phase.

- Add a new "Azure Migration" section (before Wave 7)
- Note that Wave 0–6 are complete on AK12; the codebase is ready for Azure
- List the deliverables: Bicep, deployment guide, nginx Azure config
- Indicate that the Azure deployment is **non-blocking** for MVP features (Waves 7–9 can proceed in parallel on AK12, then transition to Azure once deployed)
- Mark the following as **pending decisions:**
  - Entra app registration (for TWC-20)
  - Azure region + cost estimates
  - Custom domain (optional)
  - Monitoring/alerts (optional)

---

## Output Summary

**Files created:**
```
.infra/main.bicep
.infra/main.parameters.json
src/TriviumWorldCup.Web/nginx/default.conf.azure
.docs/AZURE_MIGRATION.md
PROGRESS.md (updated)
```

**What the user does next:**

1. **Fill in the parameters** in `.infra/main.parameters.json` (names, passwords, region, SKU choices)
2. **Run the Bicep deployment** (one `az deployment group create` command)
3. **Update the code** (connection string, nginx config) — or we can do this before step 2 if they want
4. **Commit to `main`** (Bicep, configs, nginx variant, updated PROGRESS.md)
5. **Set GitHub Secrets** (one-time, manual via GitHub UI)
6. **Push code** → GitHub Actions builds + pushes images → Container Apps updates automatically

**Timeline:**
- Setup (you): 30 min
- Bicep deployment: 5–10 min
- Code commit + GitHub Actions first build: 10 min
- Testing: 10 min
- Total: ~1 hour to go live on Azure

---

## Constraints & Assumptions

- **Assumes:** Docker images build successfully (no build errors)
- **Assumes:** Postgres connection is configurable via env var in appsettings.json
- **Assumes:** nginx config can be templated or swapped (we're generating an .azure variant)
- **Assumes:** The GitHub Actions workflow `.github/workflows/deploy-azure.yml` is already present and correct
- **Assumes:** User has Azure subscription + permissions to create resource groups and resources
- **Assumes:** User will NOT commit `.infra/main.parameters.json` with real passwords; they'll use Azure CLI or Key Vault for secrets

---

## Non-Blocking Notes

- Entra integration (TWC-20) is a future enhancement, not required for Azure deployment
- The AK12 deployment continues to work until the user is confident in Azure
- The Bicep can be re-run to update resource properties (e.g., scale up, add monitoring)
- Custom domain / DNS is optional; the auto-generated ACA FQDN is usable immediately
