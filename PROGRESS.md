# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- Current wave: **Wave 2 (partial)** — TWC-4 done; TWC-6, TWC-7, TWC-12, TWC-13 not yet started
- Scope: MVP (label `mvp`)
- Hard deadline: **11 June 2026** (first kickoff — predictions and champion/Golden Six lock)

## Accepted (Done)

### Wave 0
- **TWC-2** ✅ — Docker Compose scaffold (`feature/TWC-2`)

### Wave 1
- **TWC-3** ✅ — Auth abstraction + mock provider (`feature/TWC-3`)
- **TWC-5** ✅ — Tournament data model + seed (`feature/TWC-5`)
  - Seed data corrected from Wikipedia; all 48 teams, 72 fixtures, 32 KO slots, ~760 players verified

### Wave 2 (partial)
- **TWC-4** ✅ — User profile: display name + country (`feature/TWC-4`)
  - UserProfile Marten document, GET/POST/PUT /profile, ISO 3166-1 country validation
  - ProfileSetupModal (non-dismissable), ProfilePage, AuthContext `hasProfile` flag
  - 117 tests passing; both builds green

## In progress
_None currently running_

## Branch chain (merge in this order)
1. `feature/TWC-2` → main
2. `feature/TWC-3` → main (based on TWC-2)
3. `feature/TWC-5` → main (based on TWC-3) — includes seed data corrections
4. `feature/TWC-4` → main (based on TWC-5)
5. Wave 2 remaining branches → based on `feature/TWC-4`

## ⚠️ P0 Human action required — before 11 June
**Audit seed data in `src/TriviumWorldCup.Api/Data/SeedData/`:**
- Groups A–L confirmed correct from Wikipedia (June 2026)
- `FixturesData.cs` — all 72 UTC kickoff times have been cross-checked against Wikipedia group pages and are accurate. No more TODO comments needed on times.
- `PlayersData.cs` — rosters for the 9 new teams (CZE, BIH, HTI, SCO, CUW, SWE, CPV, JOR, COD) are best-effort and may need squad updates.

## Blocked — with the prerequisite needed
- **TWC-9** ingestion — football data API key / tier (Wave 3; not blocking MVP)
- **TWC-20** real Entra integration — Entra app registration + Cloudflare Tunnel hostname (final story; not MVP)
- **Cloudflare Tunnel token** — needed for `docker compose --profile tunnel up`; LAN demo works without it

## Known follow-ups (non-blocking for internal MVP)
- **Marten GHSA-vmw2-qwm8-x84c (Critical):** Investigate Marten 8.x / .NET 8 compatibility; upgrade before going public.
- **NuGet.config:** Repo-level override for unreachable `BluRedSelect` Azure DevOps machine feed.

## Build commands

### .NET API
```
cd src/TriviumWorldCup.Api
dotnet build      # or: dotnet build TriviumWorldCup.sln from repo root
dotnet test       # 117 tests
dotnet run        # Swagger at http://localhost:5009/swagger
```

### React Web
```
cd src/TriviumWorldCup.Web
npm install
npm run build     # production build
npm run dev       # dev server at http://localhost:5173
```

### Full stack
```
docker compose up -d                           # LAN demo (no tunnel)
docker compose --profile tunnel up -d          # with Cloudflare Tunnel
```

## Next action
Dispatch remaining Wave 2 stories (TWC-6, TWC-7, TWC-12, TWC-13) in parallel from `feature/TWC-4`.
All depend on TWC-3 + TWC-5; TWC-4 profile data is available to all of them.
