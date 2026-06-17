# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- MVP ✅ delivered. Post-MVP done: TWC-14 (knockout bracket), TWC-15 (knockout scoring), TWC-16 (admin), TWC-17 (live updates), TWC-18 (push notifications), TWC-19 (backups). TWC-22 E2E foundation done. TWC-32 ✅ knockout resolver delivered (Wave 7) — bracket now populates from final group standings.
- **E2E:** TWC-22 foundation ✅ (16/16 smoke tests). Harness updated for link auth (TWC-33 follow-up). Area specs TWC-23–TWC-31 remain Backlog.
- TWC-20 (Entra) remains BLOCKED.

## Planned waves
- **Wave 8** — E2E suite (epic TWC-21): TWC-22 foundation ✅, area specs TWC-23–TWC-30 in parallel; TWC-31 (knockout E2E) unblocked by TWC-32 ✅.
- **Wave 9 — final** — TWC-20 real Entra integration (human-gated on the Entra app registration).

## Accepted (Done)

### Wave 0
- **TWC-2** ✅ — Docker Compose scaffold (`feature/TWC-2`)

### Wave 1
- **TWC-3** ✅ — Auth abstraction + mock provider (`feature/TWC-3`)
- **TWC-5** ✅ — Tournament data model + seed (`feature/TWC-5`)

### Wave 2
- **TWC-4** ✅ — User profile: display name + country (`feature/TWC-4`)
- **TWC-6** ✅ — Group predictions with kickoff lock (`feature/TWC-11`)
- **TWC-7** ✅ — Tournament prediction: champion + Golden Six (`feature/TWC-11`)
- **TWC-12** ✅ — Rules & scoring explainer screen (`feature/TWC-11`)
- **TWC-13** ✅ — PWA shell: vite-plugin-pwa, service worker, offline banner (`feature/TWC-11`)

### Wave 3+4
- **TWC-8** ✅ — Scoring engine: GroupMatchScorer, GoldenSixScorer, ScoringRecomputeService (`feature/TWC-11`)
- **TWC-9** ✅ — Football API ingestion: Quartz.NET, FootballApiClient, idempotent GoalEvent upsert (`feature/TWC-11`)
- **TWC-10** ✅ — My standings: GET /scores/me, rank, Golden Six per-player breakdown (`feature/TWC-11`)
- **TWC-11** ✅ — Leaderboard + drill-down: competition-rank tiebreakers, privacy-enforced (`feature/TWC-11`)

**Final: 324 .NET tests + 11 frontend tests + 16 Playwright smoke tests passing. All builds green.**

### Post-MVP Wave 5
- **TWC-14** ✅ — Knockout bracket population + per-round prediction screens
- **TWC-16** ✅ — Admin screen: ingestion monitoring + manual result override
- **TWC-19** ✅ — Nightly PostgreSQL backups + 14-day rotation and restore runbook (`feature/TWC-16`)

### Post-MVP Wave 6
- **TWC-15** ✅ — Knockout scoring (advancing team + 90-min bonus + round multiplier)
- **TWC-17** ✅ — Live in-match score updates: GET /fixtures/live endpoint, LiveScoresPage, 20s polling, stops when liveWindowActive=false
- **TWC-18** ✅ — Push notifications: opt-in Web Push reminders (VAPID)

### Wave 8 — E2E
- **TWC-22** ✅ — Playwright harness: `e2e/` project, link-auth login helper, seed/reset helpers, time/result control endpoints (`/e2e/*`, Development only), page objects for all screens, 16/16 smoke tests green (`feature/TWC-22-impl`)

### Wave 7
- **TWC-32** ✅ — Knockout bracket resolver: group ranking (FIFA criteria: pts/GD/GF + head-to-head), best-8-of-12 third-placed selection, R32 slot population, MatchWinner/MatchLoser round propagation, new admin endpoint `POST /admin/knockout/{slotKey}/result`, idempotent. 22 new tests; 351 total pass. (`feature/TWC-32`)

### Unversioned work (main, 4–5 June 2026)
- **PlayersData.cs** — Complete rewrite with official 2026 FIFA World Cup squads for all 48 teams (1 246 players). Source: Wikipedia squads page fetched 4 June 2026 (all squads submitted by 1 June). Positions mapped exactly from Wikipedia GK/DF/MF/FW to `Position.GK/DEF/MID/FWD`. Test minimum-squad threshold raised from 15 → 23 (FIFA minimum). 351 tests still pass.
- **TWC theme / design system** — `twc-theme.css` design-token file added; `flagUrl.ts` utility added; all pages updated to use the new design tokens. TypeScript clean.
- **GroupPredictionsPage UX** — (1) Thin 3 px scroll-position indicator bar under the group tabs (ResizeObserver + scroll listener, shows thumb proportional to visible tabs). (2) Auto-advance: when every unlocked fixture in the active group has been predicted, the page transitions to the next group after 600 ms; on page load, starts on the first incomplete group rather than always Group A. (3) When group L completes, navigates automatically to the Tournament prediction page. All three features work on mobile.
- **Link auth provider — replaces mock auth entirely:**
  - New: `Auth/Link/InviteUser.cs` (Marten document), `LinkIdentityProvider.cs` (reads `twc_link_session` cookie from DB), `LinkAuthEndpoints.cs` (`GET /auth/link/login?id=` sets 30-day HttpOnly cookie + redirects to `/`; `POST /auth/link/logout`; `GET /auth/me` reports `authProvider: "link"`).
  - Admin user management: `GET/POST/DELETE /admin/users` in `AdminEndpoints`; Admin page shows Users section (create, list, copy link, delete) only when `isLinkAuth`.
  - Deleted: `Auth/Mock/` folder (all three files), `DevUserSwitcher.tsx`. `Auth:Provider` default changed to `"link"`. `ASPNETCORE_ENVIRONMENT` restored to `Production` (link auth has no production guard).
- **Admin user seed** — `InviteUsersData.cs` and `UserProfilesData.cs` read `ADMIN_USER_ID` env var at seed time and create an `InviteUser` (role: admin) + `UserProfile` (Tim, NL) as part of `TournamentSeed`. `ADMIN_USER_ID` added to `docker-compose.yml` and `.env.example` with stable example value `fe3a4de8-3243-48b8-b68b-528d35dedeed`.
- **App.tsx restructure** — Rules promoted to dedicated top-level tab (removed from Me sub-page). Admin remains accessible via Me → Admin. `IS_PROD` / `isMockAuth` removed; sign-in screen shows "Open your personal login link to sign in." `AuthContext` simplified: `isMockAuth` removed, `signOut` hardcoded to `/auth/link/logout`.
- **Deployment** — App deployed to Azure Container Apps (staging env). `Auth__Provider=link`, `ADMIN_USER_ID` set in Azure Key Vault / env vars.

## ⚠️ Required before going live

1. **Set `FOOTBALL__APIKEY`** env var to the API-Football key — ingestion worker runs silently without it
2. **Populate `FootballApiTeamMap.AddKnownId()`** entries by calling `GET /teams?league=1&season=2026` once the key is active (optional — name-based matching is the primary strategy)
3. ~~**Audit `PlayersData.cs`**~~ ✅ — All 48 squads replaced with official 2026 FIFA World Cup rosters (source: Wikipedia, fetched 4 June 2026). Re-seeding a fresh DB picks these up automatically; existing DBs need a player-only migration (delete `mt_doc_player`, restart API).

## Blocked (post-MVP only)
- **TWC-20** real Entra integration — Entra app registration + Cloudflare Tunnel hostname
- **Cloudflare Tunnel token** — LAN demo works without it

## Known follow-ups (non-blocking)
- **Marten GHSA-vmw2-qwm8-x84c (Critical):** Upgrade before going public.
- **NuGet.config:** Repo-level override for unreachable `BluRedSelect` Azure DevOps feed.
- **R32 bracket wiring (`SeedData.cs`):** Several slot source entries carry `// TODO: verify bracket wiring` comments. The resolver consumes these declarations faithfully — if any slot source is wrong the bracket will misfire. Verify all 32 slot source entries against the official FIFA 2026 bracket draw before the first knockout match (28 June).
- **E2E harness updated for link auth:** `POST /e2e/seed/invite-user` endpoint added; `auth.ts` rewritten to navigate to `/auth/link/login?id=`; `AppPage.ts` nav methods updated for current tab structure. Area specs TWC-23–31 remain Backlog and have not been run yet.
- **Existing DB admin seed:** `TournamentSeed` only runs on an empty database. If the DB was already seeded, the admin `InviteUser` and `UserProfile` records were NOT inserted automatically — create them via `POST /admin/users` (once logged in) or by clearing and re-seeding the DB.
- **Invite user cookie revocation:** Removing a user from the admin page stops future logins but their existing `twc_link_session` cookie remains valid until it expires (30 days). No server-side revocation is currently implemented.

## Build commands

### .NET API
```
dotnet test TriviumWorldCup.sln   # 337 tests
dotnet run --project src/TriviumWorldCup.Api  # Swagger at http://localhost:5009/swagger
```

### React Web
```
cd src/TriviumWorldCup.Web
npm run build   # generates sw.js + manifest.webmanifest
npm test        # 11 frontend tests
```

### Full stack
```
docker compose up -d                           # LAN demo
FOOTBALL__APIKEY=<key> docker compose up -d   # with ingestion
TUNNEL_TOKEN=<token> docker compose up -d   # with Cloudflare Tunnel (omit token to disable)
```

### E2E
```
cd e2e
npm install                          # first time only
BASE_URL=http://localhost:80 npx playwright test   # Docker Compose
# or
BASE_URL=http://localhost:64505 npx playwright test  # Vite dev + dotnet run
```

## Azure Migration — Staging ✅ (6 June 2026)

Staging environment fully provisioned on Azure (Visual Studio Enterprise subscription, resource group `twc-rg`).

**Branch strategy:**
- `staging` → GitHub Environment `staging` (this subscription) — auto-deploys on push
- `main` → GitHub Environment `production` (future production tenant) — auto-deploys on push

**Staging resources (all live):**

| Resource | Name | Location |
|---|---|---|
| Container Registry | `triviumworldcupacr.azurecr.io` | germanywestcentral |
| ACA Environment | `twc-dev` | germanywestcentral |
| API Container App | `twc-api` (internal ingress) | germanywestcentral |
| Web Container App | `twc-web` (external HTTPS) | germanywestcentral |
| PostgreSQL 16 | `twc-pg-stg.postgres.database.azure.com` | northeurope |
| Key Vault | `twc-kv-stg-2026` | westeurope |

**Web app URL:** `https://twc-web.bravesea-4935fc14.germanywestcentral.azurecontainerapps.io`

**CI/CD:** GitHub Actions (`.github/workflows/deploy-azure.yml`) — OIDC service principal `twc-github-actions-staging` wired; staging environment secrets + variables set. First deploy triggered by pushing to `staging` branch.

**Infrastructure notes:**
- Bicep: `.infra/main.bicep` — has `postgresLocation` + `keyVaultLocation` params to work around Visual Studio subscription quota restrictions (germanywestcentral and westeurope restricted for Postgres Flexible Server).
- Container Apps deploy with placeholder image on first Bicep run; GitHub Actions replaces with real images on first push to `staging`.
- PostgreSQL in northeurope is cross-region from ACA (germanywestcentral) — fine for staging, co-locate for production.

**Remaining / pending:**
- `git push origin staging` — triggers first real image build + deploy (currently running placeholder)
- Seed admin user: navigate to `<webAppFqdn>/auth/link/login?id=c0c53bf2-8c04-4f08-86ee-10d25e895fee`
- Production tenant: create new Azure subscription, copy `.infra/main.parameters.local.json` → `main.parameters.production.local.json`, create OIDC SP in prod tenant, set GitHub `production` environment secrets, then push to `main`
- Custom domain (optional)
- Entra app registration for TWC-20 (not blocking Azure deployment)

Local Docker Compose can still be used for development; Azure staging is the primary deployment target.

## Unversioned work (staging branch, 7 June 2026)

### Magic link — self-service sign-up + form-based login
- **`Auth/Link/InviteUser.cs`** — added nullable `Email` property (null for admin-created users).
- **`Program.cs`** — added Marten email index (`Index(u => u.Email!)`).
- **`appsettings.json`** — added `Auth:Link:AllowedDomains: ["trivium-esolutions.com"]` domain whitelist.
- **`Auth/Link/LinkAuthEndpoints.cs`** — two new anonymous endpoints:
  - `POST /auth/link/signup` — validates email domain, checks for duplicate, creates `InviteUser`, returns `{ token }` once.
  - `POST /auth/link/login` — accepts `{ email, token }`, looks up user by email, verifies GUID, sets `twc_link_session` cookie.
- **`Data/SeedData/InviteUsersData.cs`** — admin seed user now carries `Email = "tim.vanderwal@trivium-esolutions.com"` (upserted on restart).
- **`auth/SignUpPage.tsx`** (new) — email input form; on success shows token once with copy button + "Go to sign in" link.
- **`auth/LoginPage.tsx`** (new) — email + token form; on success calls `reload()` from `AuthContext`.
- **`auth/AuthContext.tsx`** + **`auth/types.ts`** — added `reload: loadAuthState` to context value.
- **`App.tsx`** — added `AuthGateway` component (login/signup toggle); replaces old "open your personal login link" prompt.

### ET/Penalties in knockout bracket + ingestion fix
- **`Domain/Enums.cs`** — extended `MatchStatus` with `ExtraTime = 4` and `PenaltyShootout = 5` (appended; safe for existing int-serialised data).
- **`Domain/KnockoutSlot.cs`** — added `PenaltyHomeScore` and `PenaltyAwayScore` nullable int fields; `HomeScore/AwayScore` documented as 90-min score.
- **`Ingestion/FootballApiClient.cs`** — added `ApiScore` / `ApiScoreEntry` DTOs; mapped `score.fulltime` → `ScoreFullTimeHome/Away` and `score.penalty` → `ScorePenaltyHome/Away`.
- **`Ingestion/ResultIngestionJob.cs`** — two critical fixes:
  1. Live-window check now also queries `KnockoutSlot` (was only checking `Fixture`; during knockout phase all group fixtures are `Completed`, so the job always exited early).
  2. New Step 8: matches live/completed API fixtures to `KnockoutSlot` by team pair; sets `ExtraTime` / `PenaltyShootout` / `Completed` status; stores 90-min score; stores penalty scores; determines `WinnerTeamId` from AET total or penalty outcome; triggers `PropagateAllKnockoutResultsAsync` on any update.
- **`Tournament/KnockoutSlotEndpoints.cs`** — `KnockoutSlotDto` now includes `PenaltyHomeScore` and `PenaltyAwayScore`.
- **`pages/KnockoutBracketPage.tsx`** — bracket cards: live border colour; `ET` / `PEN` / `LIVE` status badges; per-team penalty scores in brackets when won on penalties; "After extra time" / "Won on penalties" footer. AET inferred when `homeScore == awayScore && winnerTeamId != null && penaltyHomeScore == null`.

### Admin prediction injection endpoint
- **`Admin/AdminEndpoints.cs`** — `POST /admin/users/{userId}/predictions/inject` accepts `[{ fixtureId, home, away }]` JSON body; validates all fixture IDs exist (422 on unknown); upserts `GroupPrediction` documents bypassing lock checks; returns `{ userId, injected: N }`. Idempotent.
- **`pages/AdminPage.tsx`** — removed inject button; added "Copy ID" button per user row for convenient `userId` lookup when calling the inject endpoint.

**Build status:** `dotnet test` and `npx tsc --noEmit` both pass with zero errors.

## Unversioned hotfix (fix/events-backfill-and-quota-handling, 12 June 2026)

### Tournament prediction grace window
- **`TournamentPredictionPage.tsx`** — frontend-only override: `GRACE_DATE = '2026-06-12'`; `effectiveLocked = isLocked && !isGraceDay` bypasses the server lock for today only so users can still submit champion and top-scorer predictions. Comparison uses UTC date (`toISOString().slice(0,10)`). **Remove or update `GRACE_DATE` once the grace window has passed.**

## Unversioned work (fix/events-backfill-and-quota-handling, 12 June 2026)

### Events backfill + quota resilience (PR #13)
- **`Domain/Fixture.cs`** — added `EventsIngested` bool (default `false`). Decouples "score recorded" (`Status=Completed`) from "events recorded" so a failed events fetch no longer causes permanent data loss.
- **`Ingestion/FootballApiClient.cs`** — `GetAllEventsAsync` now detects HTTP 429 and throws a recognisable `HttpRequestException(inner: InvalidOperationException("Quota exceeded"))` instead of silently returning an empty list.
- **`Ingestion/ResultIngestionJob.cs`** — three related changes:
  1. Skip condition changed from `Completed` to `Completed && EventsIngested`: completed fixtures without events are eligible for backfill on the next 30-second poll.
  2. Separate catch handlers for quota exhaustion (429) vs. other transient errors; quota failures are recorded in `statusStore.LastError` for admin dashboard visibility.
  3. `EventsIngested = true` is set only after `GetAllEventsAsync` returns successfully; log line now includes `events=ok|failed|429_quota`.
- Existing `Fixture` documents default to `EventsIngested=false` and will backfill on the next poll — no migration required.
- **337 tests pass.**

## Next action
1. **`git push origin staging`** — triggers first GitHub Actions build; after ~5 min the web app URL serves the real app.
2. **Seed admin user on staging** — navigate to `https://twc-web.bravesea-4935fc14.germanywestcentral.azurecontainerapps.io/auth/link/login?id=c0c53bf2-8c04-4f08-86ee-10d25e895fee`
3. **Seed admin user on staging** — if the DB was already seeded before the admin user seed was added, create Tim manually: Admin page → Users → Create, or clear and re-seed the DB.
4. **Wave 9 (final)** — TWC-20 real Entra, once the app registration is provided. When ready: add `EntraIdentityProvider`, set `Auth:Provider=entra` in Azure env vars, leave link auth as-is for fallback.
