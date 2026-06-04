# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- MVP ✅ delivered. Post-MVP done: TWC-14–TWC-19. Wave 8 started: TWC-22 E2E foundation done.
- **Knockout gap:** bracket structure exists but slot population not implemented (TWC-32 pending).
- **E2E:** TWC-22 foundation ✅ (16/16 Playwright smoke tests pass). Area specs TWC-23–TWC-31 are next.
- TWC-20 (Entra) remains BLOCKED.

## Planned waves
- **Wave 7** — TWC-32 knockout resolver (group standings → R32, 8 best third-placed allocation, round propagation, idempotent). Must precede the knockout E2E.
- **Wave 8** — E2E suite (epic TWC-21): TWC-22 foundation first, then TWC-23–TWC-30 in parallel; TWC-31 (knockout E2E) after Wave 7.
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

**Final: 251 .NET tests + 11 frontend tests passing. Both builds green.**

### Post-MVP Wave 5
- **TWC-14** ✅ — Knockout bracket population + per-round prediction screens
- **TWC-16** ✅ — Admin screen: ingestion monitoring + manual result override
- **TWC-19** ✅ — Nightly PostgreSQL backups + 14-day rotation and restore runbook (`feature/TWC-16`)

### Post-MVP Wave 6
- **TWC-15** ✅ — Knockout scoring (advancing team + 90-min bonus + round multiplier)
- **TWC-17** ✅ — Live in-match score updates: GET /fixtures/live endpoint, LiveScoresPage, 20s polling, stops when liveWindowActive=false
- **TWC-18** ✅ — Push notifications: opt-in Web Push reminders (VAPID) (`feature/TWC-17`)

### Wave 8 — E2E
- **TWC-22** ✅ — Playwright harness: `e2e/` project, mock-auth login helper, seed/reset helper, time/result control endpoints (`/e2e/*`, non-Production), page objects for all 8 screens, 16/16 smoke tests green (`feature/TWC-22-impl`)

## ⚠️ Required before going live

1. **Set `FOOTBALL__APIKEY`** env var to the API-Football key — ingestion worker runs silently without it
2. **Populate `FootballApiTeamMap.AddKnownId()`** entries by calling `GET /teams?league=1&season=2026` once the key is active (optional — name-based matching is the primary strategy)
3. **Audit `PlayersData.cs`** — rosters for CZE, BIH, HTI, SCO, CUW, SWE, CPV, JOR, COD are best-effort; update before first kickoff

## Blocked (post-MVP only)
- **TWC-20** real Entra integration — Entra app registration + Cloudflare Tunnel hostname
- **Cloudflare Tunnel token** — LAN demo works without it

## Known follow-ups (non-blocking)
- **Marten GHSA-vmw2-qwm8-x84c (Critical):** Upgrade before going public.
- **NuGet.config:** Repo-level override for unreachable `BluRedSelect` Azure DevOps feed.

## Build commands

### .NET API
```
dotnet test TriviumWorldCup.sln   # 324 tests
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
docker compose --profile tunnel up -d         # with Cloudflare Tunnel
```

### E2E
```
cd e2e
npm install                          # first time only
BASE_URL=http://localhost:80 npx playwright test   # Docker Compose
# or
BASE_URL=http://localhost:5173 npx playwright test  # Vite dev + dotnet run
```

## Next action
1. **Wave 7** — implement TWC-32 (knockout resolver); this unblocks the knockout flow end to end.
2. **Wave 8 continued** — area specs TWC-23–TWC-30 in parallel after TWC-22 PR merges; TWC-31 (knockout E2E) after TWC-32.
3. **Wave 9 (final)** — TWC-20 real Entra, once the app registration is provided.
