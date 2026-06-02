# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- Current wave: **Wave 1** — TWC-3 (auth abstraction + mock provider) ‖ TWC-5 (data model + seed) — dispatching now
- Scope: MVP (label `mvp`)
- Hard deadline: **11 June 2026** (first kickoff — predictions lock)

## Accepted (Done)

### Wave 0
- **TWC-2** ✅ — Docker Compose scaffold (`feature/TWC-2`)
  - .NET 8 Minimal API, Wolverine + Marten + Npgsql wired up; `/health` + `/ping` endpoints
  - React 19 + Vite + Tailwind v4 PWA shell; nginx Dockerfile
  - `docker-compose.yml`: postgres + api + web + cloudflared (tunnel profile)
  - `.env.example`, `global.json`, `NuGet.config`, `README.md`
  - Both builds green (0 errors)

## In progress

### Wave 1
- **TWC-3** — Auth abstraction + mock provider (`feature/TWC-3`)
  - `src/TriviumWorldCup.Api/Auth/` — `AppUser`, `IIdentityProvider`, `CurrentUserMiddleware`, `AuthServiceExtensions`
  - `src/TriviumWorldCup.Api/Auth/Mock/` — `MockIdentityProvider`, `MockUsers`, `MockAuthEndpoints`
  - `src/TriviumWorldCup.Api.Tests/Auth/` — 26 tests, all passing
  - `src/TriviumWorldCup.Web/src/auth/` — `AuthContext.tsx`, `useAuth.ts`, `DevUserSwitcher.tsx`, `types.ts`
  - `App.tsx` updated to wrap with `<AuthProvider>` and render `<DevUserSwitcher>` in non-prod

## Blocked — with the prerequisite needed
- **TWC-9** ingestion — football data API key / tier (Wave 3; not blocking MVP scaffold)
- **TWC-20** real Entra integration — Entra app registration + public HTTPS tunnel hostname (final story; not MVP)
- **Cloudflare Tunnel token** — for `docker compose --profile tunnel up`; LAN demo works without it

## Known follow-ups (non-blocking for internal MVP)
- **Marten GHSA-vmw2-qwm8-x84c (Critical):** Marten 7.40.5 vulnerability. Investigate whether Marten 8.x supports .NET 8; if not, plan net9.0 upgrade before going public.
- **NuGet.config:** Repo-level config added to override unreachable `BluRedSelect` Azure DevOps machine feed. Adjust if that feed is needed for org packages.

## Build commands

### .NET API
```
cd src/TriviumWorldCup.Api
dotnet build      # or: dotnet build TriviumWorldCup.sln from repo root
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
Wave 1 in progress (parallel): TWC-3 + TWC-5. Report back when both are accepted before starting Wave 2.
