# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- Current wave: **Wave 3** — TWC-8 (scoring engine) next; TWC-9 blocked on API key
- Scope: MVP (label `mvp`)
- Hard deadline: **11 June 2026** (first kickoff — predictions and champion/Golden Six lock)

## Accepted (Done)

### Wave 0
- **TWC-2** ✅ — Docker Compose scaffold (`feature/TWC-2`)

### Wave 1
- **TWC-3** ✅ — Auth abstraction + mock provider (`feature/TWC-3`)
- **TWC-5** ✅ — Tournament data model + seed (`feature/TWC-5`)
  - Seed data corrected from Wikipedia; all 48 teams, 72 fixtures, 32 KO slots, ~760 players verified

### Wave 2
- **TWC-4** ✅ — User profile: display name + country (`feature/TWC-4`)
  - UserProfile Marten document, GET/POST/PUT /profile, ISO 3166-1 country validation
  - ProfileSetupModal (non-dismissable), ProfilePage, AuthContext `hasProfile` flag
  - 117 tests passing; both builds green

- **TWC-6** ✅ — Group predictions with kickoff lock (`feature/TWC-13`)
  - GroupPrediction document (Id = "{UserId}_{FixtureId}")
  - GET/POST/PUT /predictions/group/{fixtureId} — server-side 403 after kickoff
  - GET /fixtures (with team names), GET /teams — no auth required
  - GroupPredictionsPage.tsx — A–L group tabs, local-timezone kickoff times, locked/unpredicted states
  - 10 unit tests (lock boundary, composite ID, request validation)

- **TWC-7** ✅ — Tournament prediction: champion + Golden Six (`feature/TWC-13`)
  - TournamentPrediction document (Id = UserId)
  - GET/POST/PUT /predictions/tournament — locked at first fixture kickoff (server-side)
  - GET /players with embedded team name and position
  - TournamentPredictionPage.tsx — searchable 48-team champion picker, 6-player Golden Six
  - 25 unit tests (lock, golden-six count, champion validation)

- **TWC-12** ✅ — Rules & scoring explainer screen (`feature/TWC-13`)
  - RulesPage.tsx — full canonical ruleset, worked example (2-1 vs 2-2 → 1 pt), all tiers + multipliers
  - Reachable from main nav

- **TWC-13** ✅ — PWA shell (`feature/TWC-13`)
  - vite-plugin-pwa 1.3.0, VitePWA generateSW strategy
  - Real 192/512 PNG icons (solid #1e293b, generated via Node.js zlib)
  - PWA meta tags in index.html; sw.js + manifest.webmanifest generated on build
  - OfflineBanner component (amber, dismissable, auto-hides on reconnect)
  - network.ts utility (isOnline, requiresConnectivity) for prediction forms
  - 11 frontend tests (vitest + jsdom + testing-library)

**Wave 2 total: 142 .NET tests + 11 frontend tests passing**

## In progress
_None currently running_

## Branch chain (merge in this order)
1. `feature/TWC-2` → main
2. `feature/TWC-3` → main (based on TWC-2)
3. `feature/TWC-5` → main (based on TWC-3) — includes seed data corrections
4. `feature/TWC-4` → main (based on TWC-5)
5. `feature/TWC-13` → main (based on TWC-4) — contains TWC-4 follow-on, TWC-6, TWC-7, TWC-12, TWC-13
   - Commits: 399eb81 (TWC-13 PWA), 5932ee3 (TWC-6 + TWC-7 + TWC-12 + proxy wiring)
   - Note: `feature/TWC-7` has a standalone commit (b6015e9) representing TWC-7's independent work;
     its changes are included in feature/TWC-13 5932ee3. TWC-7 branch can be abandoned.
6. Wave 3 branches → based on `feature/TWC-13`

## ⚠️ P0 Human action required — before 11 June
**Audit seed data in `src/TriviumWorldCup.Api/Data/SeedData/`:**
- Groups A–L confirmed correct from Wikipedia (June 2026)
- `FixturesData.cs` — all 72 UTC kickoff times have been cross-checked against Wikipedia group pages and are accurate.
- `PlayersData.cs` — rosters for the 9 new teams (CZE, BIH, HTI, SCO, CUW, SWE, CPV, JOR, COD) are best-effort and may need squad updates.

## Blocked — with the prerequisite needed
- **TWC-9** ingestion — football data API key / tier (Wave 3; blocks live scoring but not MVP demo)
- **TWC-20** real Entra integration — Entra app registration + Cloudflare Tunnel hostname (final story)
- **Cloudflare Tunnel token** — needed for `docker compose --profile tunnel up`; LAN demo works without it

## Known follow-ups (non-blocking for internal MVP)
- **Marten GHSA-vmw2-qwm8-x84c (Critical):** Investigate Marten 8.x / .NET 8 compatibility; upgrade before going public.
- **NuGet.config:** Repo-level override for unreachable `BluRedSelect` Azure DevOps machine feed.

## Build commands

### .NET API
```
cd src/TriviumWorldCup.Api
dotnet build      # or: dotnet build TriviumWorldCup.sln from repo root
dotnet test       # 142 tests
dotnet run        # Swagger at http://localhost:5009/swagger
```

### React Web
```
cd src/TriviumWorldCup.Web
npm install
npm run build     # production build (generates sw.js + manifest.webmanifest)
npm run dev       # dev server at http://localhost:5173
npm test          # 11 frontend tests (vitest)
```

### Full stack
```
docker compose up -d                           # LAN demo (no tunnel)
docker compose --profile tunnel up -d          # with Cloudflare Tunnel
```

## Next action
Dispatch **TWC-8** (scoring engine) on `feature/TWC-8` branched from `feature/TWC-13`.
TWC-9 follows TWC-8 but is blocked on the football data API key — mark BLOCKED until key is provided.
After TWC-8 + TWC-9, dispatch Wave 4: TWC-10 (standings) + TWC-11 (leaderboard) in parallel.
