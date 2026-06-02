# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- Current wave: **Wave 2** — TWC-4, TWC-6, TWC-7, TWC-12, TWC-13 (parallel) — awaiting go-ahead
- Scope: MVP (label `mvp`)
- Hard deadline: **11 June 2026** (first kickoff — predictions and champion/Golden Six lock)

## Accepted (Done)

### Wave 0
- **TWC-2** ✅ — Docker Compose scaffold (`feature/TWC-2`)
  - .NET 8 Minimal API; React 19 + Vite + Tailwind v4 PWA shell; docker-compose.yml with postgres + api + web + cloudflared (tunnel profile)
  - Both builds green

### Wave 1
- **TWC-3** ✅ — Auth abstraction + mock provider (`feature/TWC-3`)
  - `IIdentityProvider`, `AppUser`, `CurrentUserMiddleware`, mock endpoints; `AuthContext`, `useAuth`, `DevUserSwitcher`
  - 5 seeded demo users (Alice, Bob, Charlie, Diana, Evan); production guard enforced at startup
  - 26/26 tests pass
- **TWC-5** ✅ — Tournament data model + seed (`feature/TWC-5`, based on TWC-3)
  - Domain: `Team`, `Group`, `Fixture`, `KnockoutSlot`, `Player` + enums
  - Seed: 48 teams, 12 groups, 72 group fixtures, 32 KO slots (wired R32→R16→QF→SF→3rd→Final), ~760 players
  - Idempotent; 70/70 tests pass

## ⚠️ P0 Human action required — before 11 June
**Audit seed data in `src/TriviumWorldCup.Api/Data/SeedData/`:**
- `FixturesData.cs` — all 72 kickoff times carry `// TODO: verify kickoff time`. Wrong times = predictions lock at wrong moment.
- `TeamsData.cs` / `GroupsData.cs` — non-prominent team group assignments carry `// TODO: verify draw assignment`.
- `PlayersData.cs` — 3 play-off slots and some squad entries carry `// TODO: verify`.
- Compare against the official FIFA World Cup 2026 schedule and correct any errors before the app is used for real predictions.

## Blocked — with the prerequisite needed
- **TWC-9** ingestion — football data API key / tier (Wave 3; not blocking MVP)
- **TWC-20** real Entra integration — Entra app registration + Cloudflare Tunnel hostname (final story; not MVP)
- **Cloudflare Tunnel token** — needed for `docker compose --profile tunnel up`; LAN demo works without it

## Known follow-ups (non-blocking for internal MVP)
- **Marten GHSA-vmw2-qwm8-x84c (Critical):** Investigate Marten 8.x / .NET 8 compatibility; upgrade before going public.
- **NuGet.config:** Repo-level override for unreachable `BluRedSelect` Azure DevOps machine feed.

## Branch chain (merge in this order)
1. `feature/TWC-2` → main
2. `feature/TWC-3` → main (based on TWC-2)
3. `feature/TWC-5` → main (based on TWC-3)
4. Wave 2 branches → based on `feature/TWC-5`

## Build commands

### .NET API
```
cd src/TriviumWorldCup.Api
dotnet build      # or: dotnet build TriviumWorldCup.sln from repo root
dotnet test       # 70 tests
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
Wave 2 ready to dispatch (5 parallel stories: TWC-4, TWC-6, TWC-7, TWC-12, TWC-13). Awaiting go-ahead. All Wave 2 branches should be based on `feature/TWC-5`.
