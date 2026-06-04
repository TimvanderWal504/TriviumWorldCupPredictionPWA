# TWC — Delivery Progress

> Secondary to Jira. Project `TWC` is the source of truth for task state; this file is a fast, human-readable log for resuming the orchestrator mid-run. Update it at the end of each wave.

## Status
- MVP ✅ delivered. Post-MVP done: TWC-14 (knockout bracket), TWC-15 (knockout scoring), TWC-16 (admin), TWC-17 (live updates), TWC-18 (push notifications), TWC-19 (backups). TWC-22 E2E foundation done.
- **Knockout gap:** the bracket structure, UI, predictions and scoring exist, but **bracket population is not implemented** — nothing resolves group standings into R32 or propagates rounds, so slots stay null. Split out as new story **TWC-32** (Wave 7).
- **E2E:** TWC-22 foundation ✅ (16/16 Playwright smoke tests pass). Area specs TWC-23–TWC-31 are next.
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

### Wave 8a — E2E foundation
- **TWC-22** ✅ — Playwright harness: `e2e/` directory, 9 page objects, login/switch helper, DB seed/reset (non-prod gated), kickoff-override + result-injection endpoints, football API stub. 16/16 smoke tests pass. (`feature/TWC-17`, PR #9 merged)

### Wave 8 — E2E
- **TWC-22** ✅ — Playwright harness: `e2e/` project, mock-auth login helper, seed/reset helper, time/result control endpoints (`/e2e/*`, non-Production), page objects for all 8 screens, 16/16 smoke tests green (`feature/TWC-22-impl`)

### Wave 7
- **TWC-32** ✅ — Knockout bracket resolver: group ranking (FIFA criteria: pts/GD/GF + head-to-head), best-8-of-12 third-placed selection, R32 slot population, MatchWinner/MatchLoser round propagation, new admin endpoint `POST /admin/knockout/{slotKey}/result`, idempotent. 22 new tests; 351 total pass. (`feature/TWC-32`)

### Unversioned work (main, 4 June 2026)
- **PlayersData.cs** — Complete rewrite with official 2026 FIFA World Cup squads for all 48 teams (1 246 players). Source: Wikipedia squads page fetched 4 June 2026 (all squads submitted by 1 June). Positions mapped exactly from Wikipedia GK/DF/MF/FW to `Position.GK/DEF/MID/FWD`. Test minimum-squad threshold raised from 15 → 23 (FIFA minimum). 351 tests still pass.
- **TWC theme / design system** — `twc-theme.css` design-token file added; `flagUrl.ts` utility added; all pages (`AdminPage`, `GroupPredictionsPage`, `KnockoutBracketPage`, `LeaderboardPage`, `LiveScoresPage`, `ProfilePage`, `RulesPage`, `StandingsPage`, `TournamentPredictionPage`, `DevUserSwitcher`, `ProfileSetupModal`, `OfflineBanner`, `index.css`) updated to use the new design tokens. TypeScript clean.
- **GroupPredictionsPage UX** — (1) Thin 3 px scroll-position indicator bar under the group tabs (ResizeObserver + scroll listener, shows thumb proportional to visible tabs). (2) Auto-advance: when every unlocked fixture in the active group has been predicted, the page transitions to the next group after 600 ms; on page load, starts on the first incomplete group rather than always Group A. (3) When group L completes, navigates automatically to the Tournament prediction page. All three features work on mobile.

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

## Build commands

### .NET API
```
dotnet test TriviumWorldCup.sln   # 351 tests
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
1. **Commit work** — `PlayersData.cs` squad update, theme/design-token rollout, GroupPredictionsPage UX (scroll indicator + auto-advance + tournament navigation).
2. **Delete `.claude/design/`** — confirmed nothing in the codebase imports it; the theme output lives in `src/TriviumWorldCup.Web/src/twc-theme.css`.
3. **Wave 8** — area specs TWC-23–TWC-28 in parallel (mvp-scope); TWC-29/30 (post-mvp) in parallel; TWC-31 (knockout E2E, unblocked by TWC-32).
4. **Wave 9 (final)** — TWC-20 real Entra, once the app registration is provided.
