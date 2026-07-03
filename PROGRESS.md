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

**Final: 417 .NET tests + 11 frontend tests + 16 Playwright smoke tests passing. All builds green.**

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
- **TWC-33** ✅ — Link auth provider (replaces mock): `LinkIdentityProvider`, `LinkAuthEndpoints`, admin user management (`GET/POST/DELETE /admin/users`), `InviteUsersData` seed, official 2026 squad data (1 246 players, 48 teams), GroupPredictionsPage UX (scroll indicator, auto-advance), Rules promoted to top-level tab, TWC design token system, E2E harness updated for link auth, Azure staging deployment. (`unversioned/main`, 4–5 June 2026)

### Unversioned work (main, 4–5 June 2026)
- **PlayersData.cs** — Complete rewrite with official 2026 FIFA World Cup squads for all 48 teams (1 246 players). Source: Wikipedia squads page fetched 4 June 2026 (all squads submitted by 1 June). Positions mapped exactly from Wikipedia GK/DF/MF/FW to `Position.GK/DEF/MID/FWD`. Test minimum-squad threshold raised from 15 → 23 (FIFA minimum). 351 tests still pass.
- **TWC theme / design system** — `twc-theme.css` design-token file added; `flagUrl.ts` utility added; all pages updated to use the new design tokens. TypeScript clean.
- **GroupPredictionsPage UX** — (1) Thin 3 px scroll-position indicator bar under the group tabs (ResizeObserver + scroll listener, shows thumb proportional to visible tabs). (2) Auto-advance: when every unlocked fixture in the active group has been predicted, the page transitions to the next group after 600 ms; on page load, starts on the first incomplete group rather than always Group A. (3) When group L completes, navigates automatically to the Tournament prediction page. All three features work on mobile.
- **Link auth provider — replaces mock auth entirely:**
  - New: `Auth/Link/InviteUser.cs` (Marten document), `LinkIdentityProvider.cs` (reads `twc_link_session` cookie from DB), `LinkAuthEndpoints.cs` (`GET /auth/link/login?id=` sets 30-day HttpOnly cookie + redirects to `/`; `POST /auth/link/logout`; `GET /auth/me` reports `authProvider: "link"`).
  - Admin user management: `GET/POST/DELETE /admin/users` in `AdminEndpoints`; Admin page shows Users section (create, list, copy link, delete) only when `isLinkAuth`.
  - Deleted: `Auth/Mock/` folder (all three files), `DevUserSwitcher.tsx`. `Auth:Provider` default changed to `"link"`. `ASPNETCORE_ENVIRONMENT` restored to `Production` (link auth has no production guard).
- **Admin user seed** — `InviteUsersData.cs` and `UserProfilesData.cs` read `ADMIN_USER_ID` env var at seed time and create an `InviteUser` (role: admin) + `UserProfile` (Tim, NL) as part of `TournamentSeed`. `ADMIN_USER_ID` added to `docker-compose.yml` and `.env.example` (real value lives in the secrets store, not in this file — see TWC-53).
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
- Seed admin user: navigate to `<webAppFqdn>/auth/link/login?id=<ADMIN_USER_ID>`
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

## Unversioned work (main, 18 June 2026)

### Performance hardening — live-match stability

Root cause analysis of Azure 503 errors during live matches identified CPU credit exhaustion on the `Standard_B1ms` Postgres tier and several code-level inefficiencies as the primary contributors.

**Infrastructure:**
- **`.infra/main.bicep`** — PostgreSQL SKU upgraded `Standard_B1ms` → `Standard_B2ms` (2 vCores, 8 GiB RAM, 1920 IOPS); doubles CPU credit accrual rate and triples IOPS headroom.

**Backend changes (all already present in codebase, verified 18 June 2026):**
- **Output cache** — `GET /leaderboard`, `/fixtures`, `/knockout-slots` cached for 20 s; `/teams`, `/players` for 5 min. Cache entries tagged and evicted by `ResultIngestionJob` / `ScoringRecomputeService` after each write so users never see stale scores beyond one poll cycle. (`AddOutputCache` in `Program.cs`, `CacheOutput(...)` on endpoints, `EvictByTagAsync` in job + service.)
- **Parallel reads in `ScoringRecomputeService`** — six independent `ToListAsync` queries (Fixtures, GroupPredictions, TournamentPredictions, KnockoutSlots, GoalEvents, KnockoutPredictions) run via `Task.WhenAll`; each gets its own lightweight session (Marten sessions are not thread-safe).
- **Marten indexes** — added on all hot query paths: `Fixture.Status`, `KnockoutSlot.Status`, `GroupPrediction.UserId`, `KnockoutPrediction.UserId`, `GoalEvent.FixtureId`, `CardEvent.FixtureId`, `SubstitutionEvent.FixtureId`, `VarEvent.FixtureId`.
- **Incremental scoring recompute** — `RecomputeForCompletedAsync` resolves only users who predicted the completed fixture/slot, then rescores only those users. `RecomputeAllAsync` retained as a full-sweep fallback.
- **Separate liveness / readiness probes** — `/ping` (no DB) used for liveness; `/health` (NpgSql) used for readiness in `main.bicep`. DB slowness degrades readiness without restarting the container.
- **Single shared session for post-completion pipeline** — `KnockoutBracketResolver` and `ScoringRecomputeService` both accept a caller-supplied `IDocumentSession`; `ResultIngestionJob` passes its own session through and calls `SaveChangesAsync` once after bracket + scoring writes.
- **20-second recompute debounce** — `RecomputeMinInterval = 20s` guards against back-to-back recomputes when two simultaneous group-stage matches complete within the same poll window.
- **`PlayerCache` singleton** — player roster loaded once on first use (tournament roster is static); eliminates the full `Player` table scan on every live-match poll cycle.
- **Early exit in `PropagateAllKnockoutResultsAsync`** — loads only slots with a recorded result or derivable winner; exits immediately with no writes if none exist (no-op during the entire group stage).

### Leaderboard podium (18 June 2026)

- **`pages/LeaderboardPage.tsx`** — added `PodiumSection` component. The top 3 entries are separated from the flat list and rendered as a tri-level podium: 2nd place on the left (silver), 1st in the centre (tallest, gold), 3rd on the right (bronze). Each slot shows a circular country-flag avatar with a coloured ring, display name + points above the bar, and the rank number inside the bar. Ranks 4+ continue to render as list rows below a column header. Podium slots are fully clickable for drill-down when the user is authenticated.

## Unversioned work (main, 18 June 2026)

### Loading states — spinners and skeleton UI

- **`src/components/ui/Spinner.tsx`** (new) — SVG-based spinner with three size variants: `sm` (16 px, single arc, inline beside button text), `md` (36 px, single arc, section loading), `lg` (64 px, dual-arc page-level: pitch-green outer arc rotating at 1.2 s + warning-orange inner arc counter-rotating at 1.8 s, with optional `label` prop rendered as uppercase caption). Matches the design mockup.
- **`src/components/ui/Skeleton.tsx`** (new) — `SkeletonLeaderboard` export: pulsing 3-column podium placeholder (gold/silver/bronze heights) + 7 `SkeletonLeaderboardRow` instances mirroring the real leaderboard grid (`rank | name+flag | pts`). Uses `animate-pulse` + `bg-surface-2` from the design token system.
- **All pages updated:** all 9 pages that previously showed plain text "Loading …" now render `<Spinner size="lg" label="…" />` instead.
- **LeaderboardPage** — initial load shows `<SkeletonLeaderboard />` (keeps the card shell visible while data fetches); drill-down member details shows `<Spinner size="md" />`.
- **GroupPredictionsPage** — save badge shows `<Spinner size="sm" />` + "Saving…" during the auto-save network round-trip.
- **TournamentPredictionPage**, **KnockoutBracketPage**, **ProfilePage** — submit buttons show `<Spinner size="sm" />` + "Saving…" while in flight.

## Unversioned work (main, 25 June 2026)

### Knockout bracket redesign + score-driven winner with tie-breaker

- **`pages/KnockoutBracketPage.tsx`** — replaced the bracket page with the new design: a horizontal round **progression stepper** (R32 → … → Final, each tab showing `predicted / total` or `Done ✓`), a full round label, and `SlotCard`s styled to match `FixtureCard`. Restored the real `flagUrl` / `Spinner` imports and `Clock` from `lucide-react` (the dropped-in file had inlined copies); fixed a `React.CSSProperties` reference (now `import { type CSSProperties }`) and corrected mojibake glyphs (`×`, `–`, `✓`, `→`, `·`).
- **Pick model changed** — a knockout pick is now entered as a **mandatory score per team** (same input as the group-stage `FixtureCard`, debounced **auto-save** after both scores are valid; no Save button). The advancing team is **derived from the higher score** and shown with an `Advances` pill; the losing row dims.
- **Tie-breaker** — when the two predicted scores are level, a prominent panel appears (`Tied X–X · who goes through?`) with two selectable team buttons (selected fills `--secondary-fill`). The chosen team becomes `predictedWinnerTeamId`. Until both scores are entered, a hint blocks the save.
- Read-only states unchanged in spirit: played slots show actual scores with a `Through` pill (+ penalty scores / "Won on penalties" / "After extra time"); locked slots show the user's predicted scores with an `Advances` pill, or "No pick made".

### Server-side enforcement of the new pick rules

- **`Predictions/KnockoutPredictionEndpoints.cs`** — added `ValidatePrediction(request, slot)`, now called by **POST** and **PUT** (replacing the bare `ValidateWinner`). Enforces: winner is a participant (delegates to existing `ValidateWinner`); **both scores mandatory** and non-negative; on a **decisive** scoreline the winner must be the higher-scoring team; on a **tie** either participant is accepted. Invalid payloads → `400`. `ValidateWinner` left intact.
- **`Api.Tests/Predictions/KnockoutPredictionTests.cs`** — 10 new tests (missing/negative scores, non-participant winner, lower-scoring-team mismatch, decisive home/away wins, tie + goalless-tie). **28 tests pass** in this file; frontend `npm run build` green.

## Unversioned work (main, 26 June 2026)

### Knockout bracket merged into Predict tab

- **`App.tsx`** — `bracket` removed from the `Tab` union and `ALL_TABS`; `TAB_TITLES` updated; `bracketOpen` state + `/knockout/slots` visibility-gate fetch removed. `PredictView` extended to `'group' | 'tournament' | 'knockout'`. Page title resolves to `'Knockout Bracket'` when `predictView === 'knockout'`. The By Group / By Date filter row moved to its own second row (below the sub-pills row) for better mobile layout.
- **`KnockoutBracketPage.tsx`** — retained as a component; rendered as the third sub-pill ("Knockout") inside the Predict tab, replacing the dedicated bottom-nav tab.

## Unversioned work (main, 26 June 2026)

### Knockout scoring — streak-based multiplier

- **`Scoring/KnockoutMatchScorer.cs`** — removed static `Multiplier(Round)` method and `Round` parameter from `Compute`. Advancing-team bonus is now `5 × (streakBefore + 1)` where `streakBefore` is the number of consecutive correct advancing-team predictions immediately before this match for the same user. Removed the `using TriviumWorldCup.Api.Domain` dependency (no longer needed).
- **`Scoring/ScoringRecomputeService.cs`** — Step 3 restructured. Slots are now sorted in tournament order (`Round` enum ascending, then `SlotNumber`). A per-user streak counter tracks consecutive correct advancing-team predictions; a wrong prediction resets the streak to 0, a skipped slot leaves it unchanged. The streak is passed to `KnockoutMatchScorer.Compute` for each prediction.
- **`Tests/Scoring/KnockoutMatchScorerTests.cs`** — all tests rewritten around `streakBefore` values instead of `Round` enum values. Added streak-specific tests: high-streak bonus, reset after wrong prediction, score component unaffected by streak.
- **`pages/RulesPage.tsx`** — round multiplier table replaced with a streak multiplier table; worked examples updated.
- **`public/changelog.json`** — in-app changelog entry added explaining the streak mechanic.

## Unversioned work (main, 26 June 2026)

### Bracket wiring fix + BestThirdPlace early resolver

**Root cause:** `TournamentSeed.SeedAsync` idempotency guard (`if anyTeam return`) skipped all slot seeding on existing databases, so a corrected `KnockoutSlotsData.cs` never reached the DB. Morocco was paired with Australia instead of the Netherlands because R32-2/R32-4 had stale incorrect source references.

**Fixes applied:**

- **`Data/TournamentSeed.cs`** — split into two phases: the initial full seed (guarded as before) + `MigrateKnockoutSlotsAsync` which always runs on startup and upserts slots whose wiring or metadata changed. Slots with wiring changes have `HomeTeamId`/`AwayTeamId` cleared; the resolver repopulates them on the next `POST /admin/recompute`. No other runtime state (scores, status, WinnerTeamId) is touched.

- **`Knockout/KnockoutBracketResolver.cs`** — removed the `allGroupsDone` guard on BestThirdPlace selection. The resolver now tries early injection via `ComputeMaxPossibleThirdStats`:
  - For each incomplete group, computes a conservative upper bound on the eventual 3rd-placed team's stats: excludes teams definitively locked into top-2 (when < 2 opponents can still overtake their current pts), then takes `max(gd[t] + 8 × remaining[t])` per candidate.
  - Combines real (completed group) and virtual (incomplete group) third-placed teams, sorts by pts/GD/GF.
  - If every slot in the top-8 is occupied by a real team (no virtual can displace any of them), the top-8 is mathematically locked and all BestThirdPlace slots are injected immediately — no need to wait for all 12 groups.
  - If any virtual team could still rank in the top-8, returns empty and defers until more groups complete.

- **`Api.Tests/Knockout/KnockoutBracketResolverTests.cs`** — 4 new tests added:
  - `SelectBestThirdPlaced_EarlyInjection_DefersWhenVirtualCanDisplace` — unstarted incomplete groups produce a (6 pts, +24 GD) virtual that outranks real 3-pt thirds → defer.
  - `SelectBestThirdPlaced_EarlyInjection_InjectsWhenVirtualCannotDisplace` — incomplete groups' dominant team excluded from 3rd-place candidates; virtual max GD (+6) below real 3rd GD (+18) → all 8 inject immediately.
  - `ComputeMaxPossibleThirdStats_NoGamesPlayed_ReturnsSixPtsAndBuffer` — fresh group with 6 unplayed fixtures → (6 pts, +24 GD).
  - `ComputeMaxPossibleThirdStats_FinalMatchdayGroup_ReflectsLockedThird` — 5 of 6 played, top-2 locked, 3rd-place definitively X3 → reports X3's actual locked stats (3 pts, −3 GD).

**406 tests pass.**

## Unversioned work (main, 29 June 2026)

### Knockout results in Results page

- **`Tournament/KnockoutSlotEndpoints.cs`** — new authenticated endpoint `GET /knockout-slots/results` returning completed knockout slots in tournament order (R32 → Final) with team names, 90-min scores, penalty scores, `winnerTeamId`, and the current user's prediction + points. Points computed with the same per-user streak logic as `ScoringRecomputeService` (streak resets on wrong winner, unchanged when no prediction submitted). Two new DTOs: `KnockoutSlotResultDto` and `MyKnockoutPredictionDto`.
- **`App.tsx`** — replaced `resultsViewMode: 'group' | 'date'` + 2-button toggle with `resultsTab: 'group-stage' | 'knockout' | 'by-date'` + a single 3-button row ("Group Stage" | "Knockouts" | "By Date"). One control row instead of two.
- **`pages/ResultsPage.tsx`** — prop changed from `viewMode: 'group' | 'date'` to `tab: 'group-stage' | 'knockout' | 'by-date'`. Internal stage-tab state removed. Three fully exclusive branches:
  - **Group Stage**: by-group view, group fixtures only — no knockout matches bleed in.
  - **Knockouts**: completed knockout slots grouped by round (Round of 32 → Final), rendered via new `KnockoutResultCard` showing teams with flags, 90-min score, penalty score (if applicable), winner badge with trophy icon, predicted winner + predicted score + points earned (colour-coded Correct/Wrong badge).
  - **By Date**: all results (group + knockout) merged and sorted newest-first; knockout items use `KnockoutResultCard` with round label ("Round of 32 · Venue").

### Knockout result card — per-component points breakdown

- **`Tournament/KnockoutSlotEndpoints.cs`** — `MyKnockoutPredictionDto` extended with `ScorePoints` (group-style 90-min score, 0–10), `AdvancingPoints` (5 × streak multiplier when winner correct, 0 when wrong), and `StreakMultiplier` (1 = no streak, 2 = one consecutive correct, …). Endpoint computes the two components separately via `GroupMatchScorer.Compute` + the existing streak counter; total is still `ScorePoints + AdvancingPoints`.
- **`pages/ResultsPage.tsx`** — `KnockoutResultCard` prediction section replaced with a three-row breakdown:
  - **Score row** — hidden when no score was submitted; shows group-stage label chip (Exact score / Correct diff / Right outcome / Wrong) + points.
  - **Winner row** — Correct/Wrong badge; badge reads "Correct ×N" when streak multiplier > 1; points shown alongside.
  - **Total row** — separated by a thin border, sums both components.

### Admin knockout team override + bracket sticky fix

**Context:** The bipartite matching in `AllocateBestThirds` assigned Bosnia-Herzegovina (Group B third) to R32-3 instead of R32-10 (USA vs BIH), causing that match to show "Bracket not yet set." Groups I/J/K/L were also incomplete at the time, deferring BestThirdPlace injection entirely.

- **`Admin/AdminEndpoints.cs`** — `POST /admin/knockout/{slotKey}/teams` accepts `{ homeTeamId?, awayTeamId? }` and manually sets `HomeTeamId`/`AwayTeamId` on a `KnockoutSlot`. Sets `HomeTeamOverridden`/`AwayTeamOverridden` flags so the bracket resolver cannot overwrite the admin values on subsequent recomputes. Logs to override history with `TargetType = "knockoutslot-teams"`. `DELETE /admin/overrides/{id}` handles `"knockoutslot-teams"` by clearing the team IDs and lock flags, allowing the resolver to repopulate on the next recompute.
- **`Domain/KnockoutSlot.cs`** — added `HomeTeamOverridden` and `AwayTeamOverridden` bool properties.
- **`Knockout/KnockoutBracketResolver.cs`** — `PopulateR32Slots` and all four `PropagateSlotResult` assignment branches now skip the write when the corresponding `*Overridden` flag is set.
- **`pages/AdminPage.tsx`** — "Knockout Team Override" section added between the result override and goal event override forms: slot dropdown, home/away team ID inputs (auto-uppercase), success/error feedback, slot list refresh on success.

## Unversioned work (main, 29 June 2026)

### Predictions tab — knockout matches show prediction only

- **`pages/KnockoutBracketPage.tsx`** — `SlotCard` read-only rows now always display the user's **predicted** scores and advancing team, regardless of whether the match has a result. The actual result is no longer surfaced in the predictions tab (it belongs exclusively in the Results tab). The AET/penalties footer note was also removed from `SlotCard` as it references actual match data. Locked matches without a prediction still show "No pick made".

### Leaderboard drill-down — knockout predictions

- **`Leaderboard/LeaderboardEndpoints.cs`** — drill-down endpoint (`GET /leaderboard/{userId}`) now loads `KnockoutPrediction` documents for the target user and the corresponding `KnockoutSlot` documents. Applies the same privacy filter as group predictions (only reveals once the slot's kickoff has passed). Computes points and streak multiplier per slot in tournament order (R32 → R16 → QF → SF → 3rd → Final), matching the same logic as `ScoringRecomputeService`. New `KnockoutPredictionDetailDto` record exposed (SlotKey, Round, teams, predicted winner + optional score, actual winner + result, Multiplier, Points). `MemberDrillDownDto` extended with `KnockoutPredictions` field.
- **`pages/LeaderboardPage.tsx`** — `KnockoutPredictionDetail` interface and `knockoutPredictions` field added to `MemberDrillDown`. `DrillDownPanel` now renders a "Knockout predictions" section showing each slot's matchup, predicted winner (green/red on correct/wrong), optional score prediction, actual result, streak multiplier badge (×N when > 1), and a points badge. Hidden when the user has no visible knockout predictions yet.

## Unversioned work (main, 1 July 2026)

### Scoring centralisation refactor

**Root cause addressed:** `MemberScore` stored only per-category totals; four endpoints recomputed per-prediction point breakdowns live from raw predictions + results, each using the pure scorer classes directly. This is structurally the same bug surface as the streak-multiplier bug: fix a scoring rule in one place and the other three endpoints don't get the memo.

**Changes:**

- **`Domain/MemberScore.cs`** — added three new record types (`GroupPredictionScore`, `KnockoutPredictionScore`, `GoldenSixPlayerScore`) and three corresponding `List<T>` breakdown properties on `MemberScore` (`GroupBreakdown`, `KnockoutBreakdown`, `GoldenSixBreakdown`). Marten stores them inline in the same JSON document — no schema migration; missing arrays deserialise as empty lists.

- **`Scoring/ScoringRecomputeService.cs`** — breakdown collection dictionaries added to Steps 1–3. Step 1 (group) collects `GroupPredictionScore` entries per user alongside the existing total. Step 2 (Golden Six) collects `GoldenSixPlayerScore` per player per user. Step 3 (knockout) decomposes the `KnockoutMatchScorer.Compute` call into separate `scorePoints` / `advancingPoints` / `streakMultiplier` values and collects `KnockoutPredictionScore` entries. Step 4 assigns all three lists to the `MemberScore` document. No change to totals logic or existing behaviour.

- **`Tournament/KnockoutSlotEndpoints.cs`** — removed `using TriviumWorldCup.Api.Scoring`. `GET /knockout-slots/results` no longer calls `GroupMatchScorer.Compute` or `KnockoutStreakCalculator.StreakBefore`; reads `MemberScore.KnockoutBreakdown` dictionary instead. `KnockoutPrediction` documents still loaded for predicted scores/winner IDs.

- **`Leaderboard/LeaderboardEndpoints.cs`** — removed `using TriviumWorldCup.Api.Scoring`. Drill-down endpoint replaced all three live scorer calls: group points now from `MemberScore.GroupBreakdown`; knockout streak/score from `MemberScore.KnockoutBreakdown`; Golden Six points from `MemberScore.GoldenSixBreakdown`. Player documents still loaded for name/teamId/position. Removed `predsByUserAndSlot` / `KnockoutStreakCalculator` setup.

- **`Tournament/FixtureEndpoints.cs`** — removed `using TriviumWorldCup.Api.Scoring`. `GET /fixtures/results` reads `MemberScore.GroupBreakdown` for per-fixture points instead of calling `GroupMatchScorer.Compute` inline.

- **`Standings/StandingsEndpoints.cs`** — removed `using TriviumWorldCup.Api.Scoring`. `GET /scores/me` reads `MemberScore.GoldenSixBreakdown` instead of querying `GoalEvent` and calling `GoldenSixScorer.ComputeForPlayer`. Player documents still loaded for name/teamId/position.

- **`Tests/Scoring/MemberScoreBreakdownTests.cs`** (new) — 8 tests: breakdown-total invariant for all three categories (group, knockout, Golden Six); idempotency tests; and a guardrail test (`ScoringFormulas_NotReferencedOutsideScoringNamespace`) that scans all `.cs` files under `src/TriviumWorldCup.Api/` and fails the build if `GroupMatchScorer.`, `KnockoutMatchScorer.`, `KnockoutStreakCalculator.`, or `GoldenSixScorer.` appear outside the `Scoring/` folder.

**⚠️ Required after deploy:** run `POST /admin/recompute` once to backfill the breakdown lists on all existing `MemberScore` documents. Until then, the migrated endpoints return `0` points for already-scored predictions.

**419 tests pass.**

## Unversioned work (main, 1 July 2026)

### GitHub Actions — unit tests gating deployment

- **`.github/workflows/deploy-azure.yml`** — added a `test` job that runs before `build-and-deploy`. Checks out the repo, sets up .NET 8, and runs `dotnet test TriviumWorldCup.sln --configuration Release`. The `build-and-deploy` job now has `needs: test`, so pushes to `staging` or `main` are blocked if any test fails.

## Next action
1. **Run `POST /admin/recompute`** (urgent) — three fixes now require a full recompute: the penalty-shootout goal type fix (30 June), the knockout streak-multiplier fix (30 June), and the scoring centralisation refactor (1 July, which backfills the new breakdown fields). A single recompute covers all three.
2. **Deploy B2ms Postgres upgrade** — re-run `az deployment group create` with updated `main.bicep` during a non-match window (Azure requires ~2 min downtime to resize Flexible Server).
3. **Run `POST /admin/fixtures/sync-api-ids`** — also populates `FootballApiFixtureId` on resolved knockout slots for reliable ingestion.
4. **Platform generalization Gen-Wave B** — TWC-36 (data-driven structure), TWC-37 (generic outcome model), TWC-38 (competitor generalization), TWC-41 (lock policy + grace removal). All unblocked by TWC-35 ✅.
5. **TWC-20 (Entra)** — deprioritised; may be marked obsolete. No action until decided.
6. **Update Confluence Design & Architecture page** — use the prompt in `.docs/confluence-update-prompt.md`.

## Unversioned work (main, 29 June 2026)

### Live Scores page — knockout match support

- **`Tournament/FixtureEndpoints.cs`** — `GET /fixtures/live` now also queries `KnockoutSlot` in the same time-window (InProgress / ExtraTime / PenaltyShootout, kicked off in the last 3 h, or imminent within 30 min). Knockout slot IDs (e.g. "R32-1") are folded into the existing event query — the ingestion job already stores goal/card/sub/VAR events keyed by `SlotKey`, so events flow through automatically. New `LiveKnockoutSlotDto` record (includes resolved team names, penalty scores, winner). `LiveFixturesResponse` extended with `KnockoutSlots`. `liveWindowActive` now considers knockout slot statuses and kickoff times.
- **`pages/LiveScoresPage.tsx`** — `StatusBadge` extended to render `AET` and `PEN` badges for `ExtraTime` / `PenaltyShootout` statuses. New `LiveKnockoutCard` component: round label + venue in header, penalty scores in parentheses, losing team dimmed once `winnerTeamId` is set. Live knockout matches appear at the top; completed/upcoming appear in "Earlier & upcoming".
- **`pages/KnockoutBracketPage.tsx`** — removed stale unused variables (`wentToAet`, `wonOnPens`) that were blocking the TypeScript build.

### Admin sync — knockout slot API IDs

- **`Admin/AdminEndpoints.cs`** — `POST /admin/fixtures/sync-api-ids` now matches resolved knockout slots by team pair in addition to group-stage fixtures. Both queries exclude `Completed` and `Cancelled` matches. Response extended with `matchedKnockout` and `knockoutSlots` fields.

### Admin event backfill — knockout slot support

- **`Admin/AdminEndpoints.cs`** — `POST /admin/fixtures/{fixtureId}/fetch-events` and `POST /admin/fixtures/{fixtureId}/reset-events` both now accept a knockout slot key (e.g. `R32-1`) in addition to a group-stage fixture ID. Both endpoints try `LoadAsync<Fixture>` first; if not found, fall back to `LoadAsync<KnockoutSlot>`. `FootballApiFixtureId` is resolved from whichever document matched. `EventsIngested` flag management is skipped for knockout slots (field only exists on `Fixture`). Event writing, deterministic GUIDs, player resolution, and scoring recompute are unchanged.
- **`pages/AdminPage.tsx`** — "Reset Fixture Events" section label updated to "Fixture / Slot ID", placeholder changed to `e.g. 1 or R32-1`, input widened, description updated to mention knockout slot keys.

### Leaderboard drill-down — knockout predictions redesign

- **`Leaderboard/LeaderboardEndpoints.cs`** — `GroupPredictionDetailDto` extended with `Points: int?` (computed via `GroupMatchScorer.Compute` when the actual result is available). `KnockoutPredictionDetailDto.Points` split into `ScorePoints: int?` (90-min score component, same tiers as group phase) and `WinnerPoints: int?` (5 × streak multiplier, 0 when wrong).
- **`pages/LeaderboardPage.tsx`** — drill-down UI reworked so knockout and group prediction cards share the exact same column alignment:
  - **Group phase row**: `[Home vs Away] | [Predicted] | [Result] | [Pts]` — points column added at the far right.
  - **Knockout score row**: identical `[Home vs Away] | [Predicted] | [Result] | [Pts]` layout, with `Pts` showing the score component only.
  - **Knockout winner row** (below, separated by a divider): `[Pred. winner ×N] | [Actual winner]` on the left; `[Winner ×N pts] | [Total]` on the right.

## Unversioned hotfix (main, 30 June 2026)

### Penalty shootout goals stored as in-match goals — bugfix

Root cause: admin re-ingest endpoints (`reset-events`, `fetch-events`, `fetch-all-events`) never distinguished shootout kicks from regulation penalties. API-Football emits both as `type:"Goal", detail:"Penalty"`; the only discriminator is `comments:"Penalty Shootout"`, which was never mapped. Result: every scored shootout kick was stored as `GoalType.PenaltyInMatch` and inflated Golden Six tallies, top-scorer counts, team goal totals, and standings. Missed shootout kicks were also missing the `!e.IsMissedPenalty` filter present in the live job, so they fell through as `GoalType.OpenPlay` (a miss counted as a goal).

**Changes:**

- **`Ingestion/FootballApiClient.cs`** — added `Comments` JSON property to `ApiMatchEvent`; added `IsShootout` bool discriminator (`Comments == "Penalty Shootout"`). The `comments` field is the sole API-Football marker separating a shootout kick from a regulation penalty.
- **`Ingestion/ResultIngestionJob.cs`** — added `public static GoalType ResolveGoalType(ApiMatchEvent evt)`: checks `IsShootout` first (shootout kicks also satisfy `IsPenalty`, so order matters), then `IsOwnGoal`, then `IsPenalty`, else `OpenPlay`. Replaced the inline ternary at all three job call sites (group FT, group live, slot live) with `ResolveGoalType(evt)`.
- **`Admin/AdminEndpoints.cs`** — all three admin re-ingest endpoints: added `&& !e.IsMissedPenalty` to the goal-event filter (now matches the live job); replaced the inline `IsOwnGoal ? … : IsPenalty ? … : OpenPlay` mapping with `ResultIngestionJob.ResolveGoalType(evt)`.

**After deploy:** re-run `POST /admin/fixtures/{slotKey}/reset-events` on each knockout slot that was already synced during or after a shootout. The endpoint deletes and re-stores all events with corrected types, then calls `RecomputeAllAsync` — no manual DB surgery required.

## Unversioned hotfix (main, 30 June 2026)

### Knockout streak-multiplier bug fix

**Root cause:** `ScoringRecomputeService` Step 3 tracked the streak as a global per-user counter that incremented with every consecutive correct pick regardless of which team advanced. Two different R32 matches, both winners correct, yielded `5×1` then `5×2` — both should be `5×1` because R32 is the start of each team's bracket path.

**Changes:**

- **`Scoring/KnockoutStreakCalculator.cs`** (new) — static class implementing the correct team-path streak model. `FeederSlotKeyFor` resolves the `HomeSlotSource`/`AwaySlotSource` of the advancing team to find the preceding knockout match; returns `null` when the source is not `MatchWinner` (i.e. R32 and third-place play-off). `FullStreak` recurses through the feeder chain with memoization (depth ≤ 6, so bounded). Extracted into its own class for testability without a database.
- **`Scoring/ScoringRecomputeService.cs`** — Step 3 rewritten: removed the buggy `orderedSlotKeys` / `streakByUser` global counter; now calls `KnockoutStreakCalculator.FullStreak` per (user, slot) pair with a shared memo dictionary for the recompute run.
- **`Scoring/KnockoutMatchScorer.cs`** — updated `streakBefore` XML doc comment and class summary to reflect team-path semantics (function body unchanged).
- **`Tests/Scoring/KnockoutStreakCalculatorTests.cs`** (new) — 6 unit tests covering all scenarios from the bug report: two R32 teams both correct (the reported failure case), growing streak R32→R16→QF, chain broken at R16, skipped round, third-place play-off (MatchLoser feeder → no extension), and score component correctness via the scorer. All run in memory — no database required.

**412 tests pass.**

**After deploy:** run `POST /admin/recompute` — existing `MemberScore` documents contain inflated knockout points for any user who had two or more correct picks in the same round. The recompute corrects all scores.

## Bug hardening (epic TWC-51) — Wave 1, 2 July 2026

Epic TWC-51 audits main @ 949ad00 (19 stories: 3 Critical, 3 High, 8 Medium, 4 Low, label `bug-hardening`). Orchestrated via `/orchestrate-twc`, one `twc-implementer` sub-agent per story.

- **TWC-52** ✅ — CRITICAL: removed the self-service `POST /predictions/group/inject` endpoint, which only checked `IsAuthenticated` and let any member overwrite completed-fixture predictions to gain points on the next scoring recompute. No frontend caller depended on it; the admin-gated equivalent (`POST /admin/users/{userId}/predictions/inject`, `IsInRole("admin")`) supersedes it. Added a route-table test proving the route is gone. Also added `scripts/audit-group-predictions-twc-52.sql`, a best-effort query surfacing `GroupPrediction` writes submitted after fixture kickoff for manual review — `GroupPrediction` has no field distinguishing admin-injected from self-service writes, so this can't cleanly separate legitimate backfills from exploited ones. **Open follow-up:** add a provenance field (e.g. `Source`/`InjectedByAdminUserId`) if reliable auditing is wanted. (`feature/TWC-52`, commit a7c77cb). 424/424 tests pass.
- **TWC-53** — CRITICAL, BLOCKED: exposed staging admin login credential in `PROGRESS.md`/`.env.example`. Needs a human decision (credential rotation on live staging, git history scrub or accept, repo visibility) before any code-side work.

### TWC-54 — Public leaderboard email exposure

- **`Leaderboard/LeaderboardEndpoints.cs`** — `GET /leaderboard` was unauthenticated and returned every member's email via `LeaderboardEntryDto.Email` (joined from `InviteUser`). Initial fix (commit `3619347`) removed `Email` entirely, per the Jira AC ("no email field on the public response; if needed elsewhere, admin-gated only").
- **Follow-up (same day):** product decision to restore a masked identity indicator on the public leaderboard rather than removing it outright. `LeaderboardEntryDto.Email` replaced by `MemberHandle` — computed server-side via a local `MaskEmail` helper that returns only the local-part (everything before `@`) of `InviteUser.Email`; full address/domain never serialized, no `email`-named property. This is a narrower interpretation than the original AC's "no email-derived data publicly" intent — logged as a comment on TWC-54 for visibility, issue left Done (not reopened).
- **`Leaderboard/LeaderboardEndpoints.cs`** DTO — `MemberHandle: string?` param added back to `LeaderboardEntryDto`; both population sites (ranked + unscored members) restored the `inviteUserById` lookup.
- **`Api.Tests/Leaderboard/LeaderboardEntryDtoTests.cs`** — rewritten: still asserts no `Email`-named property/JSON key at type + serialization level; new test asserts serialized JSON never contains `@`; "expected public fields" test updated for `memberHandle`.
- **`pages/LeaderboardPage.tsx`** — podium and list rows show `entry.memberHandle` (server-computed, no more client-side `.split('@')[0]`); search matches display name OR handle; search placeholder updated to "Search by name or handle…"; list header restored a `Handle` column.
- Backend: 14/14 Leaderboard tests pass. Frontend: `tsc -b && vite build` green.

## Bug hardening (epic TWC-51) — Wave 2, 2 July 2026

All five stories implemented sequentially by a single `twc-implementer` sub-agent on one shared branch `wave-2` (all touch `ResultIngestionJob.cs`, so serial execution avoided merge collisions). 451/451 backend tests pass (baseline 423 + TWC-52/54 work; net +28 new tests this wave).

- **TWC-55** ✅ — HIGH: knockout FT branch (`else // IsFullTime`) now calls `apiClient.GetAllEventsAsync`, purges stale events, and rewrites goal/card/sub/VAR events with deterministic IDs — mirrors the group FT path. Reuses existing shootout discrimination so penalty kicks aren't double-counted. Test covers a slot completing across two polls with no intermediate live capture. (commit `7f5ab09`)
- **TWC-56** ✅ — HIGH: new `ResultOverridden` flag on `Fixture` (mirrors `HomeTeamOverridden`/`AwayTeamOverridden`), set by `POST /admin/fixtures/{fixtureId}/result`, cleared by `DELETE /admin/overrides/{id}`. `ResultIngestionJob` gained a `ShouldSkipScoreUpdateForOverride` guard checked in both the FT and live-update branches before touching score/status; event fetch/backfill stays unguarded. (commit `f8ee0b6`)
- **TWC-57** ✅ — HIGH: new shared `MinuteKey(ApiTime?)` helper (`"{elapsed}:{extra ?? 0}"`) applied to every goal/card/sub deterministic-ID site in `ResultIngestionJob.cs` (group FT, group live, knockout live, knockout FT) and `BuildGoalEvent`. Test covers a same-minute brace with different `Extra` producing two surviving documents. **Follow-up filed as TWC-82** (not absorbed): `AdminEndpoints.cs`'s three admin backfill endpoints (`/fetch-events`, `/reset-events`, `/goals`) build keys independently with the same `{elapsed}`-only gap — out of this story's audited scope. (commit `5820c16`)
- **TWC-64** ✅ — MEDIUM: new pure helper `IsUnresolvableDecidingCompletion(statusShort, winnerTeamId)` — a PEN/AET completion with no derivable winner is kept out of `Completed`/`Cancelled`/`Postponed` (retried next poll) instead of silently finalized, with a clear message written to `IngestionStatusStore.LastError` pointing at the admin knockout override endpoint. Verified against TWC-55: event-fetch only runs on the normal completion path. (commit `b2a0ff8`)
- **TWC-66** ✅ — LOW: extracted `ThrowIfQuotaExceeded(HttpResponseMessage)` from `FootballApiClient.GetAllEventsAsync`, applied to `FetchFixturesAsync` too, so all API paths raise the same quota exception shape; `ResultIngestionJob` catch site updated to match. (commit `1606ea5`)

**Known gap, not in scope of this wave:** no DB-backed integration test harness exists for Marten/Postgres or `IFootballApiClient` — all ingestion tests (this wave and prior) are pure-function unit tests against extracted logic, consistent with the existing test suite's convention.

**Next (not yet dispatched):** remaining TWC-51 stories — TWC-58 through TWC-63, TWC-65 (Medium/Low, no fixed order). TWC-53 (credential rotation) remains BLOCKED on a human decision. TWC-82 (new, Low) filed as a TWC-57 follow-up.

## TWC-83 — Knockout Component 1 judged at end of extra time, not 90 minutes (3 July 2026)

**Root cause:** Reported by Tim after live matches went to extra time — knockout Component 1 (the unmultiplied score-prediction points) was judged strictly at 90 minutes even when a match continued into ET/AET/PEN, producing scores that looked wrong against the eventual result. Confirmed against the canonical Rules & Scoring page that "judged at end of normal time only" was the existing (now superseded) rule, not a bug — updated the canonical page first, then propagated.

**Changes:**
- **Confluence "Rules & Scoring (canonical)"** — Component 1 now reads: judged at 90 minutes for matches decided in normal time, or at the end of extra time for matches that went to ET/AET/PEN. Added worked example 4. Component 2 (advancing team, round-multiplied) unchanged.
- **`Ingestion/ResultIngestionJob.cs`** — new pure static helper `ResolveKnockoutScoreAtCutoff(statusShort, homeGoals, awayGoals, scoreFullTimeHome, scoreFullTimeAway)`: returns `score.fulltime` (falling back to `goals`) for normal-time completions, or `goals` (ET-inclusive, shootout-exclusive) for AET/PEN. Replaces the old always-90-min assignment at the FT branch call site. The now-redundant AET tie-break branch in winner determination was removed (`slot.HomeScore`/`AwayScore` already hold the decisive AET total).
- **`Domain/KnockoutSlot.cs`**, **`Domain/KnockoutPrediction.cs`**, **`Scoring/KnockoutMatchScorer.cs`**, **`Tournament/KnockoutSlotEndpoints.cs`** — doc comments updated from "90-minute score" to "score at the applicable cutoff." No logic changes needed in the scorer or `ScoringRecomputeService` — both already just compare whatever is in `KnockoutSlot.HomeScore/AwayScore`.
- **`pages/RulesPage.tsx`** — Component 1 heading/description/table header updated to match; worked examples updated; added the ET worked example.
- **`Api.Tests/Ingestion/ResultIngestionJobTests.cs`** — 4 new tests for `ResolveKnockoutScoreAtCutoff` (normal FT, FT fallback to goals, AET uses ET-inclusive score not 90-min, PEN uses end-of-ET score excluding the shootout).

**455 backend tests pass.** Frontend `npm run build` green; `npm test` has one pre-existing unrelated failure (`OfflineBanner.test.tsx` — copy text mismatch, fails identically on `main`, not touched by this story).

**After deploy:** run `POST /admin/recompute` — any already-completed AET/PEN knockout slot needs Component 1 rescored against the corrected cutoff. Slots decided in normal time (FT) are unaffected.
