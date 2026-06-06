# Azure Migration Guide — Trivium World Cup 2026

Moves the app from self-hosted Docker Compose on AK12 to Azure:

| Component | AK12 (current) | Azure (target) |
|---|---|---|
| Web | nginx container, port 2026 | Container App (external ingress, HTTPS) |
| API | .NET container, internal | Container App (internal ingress) |
| Database | postgres:16-alpine container | Azure DB for PostgreSQL Flexible Server |
| Tunnel | Cloudflare Tunnel | Container Apps built-in HTTPS ingress |
| Images | Built on AK12 | Built by GitHub Actions, stored in ACR |

Estimated cost (West Europe, MVP SKUs): ~€40–60/month
(Standard_B1ms Postgres ~€15, two Container Apps at 1 replica each ~€20–30 depending on traffic)

---

## Prerequisites

- [ ] Azure subscription (Contributor access to a resource group)
- [ ] Azure CLI ≥ 2.60: `az --version`
- [ ] Bicep auto-installs with Azure CLI — verify with `az bicep version`
- [ ] Docker (for building/testing images locally if needed)
- [ ] GitHub repo access (for Actions CI/CD)

---

## Step 1 — Log in and create a resource group

```bash
az login
az account set --subscription "<SUBSCRIPTION_ID>"

az group create \
  --name trivium-world-cup \
  --location westeurope
```

---

## Step 2 — Prepare the nginx Azure config

The web Docker image proxies API requests via nginx. The Docker Compose image uses
`api:8080` (Docker DNS). The Azure image must use the ACA internal hostname instead.

**Update the web `Dockerfile`** to copy `default.conf.azure` instead of `default.conf`:

In `src/TriviumWorldCup.Web/Dockerfile`, find the line that copies the nginx config and
change it to point to the Azure variant:

```dockerfile
# Replace this:
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
# With this:
COPY nginx/default.conf.azure /etc/nginx/conf.d/default.conf
```

Or create a separate `Dockerfile.azure` that overrides just this line.

> If you use a single Dockerfile with a build argument (e.g. `ARG NGINX_CONF=default.conf`),
> pass `--build-arg NGINX_CONF=default.conf.azure` in the GitHub Actions workflow.

---

## Step 3 — Fill in deployment parameters

Copy and edit the parameter file (do **not** commit secrets):

```bash
cp .infra/main.parameters.json .infra/main.parameters.local.json
```

Edit `.infra/main.parameters.local.json` and replace every `<PLACEHOLDER>` value:

| Parameter | What to set |
|---|---|
| `acrName` | Globally unique, alphanumeric (e.g. `triviumwc2026acr`) |
| `postgresServerName` | Globally unique (e.g. `twc-db-2026`) |
| `keyVaultName` | 3–24 chars, globally unique (e.g. `twc-kv-2026`) |
| `postgresAdminPassword` | Strong password (≥8 chars, upper+lower+digit+symbol) |
| `adminUserId` | Your GUID from AK12 (from Portainer env `ADMIN_USER_ID`) |
| `footballApiKey` | Your API-Football key (or leave empty to disable ingestion) |
| `vapidPublicKey` | Copy from AK12 Portainer env `PUSH__VAPIDPUBLICKEY` |
| `vapidPrivateKey` | Copy from AK12 Portainer env `PUSH__VAPIDPRIVATEKEY` |
| `vapidSubject` | `mailto:timvanderwal504@hotmail.com` |

> The `postgresAdminPassword` in `main.parameters.json` shows a Key Vault reference pattern.
> For a fresh deployment where no Key Vault exists yet, set the password inline in
> `main.parameters.local.json` as `{ "value": "YourP@ssw0rd" }`. Add this file to `.gitignore`.

Add to `.gitignore`:
```
.infra/main.parameters.local.json
```

---

## Step 4 — Deploy Bicep (creates all Azure resources)

```bash
az deployment group create \
  --resource-group trivium-world-cup \
  --template-file .infra/main.bicep \
  --parameters @.infra/main.parameters.local.json \
  --verbose
```

This takes **5–12 minutes** (PostgreSQL Flexible Server is the slowest resource).

When complete, note the **outputs**:

```bash
# View outputs
az deployment group show \
  --resource-group trivium-world-cup \
  --name main \
  --query properties.outputs
```

Key outputs:
- `acrLoginServer` — e.g. `triviumwc2026acr.azurecr.io`
- `webAppFqdn` — e.g. `https://twc-web.niceorange-123.westeurope.azurecontainerapps.io`
- `apiAppInternalHostname` — the API's internal ACA hostname (used by nginx)
- `postgresHostname` — e.g. `twc-db-2026.postgres.database.azure.com`

---

## Step 5 — Verify the nginx upstream matches the API hostname

The nginx Azure config (`default.conf.azure`) has:
```nginx
set $api_backend http://twc-api;
```

The `twc-api` hostname is the API Container App name. Within the same ACA environment,
apps resolve each other by their app name. If you changed `apiAppName` in the parameters,
update `default.conf.azure` to match before building the image.

Check the `apiAppInternalHostname` output — the short name before the first `.` is what nginx uses.

---

## Step 6 — Set up GitHub Secrets and Variables

In GitHub → repo **Settings → Secrets and variables → Actions**:

**Secrets** (encrypted, not visible after entry):
| Name | Value |
|---|---|
| `AZURE_CLIENT_ID` | From step 7 (OIDC service principal) |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |

**Variables** (visible, not sensitive):
| Name | Value |
|---|---|
| `ACR_NAME` | e.g. `triviumwc2026acr` (without `.azurecr.io`) |
| `AZURE_RG` | `trivium-world-cup` |
| `ACA_WEB_APP` | `twc-web` |
| `ACA_API_APP` | `twc-api` |

---

## Step 7 — Create the GitHub OIDC service principal (one-time)

This lets GitHub Actions authenticate to Azure without storing a client secret.

```bash
# Get the subscription + tenant IDs
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

# Create a service principal for GitHub Actions
az ad app create --display-name "twc-github-actions"
APP_ID=$(az ad app list --display-name "twc-github-actions" --query [0].appId -o tsv)
az ad sp create --id $APP_ID

# Assign Contributor on the resource group
az role assignment create \
  --assignee $APP_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/trivium-world-cup

# Add AcrPush role (so it can push images)
ACR_ID=$(az acr show --name <ACR_NAME> --resource-group trivium-world-cup --query id -o tsv)
az role assignment create \
  --assignee $APP_ID \
  --role AcrPush \
  --scope $ACR_ID

# Add federated credential for GitHub Actions
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-actions",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:TimvanderWal504/TriviumWorldCupPredictionPWA:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

echo "AZURE_CLIENT_ID = $APP_ID"
echo "AZURE_TENANT_ID = $TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID = $SUBSCRIPTION_ID"
```

Save the three values as GitHub Secrets (Step 6).

---

## Step 8 — First deployment via GitHub Actions

Commit and push to `main`:

```bash
git add .infra/ src/TriviumWorldCup.Web/nginx/default.conf.azure
git commit -m "Add Azure infrastructure (Bicep, nginx Azure config)"
git push origin main
```

GitHub Actions (`.github/workflows/deploy-azure.yml`) will:
1. Build the API and Web Docker images
2. Push them to ACR with the commit SHA tag
3. Update both Container Apps to run the new images

Monitor the run in GitHub → Actions.

---

## Step 9 — Test the deployment

1. Navigate to `webAppFqdn` from the Bicep outputs.
2. Sign in using your personal login link (link auth is active in Production).
3. Check that fixtures and leaderboard load (confirms DB connection).
4. Check the API health endpoint: `https://<webAppFqdn>/api/health` → `{ "status": "healthy" }`.

> **First run note:** Marten runs schema migrations on startup. If the API takes >30 seconds
> to respond on the first request, that is normal — wait for it.

---

## Step 10 — Seed the admin user (first run only)

The admin `InviteUser` is seeded from `ADMIN_USER_ID` when the database is empty.
If the DB was already seeded without this env var set, create the user manually:

1. Open the web app at `webAppFqdn`.
2. Navigate to `<webAppFqdn>/auth/link/login?id=<YOUR_ADMIN_USER_ID>` to sign in as admin.
3. Go to Me → Admin → Users → Create user (or confirm your record already exists).

---

## Troubleshooting

**Container App fails to pull image**
- Check that the managed identity has `AcrPull` on the ACR (Bicep creates this automatically).
- Run `az containerapp show --name twc-api --resource-group trivium-world-cup` and inspect the identity section.

**API can't connect to PostgreSQL**
- The firewall rule `AllowAllAzureServices` (0.0.0.0 → 0.0.0.0) allows Azure-originated traffic.
- Check that `ASPNETCORE_ENVIRONMENT=Production` is set (it is in the Bicep).
- Check the connection string: `az containerapp secret show --name twc-api ...` (not available directly — check logs instead).

**Web app returns "upstream not found" or 502**
- The nginx `$api_backend` in `default.conf.azure` must match the API app name.
- Check `az containerapp show --name twc-api --resource-group trivium-world-cup --query properties.configuration.ingress.fqdn`.
- Within the same ACA environment, the short name (before the first dot) is the resolvable hostname.

**Deployment hangs on PostgreSQL**
- Flexible Server provisioning takes 5–10 minutes. This is normal.
- Check the Azure Portal → trivium-world-cup resource group for the server status.

---

## Next steps (post-migration)

- **Entra ID (TWC-20):** When the Entra app registration is ready, add `EntraIdentityProvider`, set `Auth__Provider=entra` in the Container App env vars. No infrastructure change needed.
- **Custom domain:** `az containerapp hostname add` — point your DNS CNAME at the ACA default hostname.
- **Autoscaling:** Update `scale.maxReplicas` in the Bicep (or via Portal) once you know your traffic pattern.
- **Monitoring:** Add Application Insights to the ACA environment for distributed tracing.
- **Backups:** Azure PostgreSQL Flexible Server has built-in 7-day point-in-time restore. The nightly `backup.sh` container from Docker Compose is no longer needed.
