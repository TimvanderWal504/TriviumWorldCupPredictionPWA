# Platform Generalization — Audit & Refactoring Roadmap

> **Status:** Design deliverable (Phases 1–4 of the Generalization prompt). No production code is changed by this document.
> **Goal:** Identify everything hardcoded to the FIFA World Cup 2026 and design the minimal, backward-compatible refactoring that turns TWC into a multi-sport / multi-league prediction platform.
> **Method:** Findings below were cataloged by reading the actual source (file:line + exact identifiers). Nothing here is inferred from README/docs.
> **Jira:** This roadmap is tracked under epic **[TWC-34](https://timvanderwal504.atlassian.net/browse/TWC-34)** with stories **TWC-35 – TWC-50** (GEN-1 … GEN-16). See the mapping table in Phase 3.

---

## How the app is structured today (1-paragraph orientation)

There is **no `Tournament` aggregate** — the World Cup is an *implicit singleton*. The entire structure (12 groups A–L, 48 teams, 72 group fixtures, 32 knockout slots, ~1,250 players) is **hardcoded in C# seed files** under `Data/SeedData/*` and written once at startup by `TournamentSeed.SeedAsync()` (`Program.cs:192`). Persistence is **Marten as a plain document store** (`Program.cs:32–69`) — **no event sourcing / projections** are used, so "events" (`GoalEvent`, `CardEvent`, `SubstitutionEvent`, `VarEvent`) are ordinary documents, not an event stream. Results come from a single hardcoded provider (API-Football v3, `league=1&season=2026`) polled every 30s by a Quartz job. Scoring is hardcoded as literals in three static scorer classes. The frontend is a React PWA whose branding, prediction widgets, standings math, and "Golden Six" meta-prediction are all soccer/WC-specific.

---

# Phase 1 — Audit Report (catalog of hardcoded WC assumptions)

### Category 1 — Domain Model (`src/TriviumWorldCup.Api/Domain/`)

| # | Finding | Location | Exact identifiers |
|---|---|---|---|
| D1 | **No Tournament entity.** Structure is a singleton spread across documents. | `Program.cs:38–67` | `Schema.For<Team>()…<Group>()…<Fixture>()…` |
| D2 | Team is a **national team**: 3-letter FIFA code + ISO country code + group letter. | `Team.cs:13,19,22` | `FifaCode`, `CountryCode`, `GroupLetter` |
| D3 | Group hardcodes **"one of the 12 groups (A–L)"** and **"the 4 teams in this group."** | `Group.cs:4,15–16` | `Letter`, `TeamIds` (doc-comment "4 teams") |
| D4 | Fixture identity is **MatchNumber 1–72**; result is goal-based; timing is 90-min minute clock. | `Fixture.cs:5,12,35–36,41–43,49` | `MatchNumber`, `HomeScore`, `AwayScore`, `ElapsedMinute`, `ElapsedExtra`, `FootballApiFixtureId` |
| D5 | Player has soccer positions + shirt number. | `Player.cs:17,20` + `Enums.cs:3–9` | `Position { GK, DEF, MID, FWD }`, `ShirtNumber` |
| D6 | KnockoutSlot keys are **R32-/R16-/QF-/SF-/3RD/FIN**; canonical "90-minute score"; penalty shootout scores. | `KnockoutSlot.cs:6,13,41–43,49–50,56` | `SlotKey`, `Round`, `HomeScore`/`AwayScore` (90'), `PenaltyHomeScore`, `PenaltyAwayScore`, `WinnerTeamId` |
| D7 | Knockout round set fixed to the WC bracket. | `Enums.cs:11–19` | `Round { R32, R16, QF, SF, ThirdPlace, Final }` |
| D8 | Qualification rules encode WC format incl. **BestThirdPlace** ("8 of 12 thirds"). | `SlotSource.cs:8–9` + `Enums.cs:32–39` | `SlotSourceType { GroupWinner, GroupRunnerUp, BestThirdPlace, MatchWinner, MatchLoser }` |
| D9 | MatchStatus includes soccer-only `ExtraTime`, `PenaltyShootout`. | `Enums.cs:21–30` | `MatchStatus` |
| D10 | Predictions are **exact goal scores** (W/D/L derived from sign of goal diff). | `GroupPrediction.cs:19–22`, `KnockoutPrediction.cs:4,25–28` | `HomeScore`, `AwayScore`, `PredictedHomeScore`, `PredictedAwayScore` |
| D11 | Tournament-level prediction = **champion + exactly six "Golden Six" scorers**. | `TournamentPrediction.cs:4,18–19` | `ChampionTeamId`, `GoldenSixPlayerIds` ("Exactly six") |
| D12 | MemberScore has **four fixed point buckets**. | `MemberScore.cs:16,19,22,25,28` | `GroupMatchPoints`, `ChampionPoints`, `GoldenSixPoints`, `KnockoutPoints`, `TotalPoints` |
| D13 | Event documents are soccer-specific: goal types, cards, subs, VAR; minute+stoppage clock. | `GoalEvent.cs:21–33`, `CardEvent.cs:21–32`, `SubstitutionEvent.cs:28–33`, `VarEvent.cs:22–31` | `GoalType { OpenPlay, PenaltyInMatch, Shootout, OwnGoal }`, `CardType { Yellow, SecondYellow, Red }`, `VarDecisionType {…}`, `Minute`, `ExtraMinute` |
| D14 | User identity assumes a **country** (flag avatar). | `UserProfile.cs:16`, `CountryCodes.cs:11–38` | `CountryCode`, ISO-3166 whitelist (249 codes) |

### Category 2 — API & Endpoints

| # | Finding | Location | Notes |
|---|---|---|---|
| A1 | **No tournament-creation endpoint.** Structure is seeded at boot only. | `TournamentSeed.cs:24–62`, `Program.cs:192` | Idempotency guard = "any Team exists" |
| A2 | Schedule is **not ingested as data** — it's static C# (72 fixtures, 32 slots, all 2026 dates). | `FixturesData.cs`, `KnockoutSlotsData.cs` | `Utc(2026,6/7,…)`, `new DateTimeOffset(2026,…)` |
| A3 | Group/knockout prediction submission validates **two integer scores**, no other outcome shapes. | `GroupPredictionEndpoints.cs`, `KnockoutPredictionEndpoints.cs` | `HomeScore`/`AwayScore` |
| A4 | Tournament prediction endpoint hardcodes **`GoldenSixPlayerIds.Count != 6`** and "champion". | `TournamentPredictionEndpoints.cs:191,211` | 422 on count≠6 |
| A5 | Standings/leaderboard sums the **four fixed buckets**; Golden-Six filter is goal-type specific. | `StandingsEndpoints.cs:32–62`, `LeaderboardEndpoints.cs` | excludes `GoalType.Shootout`/`OwnGoal` |

### Category 3 — Frontend (`src/TriviumWorldCup.Web/`)

| # | Finding | Location | Exact strings/identifiers |
|---|---|---|---|
| F1 | **Branding** "World Cup 2026 / TWC 2026" baked into title, nav, login, PWA manifest. | `index.html:11`, `App.tsx:66–68,166`, `vite.config.ts:22–23` | `name: 'Trivium World Cup 2026'` |
| F2 | Knockout round names/order hardcoded. | `KnockoutBracketPage.tsx:45–49` | `ROUND_ORDER`, `ROUND_LABELS` |
| F3 | Prediction UI = **two score inputs** (group) / **advancing-team radio + 90' score** (knockout). | `GroupPredictionsPage.tsx:168–186`, `KnockoutBracketPage.tsx:175–197` | "90-min score (optional)" |
| F4 | Standings math hardcodes **W/D/L + 3-1-0** points and GF/GA columns. | `GroupStandingsTable.tsx:24–86` | `buildStandings()`, `won/drawn/lost`, `points++` |
| F5 | Champion + **"exactly 6" Golden Six** UI with soccer position ordering. | `TournamentPredictionPage.tsx:10–16,268,292` | `POS_RANK`, "Golden Six" |
| F6 | Team identity → **FIFA→ISO flag** lookup everywhere. | `flagUrl.ts:1–30` + ~9 pages | `FIFA_TO_ISO`, `flagUrl()` |
| F7 | Match-event timeline is soccer-specific (HT/90'/ET, cards, subs, VAR, `m+x'`). | `MatchEvents.tsx:1–82` | `PERIOD_THRESHOLDS`, `varLabel()`, `formatMinute()` |
| F8 | Scoring explainer (`RulesPage`) hardcodes every point value & multiplier. | `RulesPage.tsx:45–172` | 10/7/3/0, ×1.0…×3.0, FWD3/MID5/DEF8/GK15 |
| F9 | User avatar **requires a country**. | `ProfilePage.tsx:181–206`, `LeaderboardPage.tsx:105–115` | `COUNTRIES`, `countryCode` |
| F10 | Hardcoded frontend grace-date override. | `TournamentPredictionPage.tsx:227` | `GRACE_DATE = '2026-06-12'` |

### Category 4 — Data Persistence / Schema

| # | Finding | Location | Notes |
|---|---|---|---|
| P1 | **No `tournamentId` scoping** on any document — schema can only hold one tournament. | `Program.cs:38–67` | every doc keyed by natural key only |
| P2 | **No Marten event sourcing.** "Events" are plain documents; no projections/streams to version. | `Program.cs:32–69` | reduces migration risk — there is no event stream to upcast |
| P3 | Composite keys assume single tournament: `"{UserId}_{FixtureId}"`, `"{UserId}_{SlotKey}"`, `MemberScore.Id == UserId`. | `Program.cs:46–61` | collisions across tournaments |
| P4 | Indexes target soccer hot paths only (Fixture.Status, etc.) — fine, but tournament-unaware. | `Program.cs:40–67` | |

### Category 5 — Business Logic (scoring / lockdown / results)

| # | Finding | Location | Hardcoded value |
|---|---|---|---|
| B1 | Group scoring literals. | `GroupMatchScorer.cs:27,38,44,74` | exact 10, GD 7, outcome 3, tally bonus 1 |
| B2 | W/D/L derivation from goal diff. | `GroupMatchScorer.cs:59–60` | `Math.Sign(predH-predA)==Math.Sign(actH-actA)` |
| B3 | Knockout scoring + per-round multipliers. | `KnockoutMatchScorer.cs:24–33,56,64` | win 5, exact 90' +3, ×1.0/1.5/2.0/2.5/3.0 |
| B4 | Golden-Six per-position points. | `GoldenSixScorer.cs:22–29` | FWD 3, MID 5, DEF 8, GK 15 |
| B5 | Champion points. | `ScoringRecomputeService.cs:232` | 100 |
| B6 | **All scoring is compile-time literals** — zero config / `IOptions`. | (all scorers) | not in `appsettings.json` |
| B7 | Lockdown = **lock at kickoff** (per match) / **earliest kickoff** (tournament pred). | `GroupPredictionEndpoints.cs:189–190`, `KnockoutPredictionEndpoints.cs:156–157`, `TournamentPredictionEndpoints.cs:146–170` | `KickoffUtc <= UtcNow` |
| B8 | Server-side grace-date override. | `TournamentPredictionEndpoints.cs:14–15,61` | `GraceDate = new(2026,6,12)` |
| B9 | FIFA group-ranking tiebreakers + **8-of-12 best thirds** + 72-fixture gate. | `KnockoutBracketResolver.cs:70,308–320,391` | pts/GD/GF/H2H; `Take(8)`; `< 72` |

### Category 6 — Integration & Data Sources

| # | Finding | Location | Hardcoded value |
|---|---|---|---|
| I1 | **League/season hardcoded as constants.** | `FootballApiClient.cs:48–49,63,74` | `LeagueId = 1`, `Season = 2026` |
| I2 | DTOs mirror API-Football v3 JSON exactly. | `FootballApiClient.cs:198–349` | `score.fulltime`, `score.penalty`, `status.short`, `goals.home/away` |
| I3 | Status/event semantics are soccer strings. | `FootballApiClient.cs:176–242` | `"FT"/"AET"/"PEN"/"1H"/"ET"`, `"Yellow Card"`, `"Own Goal"` |
| I4 | `IFootballApiClient` is mockable but **not provider-pluggable** (returns API-Football shapes; `ResultIngestionJob` consumes them directly). | `IFootballApiClient.cs:8–13`, `ResultIngestionJob.cs` | named `Football*` |
| I5 | 48-team name map is hardcoded. | `FootballApiTeamMap.cs:32–121` | |
| I6 | Quartz schedules + live window + reminder window hardcoded. | `IngestionServiceExtensions.cs:64`, `PushServiceExtensions.cs:47`, `ResultIngestionJob.cs:76–78`, `PushReminderJob.cs:42` | 30s poll, 30m reminder, ±30m live, 2h reminder |
| I7 | Auth (link provider) is already sport-agnostic. ✓ | `Auth/Link/*` | no change needed |

---

# Phase 2 — Analysis (effort / blocking / risk)

Effort: **XS** <½d · **S** ~1d · **M** 2–4d · **L** ~1wk+. Risk = chance of regressing live WC behavior.

| Audit refs | Affected layer | Current state | Generalized state | Effort | Blocking | Risk |
|---|---|---|---|---|---|---|
| D1, A1, P1, P3 | Domain, Schema, API | Implicit singleton; natural keys; no `tournamentId` | `Tournament` aggregate + `tournamentId` on every doc + composite keys include it | **L** | **Yes (root)** | High |
| A2, A1, P1 | API, Seed | Static C# seed (72 fx/32 slots) | Tournament provisioning from a `structure` definition (JSON/seed-per-tournament) | M | Yes | Medium |
| D3, F2, F4, B9 | Domain, FE, Logic | 12×4 groups, fixed rounds | Data-driven `Stage`/`Group`/`Round` definitions | M | Yes | Medium |
| D4, D6, D10, A3 | Domain, API, FE | `Home/AwayScore` + W/D/L | Generic `Outcome` model (`OutcomeType` set per sport) | **L** | Yes | **High** |
| D2, D14, F6, F9 | Domain, FE | Team=country+flag; user=country | `Competitor` w/ optional `iconUrl`; flag optional | M | No | Low |
| D11, A4, D12, F5 | Domain, API, FE | Champion + exactly-6 Golden Six | Config-driven **special predictions** (0..n, each parameterized) | M | No | Medium |
| B1–B6, F8 | Logic, FE | Literal points in scorers | Scoring rules from config (`ScoringConfig`) | M | No (after Outcome model) | Medium |
| B7, B8, F10 | Logic, FE | Lock at kickoff + grace date | `LockPolicy` per tournament (kickoff / rolling / weekly / global) | S→M | No | Low |
| I1–I5 | Integration | Hardcoded API-Football | `IResultProvider` abstraction + provider config (league/season/mapping) | **L** | No | Medium |
| I3, D9, D13 | Integration, Domain | Soccer status/event strings | Provider maps to generic `MatchStatus`/event set; soccer events become a sport plugin | M | No | Medium |
| I6 | Integration | Hardcoded intervals/windows | Config-driven schedule/windows | S | No | Low |
| F1 | FE/PWA | "World Cup 2026" literals | Branding from config/build env | S | No | Low |
| F3, F7 | FE | Score inputs + soccer timeline | Outcome-driven `<PredictionInput>`; event timeline behind sport capability flag | M | No (after Outcome model) | Medium |

---

# Phase 3 — Refactoring Epic & Stories

## Epic: **GEN — Generalize Platform to Support Multiple Sports/Leagues** — [TWC-34](https://timvanderwal504.atlassian.net/browse/TWC-34)

**Epic goal:** Remove FIFA-World-Cup-2026 hardcoding so a new sport/league/tournament can be added through configuration + a provider plugin, with **zero change to existing WC behavior**.

**Guard rails (apply to every story):** backward compatible; existing WC tests stay green; schema changes ship with a migration; behavioral changes ship behind a feature flag; surgical scope (generalization only — no new product features).

> Stories below are live in Jira project `TWC`. The `GEN-n` labels are the design IDs used throughout this doc; the real Jira keys are mapped here:
>
> | Design ID | Jira key | Design ID | Jira key |
> |---|---|---|---|
> | GEN-1 | [TWC-35](https://timvanderwal504.atlassian.net/browse/TWC-35) | GEN-9  | [TWC-43](https://timvanderwal504.atlassian.net/browse/TWC-43) |
> | GEN-2 | [TWC-36](https://timvanderwal504.atlassian.net/browse/TWC-36) | GEN-10 | [TWC-44](https://timvanderwal504.atlassian.net/browse/TWC-44) |
> | GEN-3 | [TWC-37](https://timvanderwal504.atlassian.net/browse/TWC-37) | GEN-11 | [TWC-45](https://timvanderwal504.atlassian.net/browse/TWC-45) |
> | GEN-4 | [TWC-38](https://timvanderwal504.atlassian.net/browse/TWC-38) | GEN-12 | [TWC-46](https://timvanderwal504.atlassian.net/browse/TWC-46) |
> | GEN-5 | [TWC-39](https://timvanderwal504.atlassian.net/browse/TWC-39) | GEN-13 | [TWC-47](https://timvanderwal504.atlassian.net/browse/TWC-47) |
> | GEN-6 | [TWC-40](https://timvanderwal504.atlassian.net/browse/TWC-40) | GEN-14 | [TWC-48](https://timvanderwal504.atlassian.net/browse/TWC-48) |
> | GEN-7 | [TWC-41](https://timvanderwal504.atlassian.net/browse/TWC-41) | GEN-15 | [TWC-49](https://timvanderwal504.atlassian.net/browse/TWC-49) |
> | GEN-8 | [TWC-42](https://timvanderwal504.atlassian.net/browse/TWC-42) | GEN-16 | [TWC-50](https://timvanderwal504.atlassian.net/browse/TWC-50) |

---

### GEN-1 ([TWC-35](https://timvanderwal504.atlassian.net/browse/TWC-35)) — Introduce the `Tournament` aggregate + `tournamentId` scoping
**Current State:** No `Tournament` entity. Structure is an implicit singleton; documents are keyed by natural keys only (`Program.cs:38–67`); composite keys are `"{UserId}_{FixtureId}"` etc. (`Program.cs:46–61`).
**Desired State:** A `Tournament` document (Id, Slug, DisplayName, SportKey, status, date range, `Structure` ref, `ScoringConfig` ref, `LockPolicy` ref, `ProviderConfig` ref). Every tournament-scoped document carries `TournamentId`; composite keys become `"{TournamentId}_{UserId}_{FixtureId}"`.
**Acceptance Criteria:**
- [ ] `Tournament` document type + Marten registration.
- [ ] `TournamentId` added to Team, Group, Fixture, KnockoutSlot, Player, GroupPrediction, KnockoutPrediction, TournamentPrediction, MemberScore, *Event docs.
- [ ] A default `world-cup-2026` Tournament is created by migration and back-filled onto all existing documents.
- [ ] All queries filter by the active `TournamentId`.
- [ ] All existing WC tests pass; new tests prove two tournaments' documents never collide.
**Technical Design:** Migration writes one `Tournament` row, then bulk-updates `TournamentId = 'world-cup-2026'` on all `twc.mt_doc_*` tables; widen composite-key builders. Resolve "active tournament" via route/header/config (single-tournament deployments resolve to the only one).
**Impact:** Services: all. Breaking: internal keys change (mitigated by migration). Data migration: **yes**. Feature flag: no (pure widening, WC remains the only tournament).
**Effort:** L · **Dependencies:** none (**root story**).
**Example – WC still works:** with one tournament present, every endpoint resolves `world-cup-2026` implicitly; responses are byte-identical.

---

### GEN-2 ([TWC-36](https://timvanderwal504.atlassian.net/browse/TWC-36)) — Data-driven tournament `Structure` (replace hardcoded groups/rounds)
**Current State:** 12 groups A–L of 4 (`GroupsData.cs:13–27`), 6 fixed knockout rounds (`Enums.cs:11–19`), 72-fixture gate (`KnockoutBracketResolver.cs:70`).
**Desired State:** `Structure` definition describing stages: group/league phase (n groups, m competitors each, or a single table) + bracket rounds (ordered list with labels & multipliers) + qualification rules. No counts hardcoded in logic.
**Acceptance Criteria:**
- [ ] `Round` enum replaced by data-driven round definitions (key, label, order, multiplier) referenced by `KnockoutSlot`.
- [ ] Group size/count come from `Structure`, not constants; the `< 72` gate becomes "all group fixtures completed for this tournament."
- [ ] WC structure expressed as a `Structure` instance producing the identical 12/4/72/32 layout.
- [ ] Tests: a 4-group / R16-only structure resolves correctly; WC tests pass.
**Impact:** Domain, Logic, Schema. Breaking: `Round` enum removed (mitigated: map old int values in migration). Migration: yes. Flag: no.
**Effort:** M · **Dependencies:** GEN-1.

---

### GEN-3 ([TWC-37](https://timvanderwal504.atlassian.net/browse/TWC-37)) — Generic match **Outcome** model (replace `Home/AwayScore` + W/D/L)
**Current State:** Results & predictions are two ints; W/D/L = `Math.Sign(goal diff)` (`GroupMatchScorer.cs:59–60`); 90-min clock fields (`Fixture.cs:41–43`).
**Desired State:** `Match` stores an `Outcome` keyed by `OutcomeType` defined by the sport (e.g. soccer: `{homeScore,awayScore}`; tennis: sets; binary: `winnerId`). W/D/L becomes a derived function provided by the sport plugin, not hardcoded.
**Acceptance Criteria:**
- [ ] `Outcome` value object + `ISportRules.DeriveResult(prediction, actual)` abstraction.
- [ ] Soccer plugin reproduces current goal-based outcome + W/D/L exactly.
- [ ] Predictions persist via the generic shape; WC golden-master scoring tests unchanged.
- [ ] A non-soccer (binary win/loss) sport passes new unit tests.
**Impact:** Domain, API, Logic, FE (consumes shape). Breaking: result fields reshaped (migration maps `HomeScore/AwayScore` → soccer `Outcome`). Migration: yes. Flag: yes (read-path toggle during rollout).
**Effort:** L · **Dependencies:** GEN-1. **Highest risk — do early, behind golden-master tests.**

---

### GEN-4 ([TWC-38](https://timvanderwal504.atlassian.net/browse/TWC-38)) — Generalize `Competitor` (decouple country/flag/FIFA code)
**Current State:** `Team` = FIFA code + ISO country + group letter (`Team.cs:13,19,22`); flags via `FIFA_TO_ISO` (`flagUrl.ts`).
**Desired State:** `Competitor { Id, Name, ShortCode, IconUrl?, Metadata }`. Country/flag are one optional `IconUrl` strategy; clubs/individuals supported.
**Acceptance Criteria:**
- [ ] `Competitor` replaces `Team` (alias kept for WC); `IconUrl` resolves to the flag CDN for WC competitors.
- [ ] `flagUrl()` becomes one `iconUrl` resolver; pages render `iconUrl` generically.
- [ ] WC UI visually identical.
**Impact:** Domain, FE. Breaking: no (additive + alias). Migration: backfill `IconUrl` from FIFA code. Flag: no.
**Effort:** M · **Dependencies:** GEN-1.

---

### GEN-5 ([TWC-39](https://timvanderwal504.atlassian.net/browse/TWC-39)) — Configurable **special predictions** (replace champion + "exactly 6" Golden Six)
**Current State:** `TournamentPrediction` = champion + exactly-6 scorers (`TournamentPrediction.cs:18–19`); endpoint enforces `Count != 6` (`…Endpoints.cs:191,211`); `MemberScore` has fixed buckets (`MemberScore.cs`).
**Desired State:** A tournament defines 0..n **special predictions**, each with type (`pick-one-competitor`, `pick-n-players`, …), parameters (e.g. `n=6`), and scoring. `MemberScore` buckets become a keyed map.
**Acceptance Criteria:**
- [ ] Champion → a `pick-one-competitor` special pred; Golden Six → a `pick-n-players (n=6)` special pred — both defined in WC config, not code.
- [ ] Validation reads `n` from config (no literal `6`).
- [ ] `MemberScore` exposes `PointsByCategory` map; `TotalPoints` sums it; legacy buckets kept as computed views.
- [ ] WC scoring output identical.
**Impact:** Domain, API, FE, Logic. Breaking: no (config reproduces today). Migration: map existing buckets into the map. Flag: no.
**Effort:** M · **Dependencies:** GEN-1, GEN-3 (player scoring uses events).

---

### GEN-6 ([TWC-40](https://timvanderwal504.atlassian.net/browse/TWC-40)) — Externalize **scoring rules** to configuration
**Current State:** Literals in `GroupMatchScorer` (10/7/3/1), `KnockoutMatchScorer` (5/+3/×multipliers), `GoldenSixScorer` (3/5/8/15), champion 100 (`ScoringRecomputeService.cs:232`). No config.
**Desired State:** `ScoringConfig` (bound from the Tournament) supplies all point values & multipliers; scorers read config.
**Acceptance Criteria:**
- [ ] `ScoringConfig` model + per-tournament binding.
- [ ] All three scorers + champion read from config; **zero scoring literals** remain in scorer classes.
- [ ] WC config reproduces every current value; existing scorer tests pass unchanged.
- [ ] New test: altering a config value changes points without code edits.
**Impact:** Logic, FE (RulesPage renders from config). Breaking: no. Migration: no (config seeded with WC values). Flag: no.
**Effort:** M · **Dependencies:** GEN-2 (round multipliers), GEN-5 (special-pred scoring).

---

### GEN-7 ([TWC-41](https://timvanderwal504.atlassian.net/browse/TWC-41)) — Configurable prediction **lock policy** (+ remove grace-date hack)
**Current State:** Lock at kickoff (`GroupPredictionEndpoints.cs:189–190`, `KnockoutPredictionEndpoints.cs:156–157`); tournament pred locks at earliest kickoff (`TournamentPredictionEndpoints.cs:146–170`); hardcoded `GraceDate = 2026-06-12` server + client (`…Endpoints.cs:15`, `TournamentPredictionPage.tsx:227`).
**Desired State:** `LockPolicy` per tournament: `per-match-kickoff` (default), `global-at`, `rolling-offset(minutes-before)`, `weekly`. Grace windows become first-class config, not a hardcoded date.
**Acceptance Criteria:**
- [ ] `LockPolicy` abstraction; WC uses `per-match-kickoff` → identical behavior.
- [ ] Hardcoded grace date removed from server and client.
- [ ] Lock enforced server-side (per CLAUDE.md), UI reflects the same.
- [ ] Tests for each policy.
**Impact:** Logic, API, FE. Breaking: no. Migration: no. Flag: no.
**Effort:** S–M · **Dependencies:** GEN-1.

---

### GEN-8 ([TWC-42](https://timvanderwal504.atlassian.net/browse/TWC-42)) — Pluggable **result provider** (`IResultProvider`)
**Current State:** API-Football v3 hardcoded: `LeagueId=1`, `Season=2026` (`FootballApiClient.cs:48–49`); DTOs mirror its JSON (`:198–349`); `ResultIngestionJob` consumes `Football*` shapes directly.
**Desired State:** `IResultProvider` returning **provider-neutral** match/result/event DTOs; API-Football becomes one implementation configured by `ProviderConfig { providerKey, leagueId, season, … }` on the Tournament.
**Acceptance Criteria:**
- [ ] `IResultProvider` + neutral DTOs; `ResultIngestionJob` depends only on the neutral shapes.
- [ ] `ApiFootballResultProvider` maps v3 → neutral and reads league/season from config (no constants).
- [ ] WC ingestion behaves identically (golden-master against recorded fixtures).
- [ ] A stub provider drives a non-soccer tournament in tests.
**Impact:** Integration. Breaking: no (internal). Migration: no. Flag: yes (provider selection).
**Effort:** L · **Dependencies:** GEN-3 (neutral outcome), GEN-1.

---

### GEN-9 ([TWC-43](https://timvanderwal504.atlassian.net/browse/TWC-43)) — Sport-pluggable **events & match status** (soccer as a plugin)
**Current State:** Soccer status/event strings hardcoded (`FootballApiClient.cs:176–242`); `MatchStatus.ExtraTime/PenaltyShootout` (`Enums.cs`); goal/card/sub/VAR documents (`Domain/*Event.cs`).
**Desired State:** Core `MatchStatus` is generic (`Scheduled/Live/Completed/Cancelled/Postponed`); soccer specifics (ET/penalties, goals/cards/subs/VAR, minute clock) live in a **soccer capability module** gated by `sport.capabilities`.
**Acceptance Criteria:**
- [ ] Generic status set; provider maps raw → generic + sport extension.
- [ ] Event documents flagged as a soccer capability; non-soccer tournaments don't require them.
- [ ] WC live timeline unchanged.
**Impact:** Domain, Integration, FE. Breaking: no. Migration: no. Flag: yes (capability).
**Effort:** M · **Dependencies:** GEN-8.

---

### GEN-10 ([TWC-44](https://timvanderwal504.atlassian.net/browse/TWC-44)) — Config-driven **scheduling & windows**
**Current State:** 30s poll (`IngestionServiceExtensions.cs:64`), 30m reminder (`PushServiceExtensions.cs:47`), ±30m live window (`ResultIngestionJob.cs:76–78`), 2h reminder lookahead (`PushReminderJob.cs:42`).
**Desired State:** Values come from config (per tournament/sport) with current values as defaults.
**Acceptance Criteria:**
- [ ] All four values bound from config; defaults reproduce today.
- [ ] Tests assert defaults; one override test.
**Impact:** Integration. Breaking: no. Migration: no. Flag: no.
**Effort:** S · **Dependencies:** GEN-1 (optional).

---

### GEN-11 ([TWC-45](https://timvanderwal504.atlassian.net/browse/TWC-45)) — Tournament **provisioning** (replace static C# seed)
**Current State:** `TournamentSeed.SeedAsync()` hardcodes WC in C# (`Data/SeedData/*`); runs at boot (`Program.cs:192`); guard = "any Team exists".
**Desired State:** A tournament is provisioned from a declarative definition (structure + competitors + schedule + scoring + provider) — loaded from a file/`POST /admin/tournaments`. WC becomes `world-cup-2026.json` reproducing today's seed.
**Acceptance Criteria:**
- [ ] Provisioning service consumes a tournament definition; WC definition reproduces 12/48/72/32 exactly.
- [ ] Boot seed loads the WC definition (parity test vs current seed).
- [ ] Admin can provision a second tournament without code changes.
**Impact:** API, Seed, Schema. Breaking: no. Migration: no (parity). Flag: no.
**Effort:** M · **Dependencies:** GEN-1, GEN-2.

---

### GEN-12 ([TWC-46](https://timvanderwal504.atlassian.net/browse/TWC-46)) — Tournament-agnostic **branding & PWA**
**Current State:** "World Cup 2026"/"TWC 2026" in `index.html:11`, `App.tsx:66–68,166`, `vite.config.ts:22–23`.
**Desired State:** Title, nav label, and PWA manifest name/short_name/icons come from build-time/runtime config; WC remains the default.
**Acceptance Criteria:**
- [ ] No hardcoded "World Cup 2026" strings; values sourced from config.
- [ ] Default build renders identically to today.
**Impact:** FE/PWA. Breaking: no. Migration: no. Flag: no.
**Effort:** S · **Dependencies:** none.

---

### GEN-13 ([TWC-47](https://timvanderwal504.atlassian.net/browse/TWC-47)) — **Outcome-driven** prediction UI
**Current State:** Two score inputs (`GroupPredictionsPage.tsx:168–186`); advancing-team radio + "90-min score" (`KnockoutBracketPage.tsx:175–197`).
**Desired State:** `<PredictionInput outcomeType={sport.outcomeType} />` renders the right widget (score pair, winner pick, set scores…) from sport config.
**Acceptance Criteria:**
- [ ] Soccer outcome type renders today's score inputs identically.
- [ ] A binary outcome type renders a winner picker (new test).
**Impact:** FE. Breaking: no. Migration: no. Flag: yes.
**Effort:** M · **Dependencies:** GEN-3.

---

### GEN-14 ([TWC-48](https://timvanderwal504.atlassian.net/browse/TWC-48)) — **Generic standings & rules** rendering
**Current State:** `GroupStandingsTable` hardcodes W/D/L + 3-1-0 + GF/GA (`:24–86`); `RulesPage` hardcodes all point values (`:45–172`).
**Desired State:** Standings columns & points come from sport rules/config; RulesPage renders from `ScoringConfig`.
**Acceptance Criteria:**
- [ ] Standings computed via sport rules (WC = 3-1-0, identical table).
- [ ] RulesPage values read from config (no literals).
**Impact:** FE. Breaking: no. Migration: no. Flag: no.
**Effort:** S · **Dependencies:** GEN-6.

---

### GEN-15 ([TWC-49](https://timvanderwal504.atlassian.net/browse/TWC-49)) — **Generic competitor identity** in UI (optional flags / avatars)
**Current State:** Flags everywhere via `flagUrl()`; user avatar requires a country (`ProfilePage.tsx:181–206`, `LeaderboardPage.tsx:105–115`).
**Desired State:** UI renders `competitor.iconUrl` (flag for WC); user avatar falls back to initials when no country; country selection optional.
**Acceptance Criteria:**
- [ ] Pages render `iconUrl` generically; WC shows flags as today.
- [ ] Country becomes optional for profiles; initials fallback works.
**Impact:** FE. Breaking: no. Migration: no. Flag: no.
**Effort:** S · **Dependencies:** GEN-4.

---

### GEN-16 ([TWC-50](https://timvanderwal504.atlassian.net/browse/TWC-50)) — **Knockout/qualification resolver** parameterization
**Current State:** Resolver hardcodes FIFA tiebreakers, `Take(8)` best thirds, `< 72` gate, fixed propagation (`KnockoutBracketResolver.cs:70,308–391`).
**Desired State:** Ranking tiebreakers and qualification rules (incl. "best n thirds") come from `Structure`; bracket size derived from data; resolver is structure-driven.
**Acceptance Criteria:**
- [ ] Tiebreakers & best-thirds count read from `Structure`; WC reproduces the exact bracket (golden-master vs current resolver tests).
- [ ] A simple 8-team single-elim structure resolves with no best-thirds.
**Impact:** Logic. Breaking: no. Migration: no. Flag: no.
**Effort:** M · **Dependencies:** GEN-2, GEN-3.

---

# Phase 4 — Effort Summary & Rollout

## Effort summary (T-shirt → rough points)

| Story | Effort | ~pts |
|---|---|---|
| GEN-1 Tournament aggregate + scoping | L | 8 |
| GEN-2 Data-driven structure | M | 5 |
| GEN-3 Generic outcome model | L | 8 |
| GEN-4 Competitor generalization | M | 5 |
| GEN-5 Configurable special predictions | M | 5 |
| GEN-6 Scoring config | M | 5 |
| GEN-7 Lock policy (+grace removal) | S–M | 3 |
| GEN-8 Pluggable result provider | L | 8 |
| GEN-9 Sport-pluggable events/status | M | 5 |
| GEN-10 Config-driven scheduling | S | 2 |
| GEN-11 Tournament provisioning | M | 5 |
| GEN-12 Branding/PWA | S | 2 |
| GEN-13 Outcome-driven prediction UI | M | 5 |
| GEN-14 Generic standings/rules | S | 3 |
| GEN-15 Generic competitor identity UI | S | 3 |
| GEN-16 Resolver parameterization | M | 5 |
| **Total** | | **~80 pts** |

**Estimate: ~4 sprints (3–4 devs)**, dominated by the three **L** stories (GEN-1, GEN-3, GEN-8). The absence of Marten event sourcing (P2) materially lowers migration risk.

## Rollout plan (dependency-ordered)

```
Wave A — Foundation (must land first)
  GEN-1  Tournament aggregate + tournamentId scoping     [root]
  GEN-12 Branding/PWA            (parallel, no deps)
  GEN-10 Config scheduling       (parallel)

Wave B — Core model
  GEN-2  Data-driven structure          (needs GEN-1)
  GEN-3  Generic outcome model          (needs GEN-1)   ← highest risk, golden-master first
  GEN-4  Competitor generalization      (needs GEN-1)
  GEN-7  Lock policy + grace removal    (needs GEN-1)

Wave C — Rules & predictions
  GEN-5  Special predictions            (GEN-1, GEN-3)
  GEN-6  Scoring config                 (GEN-2, GEN-5)
  GEN-16 Resolver parameterization      (GEN-2, GEN-3)

Wave D — Integration
  GEN-8  Pluggable result provider      (GEN-1, GEN-3)
  GEN-9  Sport-pluggable events/status  (GEN-8)

Wave E — Frontend generalization
  GEN-13 Outcome-driven prediction UI   (GEN-3)
  GEN-14 Generic standings/rules        (GEN-6)
  GEN-15 Generic competitor UI          (GEN-4)

Wave F — Provisioning (capstone)
  GEN-11 Tournament provisioning        (GEN-1, GEN-2)
```

**Suggested first slice to de-risk:** GEN-1 → GEN-3 (behind golden-master scoring/ingestion tests) → GEN-6. Those three prove the hardest abstractions (scoping, outcomes, scoring) without touching ingestion or UI.

---

# Phase 5 — Verification Checklist (run before closing the epic)

```
[ ] Tournament entity is parameterized (GEN-1, GEN-11)
[ ] Match outcomes are generic, not W/D/L hardcoded (GEN-3, GEN-13)
[ ] Prediction lockdown is configurable; grace-date hack removed (GEN-7)
[ ] Scoring rules are configurable; no literals in scorers (GEN-6, GEN-14)
[ ] Special predictions (champion/Golden-Six) are config-driven, not code (GEN-5)
[ ] Result sources are pluggable; league/season from config (GEN-8, GEN-10)
[ ] Soccer events/status are a capability plugin, not core (GEN-9)
[ ] Competitors decoupled from country/flag; flags optional (GEN-4, GEN-15)
[ ] Frontend branding/PWA is tournament-agnostic (GEN-12)
[ ] Schema scopes every document by tournamentId; no cross-tournament key collisions (GEN-1)
[ ] Existing WC tournament functions identically (golden-master: scoring, bracket, ingestion)
[ ] A new tournament with a different structure can be provisioned with NO code changes (GEN-11)
[ ] All .NET + frontend + E2E tests pass
```

---

## Appendix — Unrelated improvements found (flagged separately, NOT part of this epic)

Per "surgical scope," these are noted but out of scope for generalization:
- **Marten GHSA-vmw2-qwm8-x84c (Critical):** upgrade before going public (already in `PROGRESS.md`).
- **R32 bracket wiring TODOs:** several `// TODO: verify bracket wiring` entries in `KnockoutSlotsData.cs` — correctness risk for the *current* WC, independent of generalization.
- **Incomplete H2H tiebreaker:** FIFA disciplinary/drawing-of-lots final tiebreaker not implemented (`KnockoutBracketResolver.cs`).
- **Invite-user cookie revocation:** removed users keep a valid 30-day cookie (`PROGRESS.md`).
