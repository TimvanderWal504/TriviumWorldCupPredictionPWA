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
| **Sign up / Log in** | Self-service sign-up with a work email (`trivium-esolutions.com` domain). The response never reveals whether an account already exists: a token is shown once only for a newly-created account, otherwise a generic confirmation is shown. The user enters their email + token to log in. Admins can also create users manually and share a one-click personal login link. Sign-up and login are rate-limited server-side. |
| **Predict — Group Stage** | Predict the exact score of every group-stage fixture across all 12 groups (A–L), 48 teams, 104 total matches. Each match locks server-side at its own kickoff time. The page opens on the first incomplete group, auto-advances to the next group when all unlocked predictions are filled, and navigates to the Tournament page when the last group completes. Switch between a **By Group** (tabbed) and **By Date** view. A thin scroll-position indicator shows tab overflow at a glance. |
| **Predict — Tournament** | Pick a **champion** (one of the 48 teams, locked at the tournament's first kickoff) and a **Golden Six** top-scorer squad (exactly 6 players from any of the 1 246 official 2026 World Cup squad members across all 48 teams, locked at first kickoff). |
| **Knockout Bracket** | A sub-pill inside the Predict tab. Horizontal round-progression stepper (R32 → … → Final) shows `predicted / total` per round. For each match, enter a mandatory score per team (auto-saved); the advancing team is derived from the higher score. On a tied prediction, a tie-breaker panel lets you pick who goes through. Bracket cards show **LIVE**, **ET** (extra time), and **PEN** (penalties) badges; penalty shootout scores appear in parentheses. The bracket is populated automatically from final group standings using the official FIFA criteria. Predictions-tab cards always show your own predicted score/winner, never the actual result. |
| **Live Scores** | Real-time match view polling every 20 seconds. Shows all in-progress group and knockout fixtures with live score, elapsed time (e.g. `LIVE 45+3'`), **AET**/**PEN** badges, goal scorers, cards, and substitutions. The tab appears automatically when a live window is active and disappears when no matches are in progress. |
| **Results** | Three tabs — **Group Stage**, **Knockouts**, **By Date** — each fully exclusive (no bleed-through between group and knockout matches). Knockout result cards show final score, penalty score (if applicable), a winner badge, and a per-component points breakdown (score / advancing-team / total). Visible only once at least one match has been completed. |
| **Leaderboard (Ranks)** | Competition-wide ranking with tiebreaker resolution; each entry shows a server-computed masked handle (email local-part only, never the full address). The top 3 members are displayed on a visual tri-level podium (gold / silver / bronze) with flag avatars and coloured bars; ranks 4+ appear as a flat list below. Drill into any member's predictions — group and knockout, each with a per-prediction points column, plus a separate knockout winner row showing streak multiplier — to see how they scored on each match, but only for matches that have already kicked off (predictions are hidden until a match locks). |
| **My Standings (Me)** | Your personal score breakdown: total points, current rank, group match points, knockout points, champion prediction status, and a per-player Golden Six breakdown showing goals scored and points earned by each of your 6 picks. |
| **Rules & Scoring** | In-app explainer of the full scoring system with worked examples and timetable. |
| **Profile** | Set your display name and country (shown with a flag on the leaderboard and bracket). |

### PWA features

- **Installable** — served with a Web App Manifest; prompts "Add to home screen" on mobile and shows an install icon in desktop browsers.
- **Offline banner** — a service worker handles caching; an `OfflineBanner` component alerts users when they lose connectivity.
- **Update modal** — when a new app version is deployed the service worker detects it and shows a modal with the changelog entry, letting users reload into the new version without losing their place.
- **Loading states** — a shared `Spinner` component (small/medium/large, with a dual-arc animated large variant) and a `SkeletonLeaderboard` placeholder replace plain "Loading…" text across all pages; save actions show an inline spinner during the network round-trip.

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

Two independent components, summed:

- **Component 1 — score prediction (not multiplied):** scored using the same group-stage tiers (10 / 7 / 3 / 0), judged at the end of the match as actually played — the 90-minute score for matches decided in normal time, or the score at the end of extra time for matches that went to ET/AET or were decided on penalties. Penalty-shootout kicks are excluded from this score.
- **Component 2 — advancing team (streak-multiplied):** **5 × streak points** if the correct team advances (including via extra time / penalties). Your *streak* is the number of consecutive correct advancing-team predictions you've made so far in the knockout phase, tracked per bracket path (not globally) — one wrong prediction resets it to zero; a skipped match leaves it unchanged.

| Consecutive correct predictions (streak) | Bonus |
|---|---|
| 1st correct (streak = 1) | 5 × 1 = 5 pts |
| 2nd in a row (streak = 2) | 5 × 2 = 10 pts |
| 3rd in a row (streak = 3) | 5 × 3 = 15 pts |
| …and so on | 5 × streak |

`Total = [score-prediction points] + [5 × streak if advancing team correct]`

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
| **Hosting (production)** | Azure Container Apps (Germany West Central), PostgreSQL Flexible Server, fronted by a Cloudflare Tunnel (optional) |
| **Hosting (staging)** | Azure Container Apps (Germany West Central), PostgreSQL Flexible Server (North Europe), Azure Container Registry |
| **CI/CD** | GitHub Actions with OIDC service principal; a `test` job (`dotnet test`) gates the `build-and-deploy` job — pushes to `staging` or `main` are blocked if any backend test fails |
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
3. If the email is new and eligible, a one-time token is generated and displayed **once** — the user copies it. If an account for that email already exists, the same generic success response is returned with no token, so the endpoint never reveals which addresses are already registered.
4. On the login screen the user enters their email + token; the server sets a 30-day `HttpOnly` cookie.

`/auth/link/*` endpoints are rate-limited server-side (fixed window, per client) against brute-force/guessing.

### Admin-created users

Admins can create users from the **Admin → Users** panel. A personal one-click login link (`/auth/link/login?id=<uuid>`) is displayed after creation and can be copied and shared directly.

### Entra ID (deprioritized)

The auth layer is behind a provider abstraction. Microsoft Entra ID (single-tenant, org members only) is implemented and can be activated by setting `Auth__Provider=entra`, but the backlog story for real Entra integration (TWC-20) has been marked Obsolete/deprioritized in favor of the link auth provider — no further work is planned unless reopened.

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
| **Knockout Team Override** | Manually set the home/away team on a knockout slot when the bracket resolver mis-assigns a bipartite match. Locks that side against being overwritten by future resolver runs until the override is reverted. |
| **Goal Event Override** | Inject a goal event (open play, in-match penalty, shootout, own goal) for any player in any fixture or knockout slot. Player search by name or team. Triggers full Golden Six recompute. |
| **Card Event Override** | Add a yellow, second yellow, or red card event (display only — does not affect scoring). |
| **Substitution Event Override** | Add a substitution event with player-in, player-out, and minute (display only). |
| **Fixture / Slot event backfill** | Fetch or reset match events for a group fixture ID or a knockout slot key (e.g. `R32-1`). |
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
4. Triggers an incremental `ScoringRecomputeService` pass that rescores only the members who predicted the completed fixture or slot, rather than every member.
5. **Events backfill:** `EventsIngested` is a separate flag from `Status=Completed`. If a fixture completes but events could not be fetched (e.g. a 429 quota error), the job retries events-only on subsequent polls. A 429 is surfaced in the admin ingestion health dashboard.

Name-based team matching is the primary strategy; the API fixture id for specific rows can be backfilled via `POST /admin/fixtures/sync-api-ids` with a body of `{ "ids": ["61", "SF-1", "F"] }` (group-stage fixture ids or knockout slot keys), which also works on already-completed matches.

---

## Push notifications

Opt-in Web Push using VAPID. Users can subscribe from their profile. The `PushReminderJob` sends reminders before upcoming matches.

Admins can test push delivery from the admin panel without requiring the user to subscribe first.

Required env vars: `Push__VapidPublicKey`, `Push__VapidPrivateKey`, `Push__VapidSubject`.

---

## Testing

```bash
# .NET unit + integration tests (480 tests)
dotnet test TriviumWorldCup.sln

# Frontend type check
cd src/TriviumWorldCup.Web && npx tsc --noEmit

# Frontend unit tests (11 tests; OfflineBanner.test.tsx has a known pre-existing failure)
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
- **Forwarded headers** — the API trusts `X-Forwarded-Proto`/`X-Forwarded-For` unconditionally so the session cookie's `Secure` flag reflects the original scheme behind Azure Container Apps' TLS-terminating ingress (the API has internal-only ingress; ACA doesn't publish a fixed proxy IP range to scope this further).
- **Cookie revocation** — Removing a user from the admin panel stops future logins but any existing `twc_link_session` cookie remains valid until it expires (30 days). No server-side revocation is currently implemented.
- **Entra ID** — Real Entra integration (TWC-20) is marked Obsolete/deprioritized in Jira, not just blocked. If reopened, set `Auth__Provider=entra` and supply the client credentials; the link provider remains available as a fallback.
