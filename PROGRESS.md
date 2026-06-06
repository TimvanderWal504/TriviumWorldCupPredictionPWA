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
- **Deployment** — App is running on AK12 at port 2026 via Docker Compose + Portainer. `Auth__Provider=link`, `ADMIN_USER_ID` set in Portainer env vars.

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
- **Existing DB admin seed:** `TournamentSeed` only runs on an empty database. If the AK12 DB was already seeded, the admin `InviteUser` and `UserProfile` records were NOT inserted automatically — create them via `POST /admin/users` (once logged in) or by clearing and re-seeding the DB.
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

## Next action
## Azure Migration (non-blocking, parallel to Wave 9)

Infrastructure-as-Code generated 5 June 2026. Moves the stack from Docker Compose on AK12
to Azure Container Apps + managed PostgreSQL.

**Deliverables committed in another branch:**
- `.infra/main.bicep` — parameterized Bicep (ACR, Log Analytics, ACA environment, PostgreSQL Flexible Server, Key Vault, API + Web Container Apps)
- `.infra/main.parameters.json` — parameter template (fill in names/passwords/keys)
- `src/TriviumWorldCup.Web/nginx/default.conf.azure` — Azure nginx config (`http://twc-api` upstream instead of `http://api:8080`)
- `.docs/AZURE_MIGRATION.md` — step-by-step deployment guide
- `.github/workflows/deploy-azure.yml` — CI/CD pipeline (already present; builds images, pushes to ACR, updates Container Apps on push to main)

**Pending decisions / actions (human-gated):**
- Fill in `.infra/main.parameters.json` (ACR name, Postgres password, VAPID keys, admin user ID)
- Update `src/TriviumWorldCup.Web/Dockerfile` to use `default.conf.azure` for Azure builds
- Run `az deployment group create` (5–12 min; see `.docs/AZURE_MIGRATION.md`)
- Create GitHub OIDC service principal + set repo secrets/variables
- Entra app registration (needed for TWC-20 — not required for initial Azure deployment)
- Azure region + cost sign-off (~€40–60/month at MVP SKUs)
- Custom domain (optional; auto-generated ACA FQDN works immediately)

AK12 continues to work until Azure is confirmed stable.


1. **Seed admin user on AK12** — if the DB was already seeded before the admin user seed was added, create Tim manually: Admin page → Users → Create, or clear and re-seed the DB.
2. **Azure migration** — fill in `.infra/main.parameters.json` and follow `.docs/AZURE_MIGRATION.md`.
3. **Wave 9 (final)** — TWC-20 real Entra, once the app registration is provided. When ready: add `EntraIdentityProvider`, set `Auth:Provider=entra` in Portainer/Azure env vars, leave link auth as-is for fallback.
