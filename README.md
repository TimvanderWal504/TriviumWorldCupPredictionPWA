# Trivium World Cup 2026 — Prediction Pool

An internal Progressive Web App (PWA) prediction pool for the FIFA World Cup 2026. Org members sign up with their work email, predict every group-stage match and the full knockout bracket, pick a champion and a "Golden Six" top-scorer squad, and compete on a live leaderboard. The winner receives an official jersey of the World Cup-winning nation.

---

## Table of Contents

1. [What the app does](#what-the-app-does)
2. [Scoring rules](#scoring-rules)
3. [Technology stack](#technology-stack)
4. [Project structure](#project-structure)
5. [Running locally](#running-locally)
6. [Full-stack Docker Compose](#full-stack-docker-compose)
7. [Azure staging environment](#azure-staging-environment)
8. [Authentication](#authentication)
9. [Admin panel](#admin-panel)
10. [Data ingestion](#data-ingestion)
11. [Push notifications](#push-notifications)
12. [Testing](#testing)
13. [Environment variables](#environment-variables)
14. [Deployment notes](#deployment-notes)

---

## What the app does

### User-facing features

| Screen | Description |
|---|---|
| **Sign up / Log in** | Self-service sign-up with a work email (`trivium-esolutions.com` domain). On success the token is shown once; the user enters it with their email to log in. Admins can also create users manually and share a one-click personal login link. |
| **Predict — Group Stage** | Predict the exact score of every group-stage fixture across all 12 groups (A–L), 48 teams, 104 total matches. Each match locks server-side at its own kickoff time. The page opens on the first incomplete group, auto-advances to the next group when all unlocked predictions are filled, and navigates to the Tournament page when the last group completes. Switch between a **By Group** (tabbed) and **By Date** view. A thin scroll-position indicator shows tab overflow at a glance. |
| **Predict — Tournament** | Pick a **champion** (one of the 48 teams, locked at the tournament's first kickoff) and a **Golden Six** top-scorer squad (exactly 6 players from any of the 1 246 official 2026 World Cup squad members across all 48 teams, locked at first kickoff). |
| **Knockout Bracket** | Full visual bracket from the Round of 32 through the Final. For each match, predict which team advances and, optionally, the 90-minute score. Bracket cards show **LIVE**, **ET** (extra time), and **PEN** (penalties) badges; penalty shootout scores appear in parentheses. The bracket is populated automatically from final group standings using the official FIFA criteria. Visible only once the first R32 slot has teams assigned. |
| **Live Scores** | Real-time match view polling every 20 seconds. Shows all in-progress fixtures with live score, elapsed time (e.g. `LIVE 45+3'`), goal scorers, cards, and substitutions. The tab appears automatically when a live window is active and disappears when no matches are in progress. |
| **Results** | Browse all completed matches with final scores and match events (goals, cards, substitutions). Visible only once at least one match has been completed. |
| **Leaderboard (Ranks)** | Competition-wide ranking with tiebreaker resolution. Drill into any member's predictions to see how they scored on each match — but only for matches that have already kicked off (predictions are hidden until a match locks). |
| **My Standings (Me)** | Your personal score breakdown: total points, current rank, group match points, knockout points, champion prediction status, and a per-player Golden Six breakdown showing goals scored and points earned by each of your 6 picks. |
| **Rules & Scoring** | In-app explainer of the full scoring system with worked examples and timetable. |
| **Profile** | Set your display name and country (shown with a flag on the leaderboard and bracket). |

### PWA features

- **Installable** — served with a Web App Manifest; prompts "Add to home screen" on mobile and shows an install icon in desktop browsers.
- **Offline banner** — a service worker handles caching; an `OfflineBanner` component alerts users when they lose connectivity.
- **Update modal** — when a new app version is deployed the service worker detects it and shows a modal with the changelog entry, letting users reload into the new version without losing their place.

---

## Scoring rules

### Group stage matches

Points are awarded at the single best tier — tiers are **not** cumulative.

| Prediction result | Points |
|---|---|
| Exact score | 10 |
| Correct goal difference (not exact) | 7 |
| Correct outcome only (W/D/L) | 3 |
| Wrong | 0 |

**Team-tally bonus:** +1 point if exactly one team's goal count was predicted correctly. This bonus can only add to the 3-point (correct outcome) or 0-point (wrong) tiers.

### Knockout matches

- **5 points** — correct advancing team (includes AET / penalties)
- **+3 points bonus** — exact 90-minute score (normal time only)

Round multipliers:

| Round | Multiplier |
|---|---|
| Round of 32 | ×1.0 |
| Round of 16 | ×1.5 |
| Quarter-final | ×2.0 |
| Semi-final & third-place play-off | ×2.5 |
| Final | ×3.0 |

A correct Final winner + exact 90-min score = **(5 + 3) × 3.0 = 24 points**.

### Champion

**100 points** if your predicted champion wins the tournament. No partial credit.

Locks at the first kickoff: **11 June 2026**.

### Golden Six (top scorers)

Pick exactly 6 players as your top-scorer squad. Points are earned for every goal scored by each of your 6 players during the tournament.

| Position | Points per goal |
|---|---|
| Forward | 3 |
| Midfielder | 5 |
| Defender | 8 |
| Goalkeeper | 15 |

- Goals from regular time and extra time count (open play + in-match penalties).
- Penalty-shootout goals and own goals do **not** count.
- A player's position is fixed to the position listed in the tournament data.

### Locks and privacy

- Each group match locks server-side at its own kickoff time — predictions cannot be changed after that.
- Champion and Golden Six lock at the first kickoff (11 June 2026).
- Another member's prediction for a match only becomes visible after that match has locked.

---

## Technology stack

| Layer | Technology |
|---|---|
| **Frontend** | React 18, TypeScript, Tailwind CSS, Vite, vite-plugin-pwa |
| **Backend** | .NET 8 Minimal API, Marten (PostgreSQL document store), Quartz.NET (job scheduler), WebPushClient |
| **Database** | PostgreSQL 16 |
| **Auth** | Link auth provider (magic link / form login, domain-whitelisted self-service sign-up); Entra ID provider ready but not yet active |
| **Result ingestion** | Quartz.NET worker polling API-Football v3 every 30 seconds |
| **Hosting (production)** | Docker Compose on an AWOW AK12 mini-PC, fronted by a Cloudflare Tunnel |
| **Hosting (staging)** | Azure Container Apps (Germany West Central), PostgreSQL Flexible Server (North Europe), Azure Container Registry |
| **CI/CD** | GitHub Actions with OIDC service principal; auto-deploys `staging` branch to the Azure staging environment |
| **E2E tests** | Playwright |

---

## Project structure

```
TriviumWorldCupPredictionPWA/
├── src/
│   ├── TriviumWorldCup.Api/        # .NET 8 Minimal API
│   │   ├── Auth/                   # Auth abstraction + link provider
│   │   ├── Admin/                  # Admin endpoints, ingestion status store, stats
│   │   ├── Domain/                 # Document models (Fixture, Team, Player, predictions…)
│   │   ├── Data/SeedData/          # Idempotent tournament seed (48 teams, groups, fixtures, squads)
│   │   ├── Ingestion/              # Quartz.NET result ingestion job + API-Football client
│   │   ├── Knockout/               # Bracket resolver (FIFA ranking criteria)
│   │   ├── Leaderboard/            # Rank + drill-down endpoints
│   │   ├── Predictions/            # Group, knockout, and tournament prediction endpoints
│   │   ├── Push/                   # Web Push VAPID endpoints + reminder job
│   │   ├── Scoring/                # GroupMatchScorer, KnockoutMatchScorer, GoldenSixScorer, ScoringRecomputeService
│   │   ├── Standings/              # Personal score + rank endpoint
│   │   └── Program.cs              # App wiring
│   └── TriviumWorldCup.Web/        # React PWA
│       └── src/
│           ├── auth/               # AuthContext, LoginPage, SignUpPage, ProfileSetupModal
│           ├── components/         # OfflineBanner, UpdateModal
│           ├── pages/              # One file per screen (see feature table above)
│           └── App.tsx             # Tab shell + auth gateway
├── e2e/                            # Playwright test suite (16 smoke tests)
├── scripts/                        # backup.sh (nightly pg_dump, 14-day rotation)
├── .infra/                         # Bicep templates for Azure
├── .github/workflows/              # GitHub Actions CI/CD
├── docker-compose.yml
├── PROGRESS.md                     # Delivery log and build commands
└── CLAUDE.md                       # Canonical project instructions for AI tooling
```

---

## Running locally

### Prerequisites

- .NET 8 SDK
- Node.js ≥ 20
- Docker (for PostgreSQL) or a local PostgreSQL 16 instance

### API

```bash
# Set the database connection string
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=twc;Username=postgres;Password=postgres"

dotnet run --project src/TriviumWorldCup.Api
# Swagger UI: http://localhost:5009/swagger
```

The API seeds all tournament data (48 teams, 12 groups, fixtures, and 1 246 player rosters) on first startup.

### Frontend

```bash
cd src/TriviumWorldCup.Web
npm install
npm run dev          # Vite dev server with HMR
# or
npm run build        # Production build (generates service worker + manifest)
```

---

## Full-stack Docker Compose

```bash
# LAN demo (no external tunnel)
docker compose up -d

# With live result ingestion
FOOTBALL__APIKEY=<your-api-football-key> docker compose up -d

# With Cloudflare Tunnel for remote access
TUNNEL_TOKEN=<your-token> docker compose up -d
```

The stack starts four services:

| Service | Role |
|---|---|
| `postgres` | PostgreSQL 16 database (internal network only) |
| `api` | .NET API on port 8080 (internal; proxied by nginx) |
| `web` | React app + nginx reverse proxy, published on **host port 2026** |
| `backup` | Nightly `pg_dump` at 02:00, 14-day rotation to a named volume |
| `cloudflared` | Cloudflare Tunnel outbound connector (skipped if `TUNNEL_TOKEN` is blank) |

The web app is reachable at `http://<host-ip>:2026`.

---

## Azure staging environment

Staging is provisioned via Bicep (`.infra/main.bicep`) and deployed automatically by GitHub Actions on every push to the `staging` branch.

| Resource | Name |
|---|---|
| Container Registry | `triviumworldcupacr.azurecr.io` |
| Container Apps Environment | `twc-dev` (Germany West Central) |
| API Container App | `twc-api` (internal ingress) |
| Web Container App | `twc-web` (external HTTPS) |
| PostgreSQL 16 Flexible Server | `twc-pg-stg.postgres.database.azure.com` (North Europe) |
| Key Vault | `twc-kv-stg-2026` (West Europe) |

**Staging URL:** `https://twc-web.bravesea-4935fc14.germanywestcentral.azurecontainerapps.io`

---

## Authentication

The app uses a **link auth provider** — no passwords, no OAuth redirects required for the basic flow.

### Self-service sign-up

1. User navigates to `/` and clicks "Sign up".
2. Enters a work email (`trivium-esolutions.com` domain enforced server-side).
3. A one-time token is generated and displayed **once** — the user copies it.
4. On the login screen the user enters their email + token; the server sets a 30-day `HttpOnly` cookie.

### Admin-created users

Admins can create users from the **Admin → Users** panel. A personal one-click login link (`/auth/link/login?id=<uuid>`) is displayed after creation and can be copied and shared directly.

### Entra ID (future)

The auth layer is behind a provider abstraction. Microsoft Entra ID (single-tenant, org members only) is implemented and can be activated by setting `Auth__Provider=entra` — see TWC-20 in the backlog.

---

## Admin panel

Accessible via **Me → Admin** (admin role required). Contains two tabs:

### Operations tab

| Section | What it does |
|---|---|
| **Users** | Create users (generates a shareable login link), list all users with pagination, copy login link or user ID, remove users (existing predictions are kept). |
| **Ingestion Health** | Live dashboard showing last successful/attempted poll, total poll count, error count, pending fixture count, and last error message. Buttons to force a full score recompute or sync fixture API IDs. |
| **Push Notifications** | Send a test Web Push notification to yourself or any user by ID. |
| **Manual Result Override** | Set or correct a match score. Optionally mark the match as currently live (InProgress) with elapsed minute and stoppage time for testing the live-scores feature. Triggers automatic score recompute. |
| **Goal Event Override** | Inject a goal event (open play, in-match penalty, shootout, own goal) for any player in any fixture. Player search by name or team. Triggers full Golden Six recompute. |
| **Card Event Override** | Add a yellow, second yellow, or red card event (display only — does not affect scoring). |
| **Substitution Event Override** | Add a substitution event with player-in, player-out, and minute (display only). |
| **Override History** | Audit log of the last 50 manual overrides with admin name, timestamp, and a **Revert** button that removes the override and re-runs scoring. |

### Statistics tab

Tournament-wide stats visible to admins.

---

## Data ingestion

A Quartz.NET background job (`ResultIngestionJob`) polls **API-Football v3** every **30 seconds**.

Each cycle:

1. Fetches live and recently-completed fixtures from the API.
2. For group-stage fixtures: stores 90-minute scores, match events (goals, cards, substitutions), and marks fixtures `Completed`.
3. For knockout fixtures: maps to `KnockoutSlot` by team pair; stores 90-min score and penalty scores; sets `ExtraTime`, `PenaltyShootout`, or `Completed` status; determines the winner; propagates results through subsequent bracket rounds.
4. Triggers a full `ScoringRecomputeService` pass to update every member's `MemberScore`.
5. **Events backfill:** `EventsIngested` is a separate flag from `Status=Completed`. If a fixture completes but events could not be fetched (e.g. a 429 quota error), the job retries events-only on subsequent polls. A 429 is surfaced in the admin ingestion health dashboard.

Name-based team matching is the primary strategy; known API team IDs can be cached via `POST /admin/fixtures/sync-api-ids`.

---

## Push notifications

Opt-in Web Push using VAPID. Users can subscribe from their profile. The `PushReminderJob` sends reminders before upcoming matches.

Admins can test push delivery from the admin panel without requiring the user to subscribe first.

Required env vars: `Push__VapidPublicKey`, `Push__VapidPrivateKey`, `Push__VapidSubject`.

---

## Testing

```bash
# .NET unit + integration tests (337 tests)
dotnet test TriviumWorldCup.sln

# Frontend type check
cd src/TriviumWorldCup.Web && npx tsc --noEmit

# Frontend unit tests (11 tests)
cd src/TriviumWorldCup.Web && npm test

# E2E (Playwright, 16 smoke tests)
cd e2e && npm install
BASE_URL=http://localhost:2026 npx playwright test   # Docker Compose
```

The E2E suite uses test-control endpoints (`/e2e/*`) that are only active in the `Development` environment to seed users, override fixture times, and inject results deterministically.

---

## Environment variables

| Variable | Required | Description |
|---|---|---|
| `ConnectionStrings__Postgres` | Yes | PostgreSQL connection string |
| `Auth__Provider` | No | `link` (default) or `entra` |
| `ADMIN_USER_ID` | No | UUID of the initial admin user seeded at startup |
| `Football__ApiKey` | No | API-Football v3 key — ingestion silently skips without it |
| `Push__VapidPublicKey` | No | Web Push VAPID public key |
| `Push__VapidPrivateKey` | No | Web Push VAPID private key |
| `Push__VapidSubject` | No | Web Push VAPID subject (e.g. `mailto:admin@example.com`) |
| `TUNNEL_TOKEN` | No | Cloudflare Tunnel token — omit to disable |

See `.env.example` for the full list with example values.

---

## Deployment notes

- **Database migrations** — Marten's `ApplyAllDatabaseChangesOnStartup()` handles schema creation and migrations on startup. No manual migration scripts are needed.
- **Bracket wiring** — Verify all 32 `KnockoutSlot` source entries in `SeedData.cs` against the official FIFA 2026 bracket draw before the first knockout match (28 June 2026).
- **Security advisory** — Marten GHSA-vmw2-qwm8-x84c (Critical) should be resolved before going public.
- **Cookie revocation** — Removing a user from the admin panel stops future logins but any existing `twc_link_session` cookie remains valid until it expires (30 days). No server-side revocation is currently implemented.
- **Entra ID** — Real Entra integration (TWC-20) is blocked on the Entra app registration. When ready, set `Auth__Provider=entra` and supply the client credentials; the link provider remains available as a fallback.
