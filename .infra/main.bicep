// Trivium World Cup 2026 — Azure Infrastructure
// Deploys: ACR · Log Analytics · Container Apps Environment ·
//          PostgreSQL Flexible Server · Key Vault · API + Web Container Apps
//
// Deploy:
//   az deployment group create \
//     --resource-group <rg> \
//     --template-file .infra/main.bicep \
//     --parameters @.infra/main.parameters.json

// ── Parameters ────────────────────────────────────────────────────────────────

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Azure Container Registry name (globally unique, alphanumeric only).')
param acrName string

@description('Container Apps Environment name.')
param acaEnvironmentName string = 'twc-env'

@description('API Container App name (internal ingress).')
param apiAppName string = 'twc-api'

@description('Web Container App name (external ingress).')
param webAppName string = 'twc-web'

@description('API image tag in ACR.')
param apiImageTag string = 'latest'

@description('Web image tag in ACR.')
param webImageTag string = 'latest'

@description('PostgreSQL Flexible Server name (globally unique).')
param postgresServerName string

@description('PostgreSQL admin username.')
param postgresAdminUser string = 'pgadmin'

@description('PostgreSQL admin password. Provide via --parameters or Key Vault reference.')
@secure()
param postgresAdminPassword string

@description('PostgreSQL SKU. Standard_B1ms = ~$15/month (dev). Standard_D2ds_v4 for prod.')
param postgresSkuName string = 'Standard_B1ms'

@description('Key Vault name (globally unique, 3–24 chars).')
param keyVaultName string

@description('Football API key. Set to empty string to disable ingestion.')
@secure()
param footballApiKey string = ''

@description('VAPID public key for Web Push.')
param vapidPublicKey string = ''

@description('VAPID private key for Web Push.')
@secure()
param vapidPrivateKey string = ''

@description('VAPID subject (mailto: or URL).')
param vapidSubject string = ''

@description('Admin user ID (GUID). Seeded as admin InviteUser on first run.')
@secure()
param adminUserId string = ''

// ── Variables ─────────────────────────────────────────────────────────────────

var logAnalyticsName = '${acaEnvironmentName}-logs'
var postgresDb = 'triviumworldcup'
var postgresConnectionString = 'Host=${postgresServer.properties.fullyQualifiedDomainName};Port=5432;Database=${postgresDb};Username=${postgresAdminUser};Password=${postgresAdminPassword};Ssl Mode=Require;Trust Server Certificate=false;'
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull built-in role
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e0' // Key Vault Secrets User

// ── Managed Identity (used by Container Apps to pull from ACR + read Key Vault) ──

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${acaEnvironmentName}-identity'
  location: location
}

// ── Azure Container Registry ──────────────────────────────────────────────────

resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: acrName
  location: location
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: true // allows `az acr login` from the CLI
  }
}

// Grant the managed identity AcrPull so Container Apps can pull images without stored credentials.
resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, identity.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Log Analytics Workspace ───────────────────────────────────────────────────

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Container Apps Environment ────────────────────────────────────────────────

resource acaEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: acaEnvironmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ── PostgreSQL Flexible Server ────────────────────────────────────────────────

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: postgresServerName
  location: location
  sku: {
    name: postgresSkuName
    tier: 'Burstable' // change to GeneralPurpose for production
  }
  properties: {
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    version: '16'
    storage: { storageSizeGB: 32 }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: { mode: 'Disabled' }
    // Allow Azure services to connect (Container Apps → Postgres without VNet)
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: postgresServer
  name: postgresDb
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Allow all Azure services to reach Postgres (simplest for Container Apps without VNet).
// For production, scope this down to the specific Container Apps outbound IPs.
resource postgresFirewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  parent: postgresServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ── Key Vault ─────────────────────────────────────────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true // use RBAC instead of access policies
    softDeleteRetentionInDays: 7
    enableSoftDelete: true
  }
}

// Grant Key Vault Secrets User to the managed identity so Container Apps can read secrets.
resource kvSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identity.id, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource kvSecretPostgres 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'postgres-connection-string'
  properties: { value: postgresConnectionString }
}

resource kvSecretFootball 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'football-api-key'
  properties: { value: footballApiKey }
}

resource kvSecretVapidPublic 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'vapid-public-key'
  properties: { value: vapidPublicKey }
}

resource kvSecretVapidPrivate 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'vapid-private-key'
  properties: { value: vapidPrivateKey }
}

resource kvSecretVapidSubject 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'vapid-subject'
  properties: { value: vapidSubject }
}

resource kvSecretAdminUserId 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'admin-user-id'
  properties: { value: adminUserId }
}

// ── API Container App (internal ingress) ──────────────────────────────────────

resource apiApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: apiAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: acaEnv.id
    configuration: {
      // Secrets stored in Container Apps (avoids Key Vault round-trip on each request)
      secrets: [
        {
          name: 'postgres-conn'
          value: postgresConnectionString
        }
        {
          name: 'football-api-key'
          value: footballApiKey
        }
        {
          name: 'vapid-private-key'
          value: vapidPrivateKey
        }
        {
          name: 'admin-user-id'
          value: adminUserId
        }
      ]
      registries: [
        {
          server: acr.properties.loginServer
          identity: identity.id
        }
      ]
      ingress: {
        external: false   // internal only — not reachable from the public internet
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${acr.properties.loginServer}/twc-api:${apiImageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'Auth__Provider', value: 'link' }
            // Connection string key must match appsettings.json: ConnectionStrings.Postgres
            { name: 'ConnectionStrings__Postgres', secretRef: 'postgres-conn' }
            { name: 'Football__ApiKey', secretRef: 'football-api-key' }
            { name: 'Push__VapidPublicKey', value: vapidPublicKey }
            { name: 'Push__VapidPrivateKey', secretRef: 'vapid-private-key' }
            { name: 'Push__VapidSubject', value: vapidSubject }
            { name: 'ADMIN_USER_ID', secretRef: 'admin-user-id' }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  dependsOn: [acrPullAssignment, postgresDatabase]
}

// ── Web Container App (external ingress) ──────────────────────────────────────

resource webApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    environmentId: acaEnv.id
    configuration: {
      registries: [
        {
          server: acr.properties.loginServer
          identity: identity.id
        }
      ]
      ingress: {
        external: true    // publicly accessible via HTTPS
        targetPort: 80
        transport: 'http'
        // Redirect HTTP to HTTPS (Container Apps handles TLS termination)
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${acr.properties.loginServer}/twc-web:${webImageTag}'
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          // nginx proxies /api/* and /auth/* to the API app.
          // The API's internal ACA hostname is its app name within the environment.
          // The web Docker image must use default.conf.azure (see nginx/ folder).
          env: [
            { name: 'NODE_ENV', value: 'production' }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  dependsOn: [acrPullAssignment, apiApp]
}

// ── Outputs ───────────────────────────────────────────────────────────────────

@description('ACR login server (e.g. miacr.azurecr.io). Use with docker push / az acr login.')
output acrLoginServer string = acr.properties.loginServer

@description('Web app public URL (HTTPS). Share this with users.')
output webAppFqdn string = 'https://${webApp.properties.configuration.ingress.fqdn}'

@description('API app internal hostname (for nginx upstream in the same ACA environment).')
output apiAppInternalHostname string = apiApp.properties.configuration.ingress.fqdn

@description('PostgreSQL server fully-qualified domain name.')
output postgresHostname string = postgresServer.properties.fullyQualifiedDomainName

@description('Key Vault name.')
output keyVaultName string = keyVault.name

@description('Container Apps Environment name.')
output acaEnvironmentName string = acaEnv.name

@description('Managed identity resource ID (for ACR pull + Key Vault access).')
output managedIdentityId string = identity.id
