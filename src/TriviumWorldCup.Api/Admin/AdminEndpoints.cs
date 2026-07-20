using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Link;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Ingestion;
using TriviumWorldCup.Api.Knockout;
using TriviumWorldCup.Api.Scoring;
using TriviumWorldCup.Api.Verification;
using WebPush;

namespace TriviumWorldCup.Api.Admin;

/// <summary>
/// Admin API endpoints: ingestion health, manual result overrides, override history, recompute.
/// Every endpoint requires the caller to be in the "admin" role — returns 403 otherwise.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin").WithTags("admin");

        // ── GET /admin/ingestion ──────────────────────────────────────────────
        group.MapGet("/ingestion", async (
            HttpContext context,
            IngestionStatusStore statusStore,
            FootballApiBudget budget,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var now = DateTimeOffset.UtcNow;
            var pendingCount = await session
                .Query<Fixture>()
                .Where(f => f.Status != MatchStatus.Completed && f.KickoffUtc < now)
                .CountAsync(ct);

            return Results.Ok(new
            {
                lastSuccessfulPoll  = statusStore.LastSuccessfulPoll,
                lastAttemptedPoll   = statusStore.LastAttemptedPoll,
                lastError           = statusStore.LastError,
                totalPollCount      = statusStore.TotalPollCount,
                errorCount          = statusStore.ErrorCount,
                pendingFixtureCount = pendingCount,
                budget              = new
                {
                    enabled                = budget.Enabled,
                    maxCallsPerDay         = budget.MaxCallsPerDay,
                    minPollIntervalSeconds = budget.MinPollInterval.TotalSeconds,
                    callsToday             = budget.CallsToday,
                    lastActivePoll         = budget.LastActivePollUtc,
                },
                unmatchedEvents     = statusStore.UnmatchedEvents.Select(e => new
                {
                    e.FixtureId,
                    e.EventType,
                    e.PlayerName,
                    e.Minute,
                    e.SeenAt,
                }),
            });
        })
        .WithName("GetIngestionStatus")
        .WithSummary("Returns ingestion health and pending fixture count.");

        // ── POST /admin/ingestion/budget ──────────────────────────────────────
        // Switches the Football API polling strategy at runtime:
        //   enabled=true  → free-plan budget mode: poll at most once per minPollIntervalSeconds,
        //                   capped at maxCallsPerDay, spread so the allowance lasts a full match
        //                   through penalties.
        //   enabled=false → original pro-plan behaviour: poll every 30s, no cap.
        // maxCallsPerDay / minPollIntervalSeconds are optional overrides; omit to keep current.
        group.MapPost("/ingestion/budget", (
            HttpContext context,
            [FromBody] IngestionBudgetRequest request,
            FootballApiBudget budget) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.MaxCallsPerDay is { } cap && cap < 1)
                return Results.BadRequest(new { error = "maxCallsPerDay must be >= 1." });
            if (request.MinPollIntervalSeconds is { } secs && secs < 1)
                return Results.BadRequest(new { error = "minPollIntervalSeconds must be >= 1." });

            budget.Enabled = request.Enabled;
            if (request.MaxCallsPerDay is { } newCap)
                budget.MaxCallsPerDay = newCap;
            if (request.MinPollIntervalSeconds is { } newSecs)
                budget.MinPollInterval = TimeSpan.FromSeconds(newSecs);

            return Results.Ok(new
            {
                enabled                = budget.Enabled,
                maxCallsPerDay         = budget.MaxCallsPerDay,
                minPollIntervalSeconds = budget.MinPollInterval.TotalSeconds,
                callsToday             = budget.CallsToday,
            });
        })
        .WithName("SetIngestionBudget")
        .WithSummary("Enable/disable free-plan budget mode for Football API polling and tune its limits.");

        // ── POST /admin/fixtures/{fixtureId}/result ───────────────────────────
        group.MapPost("/fixtures/{fixtureId}/result", async (
            string fixtureId,
            HttpContext context,
            [FromBody] SetResultRequest request,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            KnockoutBracketResolver bracketResolver,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.HomeScore < 0 || request.AwayScore < 0)
                return Results.BadRequest(new { error = "Scores must be non-negative." });

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            fixture.HomeScore     = request.HomeScore;
            fixture.AwayScore     = request.AwayScore;
            fixture.Status        = request.MarkAsLive ? MatchStatus.InProgress : MatchStatus.Completed;
            fixture.ElapsedMinute = request.MarkAsLive ? request.ElapsedMinute : null;
            fixture.ElapsedExtra  = request.MarkAsLive ? request.ElapsedExtra  : null;
            // Marks the result as admin-authoritative so ResultIngestionJob does not overwrite
            // it with the API's score on a subsequent poll.
            fixture.ResultOverridden = true;
            session.Store(fixture);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "fixture",
                TargetId         = fixtureId,
                Description      = request.MarkAsLive
                    ? $"Set as InProgress with score {request.HomeScore}-{request.AwayScore}"
                    : $"Set result {request.HomeScore}-{request.AwayScore}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);

            if (!request.MarkAsLive)
            {
                // Attempt to resolve R32 bracket (exits early if group stage not yet complete).
                await bracketResolver.ResolveGroupStageAsync(ct);
                await scoringService.RecomputeAllAsync(ct);
            }

            return Results.Ok(new
            {
                fixture.Id,
                fixture.HomeTeamId,
                fixture.AwayTeamId,
                fixture.HomeScore,
                fixture.AwayScore,
                fixture.Status,
            });
        })
        .WithName("SetFixtureResult")
        .WithSummary("Manually sets the result of a group fixture and triggers recompute.");

        // ── POST /admin/fixtures/{fixtureId}/goals ────────────────────────────
        group.MapPost("/fixtures/{fixtureId}/goals", async (
            string fixtureId,
            HttpContext context,
            [FromBody] AddGoalRequest request,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!await MatchExistsAsync(session, fixtureId, ct))
                return Results.NotFound(new { error = $"No fixture or knockout slot found with ID '{fixtureId}'." });

            if (!Enum.TryParse<GoalType>(request.Type, ignoreCase: true, out var goalType))
                return Results.BadRequest(new { error = $"Invalid goal type '{request.Type}'. Valid values: OpenPlay, PenaltyInMatch, Shootout, OwnGoal." });

            // Deterministic ID based on fixtureId + playerId + minute — same as ingestion strategy
            var deterministicKey = $"admin:{fixtureId}:{request.PlayerId}:{request.Minute}";
            var goalId = DeterministicGuid(deterministicKey);

            var goalEvent = new GoalEvent
            {
                Id        = goalId,
                FixtureId = fixtureId,
                PlayerId  = request.PlayerId,
                Type      = goalType,
                Minute    = request.Minute,
            };
            session.Store(goalEvent);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "goalevent",
                TargetId         = goalId.ToString(),
                Description      = $"Added/replaced goal for player {request.PlayerId} at minute {request.Minute} ({goalType}) in fixture {fixtureId}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Created(
                $"/admin/fixtures/{fixtureId}/goals",
                new
                {
                    goalEvent.Id,
                    goalEvent.FixtureId,
                    goalEvent.PlayerId,
                    goalEvent.Type,
                    goalEvent.Minute,
                });
        })
        .WithName("AddGoalEvent")
        .WithSummary("Adds or replaces a goal event for a group fixture or knockout slot and triggers recompute.");

        // ── DELETE /admin/fixtures/{fixtureId}/goals/{goalEventId} ────────────
        group.MapDelete("/fixtures/{fixtureId}/goals/{goalEventId}", async (
            string fixtureId,
            Guid goalEventId,
            HttpContext context,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var goalEvent = await session.LoadAsync<GoalEvent>(goalEventId, ct);
            if (goalEvent is null)
                return Results.NotFound(new { error = $"GoalEvent '{goalEventId}' not found." });

            session.Delete<GoalEvent>(goalEventId);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "goalevent",
                TargetId         = goalEventId.ToString(),
                Description      = $"Deleted goal event {goalEventId} from fixture {fixtureId}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteGoalEvent")
        .WithSummary("Deletes a goal event from a fixture and triggers recompute.");

        // ── POST /admin/fixtures/{fixtureId}/cards ────────────────────────────
        group.MapPost("/fixtures/{fixtureId}/cards", async (
            string fixtureId,
            HttpContext context,
            [FromBody] AddCardRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!await MatchExistsAsync(session, fixtureId, ct))
                return Results.NotFound(new { error = $"No fixture or knockout slot found with ID '{fixtureId}'." });

            if (!Enum.TryParse<CardType>(request.Type, ignoreCase: true, out var cardType))
                return Results.BadRequest(new { error = $"Invalid card type '{request.Type}'. Valid values: Yellow, SecondYellow, Red." });

            var deterministicKey = $"admin:card:{fixtureId}:{request.PlayerId}:{cardType}:{request.Minute}";
            var cardId = DeterministicGuid(deterministicKey);

            var cardEvent = new CardEvent
            {
                Id        = cardId,
                FixtureId = fixtureId,
                PlayerId  = request.PlayerId,
                Type      = cardType,
                Minute    = request.Minute,
            };
            session.Store(cardEvent);

            session.Store(new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "cardevent",
                TargetId         = cardId.ToString(),
                Description      = $"Added/replaced {cardType} card for player {request.PlayerId} at minute {request.Minute} in fixture {fixtureId}",
            });

            await session.SaveChangesAsync(ct);

            return Results.Created(
                $"/admin/fixtures/{fixtureId}/cards",
                new { cardEvent.Id, cardEvent.FixtureId, cardEvent.PlayerId, cardEvent.Type, cardEvent.Minute });
        })
        .WithName("AddCardEvent")
        .WithSummary("Adds or replaces a card event for a group fixture or knockout slot.");

        // ── DELETE /admin/fixtures/{fixtureId}/cards/{cardEventId} ────────────
        group.MapDelete("/fixtures/{fixtureId}/cards/{cardEventId}", async (
            string fixtureId,
            Guid cardEventId,
            HttpContext context,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var cardEvent = await session.LoadAsync<CardEvent>(cardEventId, ct);
            if (cardEvent is null)
                return Results.NotFound(new { error = $"CardEvent '{cardEventId}' not found." });

            session.Delete<CardEvent>(cardEventId);

            session.Store(new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "cardevent",
                TargetId         = cardEventId.ToString(),
                Description      = $"Deleted card event {cardEventId} from fixture {fixtureId}",
            });

            await session.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteCardEvent")
        .WithSummary("Deletes a card event from a fixture.");

        // ── POST /admin/fixtures/{fixtureId}/substitutions ───────────────────
        group.MapPost("/fixtures/{fixtureId}/substitutions", async (
            string fixtureId,
            HttpContext context,
            [FromBody] AddSubstitutionRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!await MatchExistsAsync(session, fixtureId, ct))
                return Results.NotFound(new { error = $"No fixture or knockout slot found with ID '{fixtureId}'." });

            if (request.Minute < 1 || request.Minute > 130)
                return Results.BadRequest(new { error = "Minute must be between 1 and 130." });

            if (string.IsNullOrWhiteSpace(request.PlayerInName) || string.IsNullOrWhiteSpace(request.PlayerOutName))
                return Results.BadRequest(new { error = "PlayerInName and PlayerOutName are required." });

            var sub = new SubstitutionEvent
            {
                Id            = Guid.NewGuid(),
                FixtureId     = fixtureId,
                PlayerInName  = request.PlayerInName.Trim(),
                PlayerOutName = request.PlayerOutName.Trim(),
                TeamId        = request.TeamId?.Trim() ?? string.Empty,
                Minute        = request.Minute,
            };

            session.Store(sub);

            session.Store(new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "substitutionevent",
                TargetId         = sub.Id.ToString(),
                Description      = $"Added substitution {request.PlayerInName} on / {request.PlayerOutName} off at {request.Minute}' in fixture {fixtureId}",
            });

            await session.SaveChangesAsync(ct);

            return Results.Ok(new { id = sub.Id });
        })
        .WithName("AddSubstitutionEvent")
        .WithSummary("Manually adds a substitution event to a group fixture or knockout slot.");

        // ── DELETE /admin/fixtures/{fixtureId}/substitutions/{subId} ─────────
        group.MapDelete("/fixtures/{fixtureId}/substitutions/{subId:guid}", async (
            string fixtureId,
            Guid subId,
            HttpContext context,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var existing = await session.LoadAsync<SubstitutionEvent>(subId, ct);
            if (existing is null || existing.FixtureId != fixtureId)
                return Results.NotFound(new { error = $"Substitution event '{subId}' not found in fixture '{fixtureId}'." });

            session.Delete<SubstitutionEvent>(subId);

            session.Store(new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "substitutionevent",
                TargetId         = subId.ToString(),
                Description      = $"Deleted substitution event {subId} from fixture {fixtureId}",
            });

            await session.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteSubstitutionEvent")
        .WithSummary("Deletes a substitution event from a fixture.");

        // ── GET /admin/overrides ──────────────────────────────────────────────
        group.MapGet("/overrides", async (
            HttpContext context,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var overrides = await session
                .Query<ResultOverride>()
                .OrderByDescending(o => o.OverriddenAt)
                .Take(50)
                .ToListAsync(ct);

            return Results.Ok(overrides.Select(o => new
            {
                id              = o.Id,
                adminDisplayName = o.AdminDisplayName,
                overriddenAt    = o.OverriddenAt,
                targetType      = o.TargetType,
                targetId        = o.TargetId,
                description     = o.Description,
            }));
        })
        .WithName("GetOverrides")
        .WithSummary("Returns the most recent 50 manual overrides, newest first.");

        // ── DELETE /admin/overrides/{id} ──────────────────────────────────────
        // Deletes an override record and reverts the underlying data change so
        // auto-ingestion can take over again. Triggers a full recompute.
        group.MapDelete("/overrides/{id}", async (
            Guid id,
            HttpContext context,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var record = await session.LoadAsync<ResultOverride>(id, ct);
            if (record is null)
                return Results.NotFound(new { error = $"Override '{id}' not found." });

            switch (record.TargetType?.ToLowerInvariant())
            {
                case "fixture":
                    var fixture = await session.LoadAsync<Fixture>(record.TargetId, ct);
                    if (fixture is not null)
                    {
                        fixture.HomeScore        = null;
                        fixture.AwayScore        = null;
                        fixture.Status           = MatchStatus.Scheduled;
                        fixture.ResultOverridden = false;
                        session.Store(fixture);
                    }
                    break;

                case "goalevent":
                    if (Guid.TryParse(record.TargetId, out var goalId))
                        session.Delete<GoalEvent>(goalId);
                    break;

                case "cardevent":
                    if (Guid.TryParse(record.TargetId, out var cardId))
                        session.Delete<CardEvent>(cardId);
                    break;

                case "substitutionevent":
                    if (Guid.TryParse(record.TargetId, out var subId))
                        session.Delete<SubstitutionEvent>(subId);
                    break;

                case "knockoutslot":
                    var slot = await session.LoadAsync<KnockoutSlot>(record.TargetId, ct);
                    if (slot is not null)
                    {
                        slot.HomeScore    = null;
                        slot.AwayScore    = null;
                        slot.WinnerTeamId = null;
                        slot.Status       = MatchStatus.Scheduled;
                        session.Store(slot);
                    }
                    break;

                case "knockoutslot-teams":
                    var teamsSlot = await session.LoadAsync<KnockoutSlot>(record.TargetId, ct);
                    if (teamsSlot is not null)
                    {
                        teamsSlot.HomeTeamId       = null;
                        teamsSlot.HomeTeamOverridden = false;
                        teamsSlot.AwayTeamId       = null;
                        teamsSlot.AwayTeamOverridden = false;
                        session.Store(teamsSlot);
                    }
                    break;
            }

            session.Delete(record);
            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteOverride")
        .WithSummary("Deletes a manual override and reverts the underlying fixture/slot/goal to pre-override state.");

        // ── POST /admin/knockout/{slotKey}/result ─────────────────────────────
        group.MapPost("/knockout/{slotKey}/result", async (
            string slotKey,
            HttpContext context,
            [FromBody] SetKnockoutResultRequest request,
            IDocumentSession session,
            KnockoutBracketResolver bracketResolver,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.HomeScore < 0 || request.AwayScore < 0)
                return Results.BadRequest(new { error = "Scores must be non-negative." });

            var slot = await session.LoadAsync<KnockoutSlot>(slotKey, ct);
            if (slot is null)
                return Results.NotFound(new { error = $"Knockout slot '{slotKey}' not found." });

            if (slot.HomeTeamId is null || slot.AwayTeamId is null)
                return Results.UnprocessableEntity(new
                {
                    error = "Slot teams are not yet determined. Resolve the bracket first."
                });

            // WinnerTeamId is required for knockout matches.
            if (string.IsNullOrWhiteSpace(request.WinnerTeamId))
                return Results.BadRequest(new { error = "WinnerTeamId is required." });

            if (!string.Equals(request.WinnerTeamId, slot.HomeTeamId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.WinnerTeamId, slot.AwayTeamId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    error = $"WinnerTeamId must be '{slot.HomeTeamId}' or '{slot.AwayTeamId}'."
                });
            }

            slot.HomeScore    = request.HomeScore;
            slot.AwayScore    = request.AwayScore;
            slot.WinnerTeamId = request.WinnerTeamId;
            slot.Status       = MatchStatus.Completed;
            session.Store(slot);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "knockoutslot",
                TargetId         = slotKey,
                Description      = $"Set result {request.HomeScore}-{request.AwayScore}, winner {request.WinnerTeamId}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);

            // Propagate winner into downstream slots and recompute scores.
            await bracketResolver.PropagateKnockoutResultAsync(slotKey, ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                slot.SlotKey,
                slot.HomeTeamId,
                slot.AwayTeamId,
                slot.HomeScore,
                slot.AwayScore,
                slot.WinnerTeamId,
                slot.Status,
            });
        })
        .WithName("SetKnockoutSlotResult")
        .WithSummary("Manually sets the result of a knockout slot, propagates bracket, and triggers recompute.");

        // ── GET /admin/users ──────────────────────────────────────────────────
        // Returns all InviteUsers. Only meaningful when Auth:Provider = "link".
        group.MapGet("/users", async (
            HttpContext context,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();
            var users = await session.Query<InviteUser>()
                .OrderBy(u => u.DisplayName)
                .ToListAsync(ct);

            return Results.Ok(users.Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.Roles,
                u.CreatedAt,
                loginPath = $"/auth/link/login?id={u.Id}",
            }));
        })
        .WithName("GetInviteUsers")
        .WithSummary("Lists all admin-managed users (link auth provider).");

        // ── POST /admin/users ─────────────────────────────────────────────────
        group.MapPost("/users", async (
            HttpContext context,
            [FromBody] CreateUserRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return Results.BadRequest(new { error = "DisplayName is required." });

            var newUser = new InviteUser
            {
                Id          = Guid.NewGuid().ToString(),
                DisplayName = request.DisplayName.Trim(),
                Roles       = ["user"],
                CreatedAt   = DateTimeOffset.UtcNow,
            };

            session.Store(newUser);
            await session.SaveChangesAsync(ct);

            return Results.Created(
                $"/admin/users/{newUser.Id}",
                new
                {
                    newUser.Id,
                    newUser.DisplayName,
                    newUser.Roles,
                    newUser.CreatedAt,
                    loginPath = $"/auth/link/login?id={newUser.Id}",
                });
        })
        .WithName("CreateInviteUser")
        .WithSummary("Creates a new admin-managed user and returns their login link (link auth provider).");

        // ── DELETE /admin/users/{id} ──────────────────────────────────────────
        group.MapDelete("/users/{id}", async (
            string id,
            HttpContext context,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var target = await session.LoadAsync<InviteUser>(id, ct);
            if (target is null)
                return Results.NotFound(new { error = $"User '{id}' not found." });

            session.Delete<InviteUser>(id);
            await session.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteInviteUser")
        .WithSummary("Deletes an admin-managed user (link auth provider).");

        // ── POST /admin/users/{userId}/predictions/inject ────────────────────
        // Bulk-upserts explicit group-stage predictions supplied by the caller.
        // Body: array of { fixtureId, home, away } — fixture IDs are "1"–"72".
        // Lock checks are bypassed — admin privilege. Idempotent (upsert semantics).
        group.MapPost("/users/{userId}/predictions/inject", async (
            string userId,
            [FromBody] List<InjectPredictionItem> items,
            HttpContext context,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var caller = context.GetAppUser();
            if (!caller.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (items is not { Count: > 0 })
                return Results.BadRequest(new { error = "Request body must be a non-empty array." });

            // Validate all referenced fixture IDs exist
            var fixtureIds = items.Select(i => i.FixtureId).Distinct().ToList();
            var existingIds = (await session.Query<Fixture>()
                .Select(f => f.Id)
                .ToListAsync(ct))
                .ToHashSet();

            var unknown = fixtureIds.Where(id => !existingIds.Contains(id)).ToList();
            if (unknown.Count > 0)
                return Results.UnprocessableEntity(new { error = $"Unknown fixture ID(s): {string.Join(", ", unknown)}" });

            var now = DateTimeOffset.UtcNow;
            foreach (var item in items)
            {
                session.Store(new GroupPrediction
                {
                    Id          = $"{userId}_{item.FixtureId}",
                    UserId      = userId,
                    FixtureId   = item.FixtureId,
                    HomeScore   = item.Home,
                    AwayScore   = item.Away,
                    SubmittedAt = now,
                });
            }

            await session.SaveChangesAsync(ct);

            return Results.Ok(new { userId, injected = items.Count });
        })
        .WithName("InjectUserPredictions")
        .WithSummary("Upserts explicit group-stage predictions for a user. Body: [{fixtureId, home, away}]. Bypasses lock checks. Idempotent.");

        // ── POST /admin/push/test ─────────────────────────────────────────────
        // Sends a test push notification to the caller's subscriptions (or a target user's).
        group.MapPost("/push/test", async (
            HttpContext context,
            [FromBody] TestPushRequest request,
            IDocumentStore store,
            WebPushClient webPushClient,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var caller = context.GetAppUser();
            if (!caller.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var vapidPublicKey  = config["Push:VapidPublicKey"]  ?? string.Empty;
            var vapidPrivateKey = config["Push:VapidPrivateKey"] ?? string.Empty;
            var vapidSubject    = config["Push:VapidSubject"]    ?? string.Empty;

            if (string.IsNullOrWhiteSpace(vapidPublicKey) ||
                string.IsNullOrWhiteSpace(vapidPrivateKey) ||
                string.IsNullOrWhiteSpace(vapidSubject))
                return Results.Problem("VAPID keys are not configured on the server.", statusCode: 503);

            var targetUserId = string.IsNullOrWhiteSpace(request.UserId) ? caller.UserId : request.UserId;
            var title = string.IsNullOrWhiteSpace(request.Title) ? "Test notification" : request.Title;
            var body  = string.IsNullOrWhiteSpace(request.Body)  ? "This is a test notification from the admin panel." : request.Body;

            await using var session = store.LightweightSession();
            var subs = await session.Query<TriviumWorldCup.Api.Domain.PushSubscription>()
                .Where(s => s.UserId == targetUserId)
                .ToListAsync(ct);

            if (subs.Count == 0)
                return Results.Ok(new { sent = 0, message = "No push subscriptions found for this user." });

            var vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body });
            var expiredEndpoints = new List<string>();
            var sentCount = 0;

            foreach (var sub in subs)
            {
                var webPushSub = new WebPush.PushSubscription
                {
                    Endpoint = sub.Endpoint,
                    P256DH   = sub.P256dh,
                    Auth     = sub.Auth,
                };
                try
                {
                    await webPushClient.SendNotificationAsync(webPushSub, payload, vapidDetails);
                    sentCount++;
                }
                catch (WebPushException ex) when ((int)ex.StatusCode == 410)
                {
                    expiredEndpoints.Add(sub.Endpoint);
                }
                catch { /* swallow; sentCount reflects success only */ }
            }

            if (expiredEndpoints.Count > 0)
            {
                var toDelete = await session.Query<TriviumWorldCup.Api.Domain.PushSubscription>()
                    .Where(s => s.Endpoint.IsOneOf(expiredEndpoints))
                    .ToListAsync(ct);
                foreach (var s in toDelete)
                    session.Delete(s);
                await session.SaveChangesAsync(ct);
            }

            return Results.Ok(new
            {
                sent    = sentCount,
                message = $"Sent {sentCount} of {subs.Count} notification(s).",
            });
        })
        .WithName("SendTestPush")
        .WithSummary("Sends a test push notification to the caller or a target user. Admin only.");

        // ── POST /admin/fixtures/sync-api-ids ────────────────────────────────
        // Backfills FootballApiFixtureId on the specific rows named in the request body,
        // matching each to a Football API fixture by team pair. Body: { "ids": ["61","F"] }
        // — each id is a group-stage Fixture id ("1"–"72") or a knockout SlotKey ("R32-1",
        // "SF-1", …). Rows are loaded by id regardless of status, so a match that has already
        // completed (which the ingestion job and the old blanket sync both skipped) can still
        // have its id backfilled here.
        group.MapPost("/fixtures/sync-api-ids", async (
            HttpContext context,
            [FromBody] SyncApiFixtureIdsRequest request,
            IFootballApiClient apiClient,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var ids = request.Ids?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            if (ids.Count == 0)
                return Results.BadRequest(new { error = "Specify at least one fixture id or knockout slot key in 'ids'." });

            IReadOnlyList<ApiFixture> apiFixtures;
            try
            {
                apiFixtures = await apiClient.GetAllFixturesForSeasonAsync(ct);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Football API call failed: {ex.Message}", statusCode: 502);
            }

            // Index API fixtures by resolved FIFA team pair (the only linkage to our rows).
            // A pair can appear more than once when two teams meet in both the group stage and
            // a later knockout round, so keep every candidate and disambiguate by kickoff date.
            var apiByTeamPair = new Dictionary<(string, string), List<ApiFixture>>();
            foreach (var api in apiFixtures)
            {
                var homeCode = FootballApiTeamMap.Resolve(api.HomeTeamId, api.HomeTeamName);
                var awayCode = FootballApiTeamMap.Resolve(api.AwayTeamId, api.AwayTeamName);
                if (homeCode == null || awayCode == null) continue;
                if (!apiByTeamPair.TryGetValue((homeCode, awayCode), out var list))
                    apiByTeamPair[(homeCode, awayCode)] = list = [];
                list.Add(api);
            }

            // Picks the API fixture whose kickoff is nearest the row's kickoff. When the row has
            // no kickoff (e.g. an unresolved knockout slot), falls back to the latest-dated
            // candidate — the knockout meeting always follows any earlier group-stage meeting.
            static ApiFixture PickApiFixture(List<ApiFixture> candidates, DateTimeOffset? kickoff)
            {
                if (candidates.Count == 1) return candidates[0];
                DateTimeOffset? ParseDate(ApiFixture f) =>
                    DateTimeOffset.TryParse(f.Date, out var d) ? d : null;
                if (kickoff is { } k)
                    return candidates
                        .OrderBy(f => ParseDate(f) is { } d ? Math.Abs((d - k).TotalMinutes) : double.MaxValue)
                        .First();
                return candidates.OrderByDescending(f => ParseDate(f) ?? DateTimeOffset.MinValue).First();
            }

            await using var session = store.LightweightSession();

            var matchedFixtures = new List<object>();
            var matchedKnockout = new List<object>();
            var unresolved = new List<string>();

            foreach (var id in ids)
            {
                var fixture = await session.LoadAsync<Fixture>(id, ct);
                if (fixture is not null)
                {
                    if (apiByTeamPair.TryGetValue((fixture.HomeTeamId, fixture.AwayTeamId), out var candidates))
                    {
                        var api = PickApiFixture(candidates, fixture.KickoffUtc);
                        fixture.FootballApiFixtureId = api.FixtureId;
                        session.Store(fixture);
                        matchedFixtures.Add(new { fixture.Id, fixture.HomeTeamId, fixture.AwayTeamId, apiFixtureId = api.FixtureId });
                    }
                    else
                    {
                        unresolved.Add($"fixture {id} ({fixture.HomeTeamId} vs {fixture.AwayTeamId}) — no API fixture with a matching team pair");
                    }
                    continue;
                }

                var slot = await session.LoadAsync<KnockoutSlot>(id, ct);
                if (slot is null)
                {
                    unresolved.Add($"{id} — no fixture or knockout slot with this id");
                    continue;
                }

                if (slot.HomeTeamId is null || slot.AwayTeamId is null)
                {
                    unresolved.Add($"slot {id} — teams not yet determined");
                    continue;
                }

                if (apiByTeamPair.TryGetValue((slot.HomeTeamId, slot.AwayTeamId), out var slotCandidates))
                {
                    var api = PickApiFixture(slotCandidates, slot.KickoffUtc);
                    slot.FootballApiFixtureId = api.FixtureId;
                    session.Store(slot);
                    matchedKnockout.Add(new { slot.SlotKey, slot.Round, slot.HomeTeamId, slot.AwayTeamId, apiFixtureId = api.FixtureId });
                }
                else
                {
                    unresolved.Add($"slot {id} ({slot.HomeTeamId} vs {slot.AwayTeamId}) — no API fixture with a matching team pair");
                }
            }

            if (matchedFixtures.Count > 0 || matchedKnockout.Count > 0)
                await session.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                matchedFixtures  = matchedFixtures.Count,
                fixtures         = matchedFixtures,
                matchedKnockout  = matchedKnockout.Count,
                knockoutSlots    = matchedKnockout,
                unresolved,
            });
        })
        .WithName("SyncApiFixtureIds")
        .WithSummary("Backfills FootballApiFixtureId on the named group-stage Fixture and/or KnockoutSlot rows from the Football API.");

        // ── POST /admin/fixtures/{fixtureId}/fetch-events ────────────────────
        // Fetches events directly from the Football API for any completed fixture,
        // regardless of how long ago it was played. Use to backfill goals/cards/subs
        // that the ingestion job missed because the fixture's date fell outside the
        // live-window fetch scope. Idempotent: deterministic IDs mean re-running is safe.
        group.MapPost("/fixtures/{fixtureId}/fetch-events", async (
            string fixtureId,
            HttpContext context,
            IFootballApiClient apiClient,
            IDocumentStore store,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            KnockoutSlot? knockoutSlot = null;
            if (fixture is null)
                knockoutSlot = await session.LoadAsync<KnockoutSlot>(fixtureId, ct);

            if (fixture is null && knockoutSlot is null)
                return Results.NotFound(new { error = $"No fixture or knockout slot found with ID '{fixtureId}'." });

            var apiId = fixture?.FootballApiFixtureId ?? knockoutSlot?.FootballApiFixtureId;
            if (apiId is null)
                return Results.UnprocessableEntity(new
                {
                    error = "No FootballApiFixtureId — call POST /admin/fixtures/sync-api-ids first."
                });

            IReadOnlyList<ApiMatchEvent> allEvents;
            try
            {
                allEvents = await apiClient.GetAllEventsAsync(apiId.Value, ct);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Football API call failed: {ex.Message}", statusCode: 502);
            }

            var allPlayers = await session.Query<Player>().ToListAsync(ct);
            var playerByName = allPlayers
                .GroupBy(p => ResultIngestionJob.StripDiacritics(p.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var playersByLastName = allPlayers
                .ToLookup(p => ResultIngestionJob.LastWord(p.Name), StringComparer.OrdinalIgnoreCase);

            var goalEvents = allEvents.Where(e => e.IsGoal && !e.IsMissedPenalty).ToList();
            var varEvents  = allEvents.Where(e => e.IsVar).ToList();
            var cardEvents = allEvents.Where(e => e.IsCard).ToList();
            var subEvents  = allEvents.Where(e => e.IsSub).ToList();

            var ns = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
            var goalsStored = 0;
            var cardsStored = 0;
            var playerMisses = new List<string>();

            foreach (var evt in goalEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                var player = ResultIngestionJob.ResolvePlayer(playerName, playerByName, playersByLastName);
                if (player is null)
                {
                    playerMisses.Add($"goal: {playerName}");
                    continue;
                }
                var goalType = ResultIngestionJob.ResolveGoalType(evt);
                var goalId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"{apiId}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new GoalEvent
                {
                    Id          = goalId,
                    FixtureId   = fixtureId,
                    PlayerId    = player.Id,
                    Type        = goalType,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
                goalsStored++;
            }

            foreach (var evt in cardEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                if (!evt.IsYellow && !evt.IsSecondYellow && !evt.IsRed) continue;
                var player = ResultIngestionJob.ResolvePlayer(playerName, playerByName, playersByLastName);
                if (player is null)
                {
                    playerMisses.Add($"card: {playerName}");
                    continue;
                }
                var cardType = evt.IsSecondYellow ? CardType.SecondYellow : evt.IsRed ? CardType.Red : CardType.Yellow;
                var cardId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"card:{apiId}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new CardEvent
                {
                    Id          = cardId,
                    FixtureId   = fixtureId,
                    PlayerId    = player.Id,
                    Type        = cardType,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
                cardsStored++;
            }

            foreach (var evt in subEvents)
            {
                var playerOutName = evt.Player?.Name ?? string.Empty;
                var playerInName  = evt.Assist?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerOutName) && string.IsNullOrWhiteSpace(playerInName)) continue;

                var playerOut = ResultIngestionJob.ResolvePlayer(playerOutName, playerByName, playersByLastName);
                var playerIn  = ResultIngestionJob.ResolvePlayer(playerInName,  playerByName, playersByLastName);
                var teamFifaCode = evt.Team?.Name is { } tn
                    ? FootballApiTeamMap.Resolve(evt.Team.Id, tn) ?? string.Empty
                    : string.Empty;
                var subId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"sub:{apiId}:{playerOutName}:{playerInName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new SubstitutionEvent
                {
                    Id            = subId,
                    FixtureId     = fixtureId,
                    PlayerOutId   = playerOut?.Id,
                    PlayerInId    = playerIn?.Id,
                    PlayerOutName = playerOutName,
                    PlayerInName  = playerInName,
                    TeamId        = teamFifaCode,
                    Minute        = evt.Time?.Elapsed ?? 0,
                    ExtraMinute   = evt.Time?.Extra,
                });
            }

            foreach (var evt in varEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                VarDecisionType? decType =
                    evt.IsGoalCancelled     ? VarDecisionType.GoalCancelled :
                    evt.IsCardUpgradeRed    ? VarDecisionType.CardUpgradeRed :
                    evt.IsCardUpgrade2ndYel ? VarDecisionType.CardUpgradeSecondYellow :
                    null;
                if (decType is null) continue;
                var varTeam = evt.Team?.Name is { } vtn
                    ? FootballApiTeamMap.Resolve(evt.Team.Id, vtn) ?? string.Empty
                    : string.Empty;
                var varId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"var:{apiId}:{decType}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new VarEvent
                {
                    Id          = varId,
                    FixtureId   = fixtureId,
                    Type        = decType.Value,
                    PlayerName  = playerName,
                    TeamId      = varTeam,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
            }

            if (fixture is not null)
            {
                fixture.EventsIngested = true;
                session.Store(fixture);
            }
            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                fixtureId,
                footballApiFixtureId = apiId,
                totalEvents          = allEvents.Count,
                goalsStored,
                cardsStored,
                subsStored           = subEvents.Count,
                playerMisses,
            });
        })
        .WithName("FetchFixtureEvents")
        .WithSummary("Fetches events from the Football API for any completed fixture or knockout slot and writes them. Idempotent.");

        // ── POST /admin/fixtures/{fixtureId}/reset-events ───────────────────
        // Wipes ALL existing goal, card, and substitution events for the fixture,
        // then immediately re-fetches them from the Football API. Use when events
        // appear duplicated or incorrect — gives a clean slate before re-ingestion.
        group.MapPost("/fixtures/{fixtureId}/reset-events", async (
            string fixtureId,
            HttpContext context,
            IFootballApiClient apiClient,
            IDocumentStore store,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            KnockoutSlot? knockoutSlot = null;
            if (fixture is null)
                knockoutSlot = await session.LoadAsync<KnockoutSlot>(fixtureId, ct);

            if (fixture is null && knockoutSlot is null)
                return Results.NotFound(new { error = $"No fixture or knockout slot found with ID '{fixtureId}'." });

            var apiId = fixture?.FootballApiFixtureId ?? knockoutSlot?.FootballApiFixtureId;
            if (apiId is null)
                return Results.UnprocessableEntity(new
                {
                    error = "No FootballApiFixtureId — call POST /admin/fixtures/sync-api-ids first."
                });

            var existingGoals = await session.Query<GoalEvent>()
                .Where(g => g.FixtureId == fixtureId).ToListAsync(ct);
            var existingCards = await session.Query<CardEvent>()
                .Where(c => c.FixtureId == fixtureId).ToListAsync(ct);
            var existingSubs = await session.Query<SubstitutionEvent>()
                .Where(s => s.FixtureId == fixtureId).ToListAsync(ct);
            var existingVars = await session.Query<VarEvent>()
                .Where(v => v.FixtureId == fixtureId).ToListAsync(ct);

            foreach (var g in existingGoals) session.Delete(g);
            foreach (var c in existingCards) session.Delete(c);
            foreach (var s in existingSubs) session.Delete(s);
            foreach (var v in existingVars) session.Delete(v);

            if (fixture is not null)
            {
                fixture.EventsIngested = false;
                session.Store(fixture);
            }
            await session.SaveChangesAsync(ct);

            IReadOnlyList<ApiMatchEvent> allEvents;
            try
            {
                allEvents = await apiClient.GetAllEventsAsync(apiId.Value, ct);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    $"Events cleared but Football API call failed: {ex.Message}. The ingestion job will backfill on the next cycle.",
                    statusCode: 502);
            }

            var allPlayers = await session.Query<Player>().ToListAsync(ct);
            var playerByName = allPlayers
                .GroupBy(p => ResultIngestionJob.StripDiacritics(p.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var playersByLastName = allPlayers
                .ToLookup(p => ResultIngestionJob.LastWord(p.Name), StringComparer.OrdinalIgnoreCase);

            var goalEvents = allEvents.Where(e => e.IsGoal && !e.IsMissedPenalty).ToList();
            var varEvents  = allEvents.Where(e => e.IsVar).ToList();
            var cardEvents = allEvents.Where(e => e.IsCard).ToList();
            var subEvents  = allEvents.Where(e => e.IsSub).ToList();

            var ns = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
            var goalsStored = 0;
            var cardsStored = 0;
            var playerMisses = new List<string>();

            foreach (var evt in goalEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                var player = ResultIngestionJob.ResolvePlayer(playerName, playerByName, playersByLastName);
                if (player is null)
                {
                    playerMisses.Add($"goal: {playerName}");
                    continue;
                }
                var goalType = ResultIngestionJob.ResolveGoalType(evt);
                var goalId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"{apiId}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new GoalEvent
                {
                    Id          = goalId,
                    FixtureId   = fixtureId,
                    PlayerId    = player.Id,
                    Type        = goalType,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
                goalsStored++;
            }

            foreach (var evt in cardEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                if (!evt.IsYellow && !evt.IsSecondYellow && !evt.IsRed) continue;
                var player = ResultIngestionJob.ResolvePlayer(playerName, playerByName, playersByLastName);
                if (player is null)
                {
                    playerMisses.Add($"card: {playerName}");
                    continue;
                }
                var cardType = evt.IsSecondYellow ? CardType.SecondYellow : evt.IsRed ? CardType.Red : CardType.Yellow;
                var cardId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"card:{apiId}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new CardEvent
                {
                    Id          = cardId,
                    FixtureId   = fixtureId,
                    PlayerId    = player.Id,
                    Type        = cardType,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
                cardsStored++;
            }

            foreach (var evt in subEvents)
            {
                var playerOutName = evt.Player?.Name ?? string.Empty;
                var playerInName  = evt.Assist?.Name  ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerOutName) && string.IsNullOrWhiteSpace(playerInName)) continue;
                var playerOut = ResultIngestionJob.ResolvePlayer(playerOutName, playerByName, playersByLastName);
                var playerIn  = ResultIngestionJob.ResolvePlayer(playerInName,  playerByName, playersByLastName);
                var teamFifaCode = evt.Team?.Name is { } tn
                    ? FootballApiTeamMap.Resolve(evt.Team.Id, tn) ?? string.Empty
                    : string.Empty;
                var subId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"sub:{apiId}:{playerOutName}:{playerInName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new SubstitutionEvent
                {
                    Id            = subId,
                    FixtureId     = fixtureId,
                    PlayerOutId   = playerOut?.Id,
                    PlayerInId    = playerIn?.Id,
                    PlayerOutName = playerOutName,
                    PlayerInName  = playerInName,
                    TeamId        = teamFifaCode,
                    Minute        = evt.Time?.Elapsed ?? 0,
                    ExtraMinute   = evt.Time?.Extra,
                });
            }

            foreach (var evt in varEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                VarDecisionType? decType =
                    evt.IsGoalCancelled     ? VarDecisionType.GoalCancelled :
                    evt.IsCardUpgradeRed    ? VarDecisionType.CardUpgradeRed :
                    evt.IsCardUpgrade2ndYel ? VarDecisionType.CardUpgradeSecondYellow :
                    null;
                if (decType is null) continue;
                var varTeam = evt.Team?.Name is { } vtn
                    ? FootballApiTeamMap.Resolve(evt.Team.Id, vtn) ?? string.Empty
                    : string.Empty;
                var varId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"var:{apiId}:{decType}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                session.Store(new VarEvent
                {
                    Id          = varId,
                    FixtureId   = fixtureId,
                    Type        = decType.Value,
                    PlayerName  = playerName,
                    TeamId      = varTeam,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
            }

            if (fixture is not null)
            {
                fixture.EventsIngested = true;
                session.Store(fixture);
            }
            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                fixtureId,
                footballApiFixtureId = apiId,
                deletedGoals  = existingGoals.Count,
                deletedCards  = existingCards.Count,
                deletedSubs   = existingSubs.Count,
                deletedVars   = existingVars.Count,
                totalApiEvents = allEvents.Count,
                goalsStored,
                cardsStored,
                subsStored    = subEvents.Count,
                playerMisses,
            });
        })
        .WithName("ResetFixtureEvents")
        .WithSummary("Deletes all events for a fixture then re-fetches them from the Football API. Fixes duplicated or incorrect events.");

        // ── POST /admin/fixtures/fetch-all-events ────────────────────────────
        // Bulk-fetches events (goals, cards, subs) from the Football API for all
        // completed group-stage fixtures and upserts them into Marten using the same
        // deterministic IDs as the individual /fetch-events endpoint — idempotent.
        // Use onlyMissing=true to skip fixtures that already have EventsIngested=true
        // and conserve the daily API-Football quota (100 requests/day on the free plan).
        group.MapPost("/fixtures/fetch-all-events", async (
            HttpContext context,
            [FromQuery] bool onlyMissing,
            IFootballApiClient apiClient,
            IDocumentStore store,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var allCompleted = await session
                .Query<Fixture>()
                .Where(f => f.Status == MatchStatus.Completed && f.FootballApiFixtureId != null)
                .ToListAsync(ct);

            var toProcess = onlyMissing
                ? allCompleted.Where(f => !f.EventsIngested).ToList()
                : allCompleted;

            if (toProcess.Count == 0)
                return Results.Ok(new
                {
                    message        = "No fixtures to process.",
                    processed      = 0,
                    total          = allCompleted.Count,
                    quotaExhausted = false,
                });

            var allPlayers = await session.Query<Player>().ToListAsync(ct);
            var playerByName = allPlayers
                .GroupBy(p => ResultIngestionJob.StripDiacritics(p.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var playersByLastName = allPlayers
                .ToLookup(p => ResultIngestionJob.LastWord(p.Name), StringComparer.OrdinalIgnoreCase);

            var ns           = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
            var totalGoals   = 0;
            var totalCards   = 0;
            var totalSubs    = 0;
            var processed    = 0;
            var skipped      = new List<string>();
            var playerMisses = new List<string>();
            var quotaExhausted = false;

            foreach (var fixture in toProcess)
            {
                IReadOnlyList<ApiMatchEvent> allEvents;
                try
                {
                    allEvents = await apiClient.GetAllEventsAsync(fixture.FootballApiFixtureId!.Value, ct);
                }
                catch (HttpRequestException ex) when (ex.InnerException is InvalidOperationException { Message: "Quota exceeded" })
                {
                    quotaExhausted = true;
                    break;
                }
                catch (Exception ex)
                {
                    skipped.Add($"{fixture.Id}: {ex.Message}");
                    continue;
                }

                var goalEvents = allEvents.Where(e => e.IsGoal && !e.IsMissedPenalty).ToList();
                var varEvents  = allEvents.Where(e => e.IsVar).ToList();
                var cardEvents = allEvents.Where(e => e.IsCard).ToList();
                var subEvents  = allEvents.Where(e => e.IsSub).ToList();

                foreach (var evt in goalEvents)
                {
                    if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                    var player = ResultIngestionJob.ResolvePlayer(playerName, playerByName, playersByLastName);
                    if (player is null)
                    {
                        playerMisses.Add($"{fixture.Id}/goal: {playerName}");
                        continue;
                    }
                    var goalType = ResultIngestionJob.ResolveGoalType(evt);
                    var goalId   = ResultIngestionJob.CreateDeterministicGuid(ns,
                        $"{fixture.FootballApiFixtureId}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                    session.Store(new GoalEvent
                    {
                        Id          = goalId,
                        FixtureId   = fixture.Id,
                        PlayerId    = player.Id,
                        Type        = goalType,
                        Minute      = evt.Time?.Elapsed ?? 0,
                        ExtraMinute = evt.Time?.Extra,
                    });
                    totalGoals++;
                }

                foreach (var evt in cardEvents)
                {
                    if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                    if (!evt.IsYellow && !evt.IsSecondYellow && !evt.IsRed) continue;
                    var player = ResultIngestionJob.ResolvePlayer(playerName, playerByName, playersByLastName);
                    if (player is null)
                    {
                        playerMisses.Add($"{fixture.Id}/card: {playerName}");
                        continue;
                    }
                    var cardType = evt.IsSecondYellow ? CardType.SecondYellow : evt.IsRed ? CardType.Red : CardType.Yellow;
                    var cardId = ResultIngestionJob.CreateDeterministicGuid(ns,
                        $"card:{fixture.FootballApiFixtureId}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                    session.Store(new CardEvent
                    {
                        Id          = cardId,
                        FixtureId   = fixture.Id,
                        PlayerId    = player.Id,
                        Type        = cardType,
                        Minute      = evt.Time?.Elapsed ?? 0,
                        ExtraMinute = evt.Time?.Extra,
                    });
                    totalCards++;
                }

                foreach (var evt in subEvents)
                {
                    var playerOutName = evt.Player?.Name ?? string.Empty;
                    var playerInName  = evt.Assist?.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(playerOutName) && string.IsNullOrWhiteSpace(playerInName)) continue;
                    var playerOut = ResultIngestionJob.ResolvePlayer(playerOutName, playerByName, playersByLastName);
                    var playerIn  = ResultIngestionJob.ResolvePlayer(playerInName,  playerByName, playersByLastName);
                    var teamFifaCode = evt.Team?.Name is { } tn
                        ? FootballApiTeamMap.Resolve(evt.Team.Id, tn) ?? string.Empty
                        : string.Empty;
                    var subId = ResultIngestionJob.CreateDeterministicGuid(ns,
                        $"sub:{fixture.FootballApiFixtureId}:{playerOutName}:{playerInName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                    session.Store(new SubstitutionEvent
                    {
                        Id            = subId,
                        FixtureId     = fixture.Id,
                        PlayerOutId   = playerOut?.Id,
                        PlayerInId    = playerIn?.Id,
                        PlayerOutName = playerOutName,
                        PlayerInName  = playerInName,
                        TeamId        = teamFifaCode,
                        Minute        = evt.Time?.Elapsed ?? 0,
                        ExtraMinute   = evt.Time?.Extra,
                    });
                    totalSubs++;
                }

                foreach (var evt in varEvents)
                {
                    if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                    VarDecisionType? decType =
                        evt.IsGoalCancelled     ? VarDecisionType.GoalCancelled :
                        evt.IsCardUpgradeRed    ? VarDecisionType.CardUpgradeRed :
                        evt.IsCardUpgrade2ndYel ? VarDecisionType.CardUpgradeSecondYellow :
                        null;
                    if (decType is null) continue;
                    var varTeam = evt.Team?.Name is { } vtn
                        ? FootballApiTeamMap.Resolve(evt.Team.Id, vtn) ?? string.Empty
                        : string.Empty;
                    var varId = ResultIngestionJob.CreateDeterministicGuid(ns,
                        $"var:{fixture.FootballApiFixtureId}:{decType}:{playerName}:{ResultIngestionJob.MinuteKey(evt.Time)}");
                    session.Store(new VarEvent
                    {
                        Id          = varId,
                        FixtureId   = fixture.Id,
                        Type        = decType.Value,
                        PlayerName  = playerName,
                        TeamId      = varTeam,
                        Minute      = evt.Time?.Elapsed ?? 0,
                        ExtraMinute = evt.Time?.Extra,
                    });
                }

                fixture.EventsIngested = true;
                session.Store(fixture);
                processed++;
            }

            if (processed > 0)
            {
                await session.SaveChangesAsync(ct);
                await scoringService.RecomputeAllAsync(ct);
            }

            return Results.Ok(new
            {
                processed,
                total          = toProcess.Count,
                totalGoals,
                totalCards,
                totalSubs,
                quotaExhausted,
                skipped,
                playerMisses,
            });
        })
        .WithName("FetchAllFixtureEvents")
        .WithSummary("Bulk-fetches events (goals, cards, subs) for all completed group-stage fixtures and upserts them. Pass onlyMissing=true to skip fixtures that already have events (saves API quota). Idempotent.");

        // ── POST /admin/knockout/{slotKey}/teams ─────────────────────────────
        // Manually sets the HomeTeamId and/or AwayTeamId on a knockout slot.
        // Use when the automatic bracket resolver assigns the wrong teams (e.g.
        // BestThirdPlace bipartite matching diverges from the FIFA allocation table).
        // Does NOT affect scores, winner, or status — those stay untouched.
        // Logs to the override history; triggers a full scoring recompute so
        // existing predictions are re-evaluated against the correct teams.
        group.MapPost("/knockout/{slotKey}/teams", async (
            string slotKey,
            HttpContext context,
            [FromBody] SetKnockoutTeamsRequest request,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(request.HomeTeamId) && string.IsNullOrWhiteSpace(request.AwayTeamId))
                return Results.BadRequest(new { error = "At least one of HomeTeamId or AwayTeamId must be provided." });

            var slot = await session.LoadAsync<KnockoutSlot>(slotKey, ct);
            if (slot is null)
                return Results.NotFound(new { error = $"Knockout slot '{slotKey}' not found." });

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.HomeTeamId))
            {
                slot.HomeTeamId = request.HomeTeamId.Trim().ToUpperInvariant();
                slot.HomeTeamOverridden = true;
                parts.Add($"home={slot.HomeTeamId}");
            }
            if (!string.IsNullOrWhiteSpace(request.AwayTeamId))
            {
                slot.AwayTeamId = request.AwayTeamId.Trim().ToUpperInvariant();
                slot.AwayTeamOverridden = true;
                parts.Add($"away={slot.AwayTeamId}");
            }
            session.Store(slot);

            // TWC-63: the participant change may invalidate existing predictions for this slot
            // (predicted a team that is no longer a participant). Delete them so scoring/UI treat
            // it as "no pick" rather than a wrong pick that breaks a streak. The slot isn't locked
            // by this change, so affected users can re-predict against the corrected matchup.
            var existingPredictions = await session.Query<KnockoutPrediction>()
                .Where(p => p.SlotKey == slotKey)
                .ToListAsync(ct);
            var stalePredictions = KnockoutPredictionInvalidator.FindStale(slot, existingPredictions);
            foreach (var stale in stalePredictions)
                session.Delete(stale);

            session.Store(new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "knockoutslot-teams",
                TargetId         = slotKey,
                Description      = $"Manual team override: {string.Join(", ", parts)}"
                                    + (stalePredictions.Count > 0 ? $"; cleared {stalePredictions.Count} stale prediction(s)" : string.Empty),
            });

            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                slot.SlotKey,
                slot.HomeTeamId,
                slot.AwayTeamId,
                slot.HomeScore,
                slot.AwayScore,
                slot.Status,
                clearedPredictions = stalePredictions.Count,
            });
        })
        .WithName("SetKnockoutSlotTeams")
        .WithSummary("Manually overrides HomeTeamId and/or AwayTeamId on a knockout slot. Use when the bracket resolver assigns wrong teams. Does not affect scores or winner.");

        // ── POST /admin/recompute ─────────────────────────────────────────────
        group.MapPost("/recompute", async (
            HttpContext context,
            ScoringRecomputeService scoringService,
            KnockoutBracketResolver bracketResolver,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            // Re-resolve group stage (if all 72 fixtures are done) and propagate all knockout results.
            await bracketResolver.ResolveGroupStageAsync(ct);
            await bracketResolver.PropagateAllKnockoutResultsAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new { message = "Recompute triggered" });
        })
        .WithName("ForceRecompute")
        .WithSummary("Forces bracket re-resolution and full scoring recompute for all members.");

        // ── GET /admin/scoring/verify ─────────────────────────────────────────
        // Read-only. Independently re-derives every member's points from the raw
        // predictions and results, then diffs against the stored MemberScore documents.
        // Never writes — a discrepancy is fixed with POST /admin/recompute.
        group.MapGet("/scoring/verify", async (
            HttpContext context,
            ScoreVerifier verifier,
            [FromQuery] string? userId,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var report = await verifier.VerifyAsync(userId, ct);
            return Results.Ok(report);
        })
        .WithName("VerifyScores")
        .WithSummary("Re-derives all points independently and reports any mismatch with the stored scores.");

        return routes;
    }

    /// <summary>
    /// Simple deterministic Guid based on a string key using MD5 (sufficient for admin override
    /// deduplication — not security-sensitive, just a stable upsert key).
    /// </summary>
    private static Guid DeterministicGuid(string key)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }

    /// <summary>
    /// Returns true if the given id refers to either a group-stage <see cref="Fixture"/> or a
    /// <see cref="KnockoutSlot"/>. Manual event overrides (goals/cards/subs) store the id verbatim
    /// in the event's FixtureId, so both kinds of match can be targeted.
    /// </summary>
    private static async Task<bool> MatchExistsAsync(IDocumentSession session, string id, CancellationToken ct)
    {
        if (await session.LoadAsync<Fixture>(id, ct) is not null)
            return true;
        return await session.LoadAsync<KnockoutSlot>(id, ct) is not null;
    }
}

/// <summary>Request body for POST /admin/users.</summary>
public sealed record CreateUserRequest(string DisplayName);

/// <summary>Request body for POST /admin/fixtures/{id}/result.</summary>
public sealed record SetResultRequest(int HomeScore, int AwayScore, bool MarkAsLive = false, int? ElapsedMinute = null, int? ElapsedExtra = null);

/// <summary>One item in the POST /admin/users/{userId}/predictions/inject body.</summary>
public sealed record InjectPredictionItem(string FixtureId, int Home, int Away);

/// <summary>Request body for POST /admin/fixtures/{id}/goals.</summary>
public sealed record AddGoalRequest(Guid PlayerId, string Type, int Minute);

/// <summary>Request body for POST /admin/fixtures/{id}/cards.</summary>
public sealed record AddCardRequest(Guid PlayerId, string Type, int Minute);

/// <summary>Request body for POST /admin/fixtures/{id}/substitutions.</summary>
public sealed record AddSubstitutionRequest(string PlayerInName, string PlayerOutName, string? TeamId, int Minute);

/// <summary>Request body for POST /admin/knockout/{slotKey}/result.</summary>
public sealed record SetKnockoutResultRequest(int HomeScore, int AwayScore, string WinnerTeamId);

/// <summary>Request body for POST /admin/push/test.</summary>
public sealed record TestPushRequest(string? UserId, string? Title, string? Body);

/// <summary>Request body for POST /admin/fixtures/sync-api-ids. Each id is a group-stage
/// Fixture id ("1"–"72") or a knockout SlotKey ("R32-1", "SF-1", …).</summary>
public sealed record SyncApiFixtureIdsRequest(IReadOnlyList<string>? Ids);

/// <summary>Request body for POST /admin/knockout/{slotKey}/teams.</summary>
public sealed record SetKnockoutTeamsRequest(string? HomeTeamId, string? AwayTeamId);

/// <summary>
/// Body for POST /admin/ingestion/budget. <see cref="Enabled"/> switches free-plan budget mode
/// on/off; the two nullable overrides tune the cap and pacing when supplied (omit to keep current).
/// </summary>
public sealed record IngestionBudgetRequest(bool Enabled, int? MaxCallsPerDay = null, int? MinPollIntervalSeconds = null);
