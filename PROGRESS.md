# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- **MVP COMPLETE** — all 13 MVP stories done on `feature/TWC-11`
- Hard deadline: **11 June 2026** (first kickoff — ✅ delivered with 9 days to spare)

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

## Branch to merge
All MVP work is on **`feature/TWC-11`**. Merge in this order (each must merge before the next PR is created):

1. `feature/TWC-2` → main
2. `feature/TWC-3` → main
3. `feature/TWC-5` → main
4. `feature/TWC-4` → main
5. `feature/TWC-13` → main (TWC-6/7/12/13)
6. `feature/TWC-8` → main (scoring engine)
7. `feature/TWC-11` → main (TWC-9/10/11 + all integrations)

`feature/TWC-7`, `feature/TWC-9`, `feature/TWC-10` can be abandoned — their code is in `feature/TWC-11`.

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
dotnet test TriviumWorldCup.sln   # 251 tests
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

## Next action
**MVP is done. Push `feature/TWC-11` and open PRs in branch-chain order.**
Post-MVP stories (TWC-14 knockout bracket, TWC-15 knockout scoring, TWC-16 admin, TWC-17 live updates, TWC-18 push reminders, TWC-19 backups) can follow after kickoff.
TWC-20 (real Entra) is human-gated on the Entra app registration.
