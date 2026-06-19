using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Link;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Ingestion;
using TriviumWorldCup.Api.Knockout;
using TriviumWorldCup.Api.Predictions;
using TriviumWorldCup.Api.Scoring;
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
            IDocumentStore store,
            ITournamentContext tournament,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var now = DateTimeOffset.UtcNow;
            var pendingCount = await session
                .Query<Fixture>()
                .Where(f => f.TournamentId == tournament.TournamentId
                         && f.Status != MatchStatus.Completed
                         && f.KickoffUtc < now)
                .CountAsync(ct);

            return Results.Ok(new
            {
                lastSuccessfulPoll  = statusStore.LastSuccessfulPoll,
                lastAttemptedPoll   = statusStore.LastAttemptedPoll,
                lastError           = statusStore.LastError,
                totalPollCount      = statusStore.TotalPollCount,
                errorCount          = statusStore.ErrorCount,
                pendingFixtureCount = pendingCount,
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

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

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
        .WithSummary("Adds or replaces a goal event for a fixture and triggers recompute.");

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

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

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
        .WithSummary("Adds or replaces a card event for a fixture.");

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

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

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
        .WithSummary("Manually adds a substitution event to a fixture.");

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
                        fixture.HomeScore = null;
                        fixture.AwayScore = null;
                        fixture.Status    = MatchStatus.Scheduled;
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
            ITournamentContext tournament,
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
                .Where(f => f.TournamentId == tournament.TournamentId)
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
                    Id           = GroupPredictionEndpoints.BuildId(tournament.TournamentId, userId, item.FixtureId),
                    TournamentId = tournament.TournamentId,
                    UserId       = userId,
                    FixtureId    = item.FixtureId,
                    HomeScore    = item.Home,
                    AwayScore    = item.Away,
                    SubmittedAt  = now,
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
        // Fetches all WC 2026 fixtures from the Football API in one call, matches each to a
        // Marten Fixture by team pair, and writes the FootballApiFixtureId field.
        // Also returns the equivalent SQL UPDATE statements so you can run them directly
        // on the Azure PostgreSQL instance if preferred.
        // Safe to call multiple times — already-populated rows are overwritten with the same value.
        group.MapPost("/fixtures/sync-api-ids", async (
            HttpContext context,
            IFootballApiClient apiClient,
            IDocumentStore store,
            ITournamentContext tournament,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            IReadOnlyList<ApiFixture> apiFixtures;
            try
            {
                apiFixtures = await apiClient.GetAllFixturesForSeasonAsync(ct);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Football API call failed: {ex.Message}", statusCode: 502);
            }

            await using var session = store.LightweightSession();
            var allFixtures = await session.Query<Fixture>()
                .Where(f => f.TournamentId == tournament.TournamentId)
                .ToListAsync(ct);
            var fixtureByTeamPair = allFixtures.ToDictionary(f => (f.HomeTeamId, f.AwayTeamId));

            var matched = new List<object>();
            var unresolved = new List<string>();

            foreach (var api in apiFixtures)
            {
                var homeCode = FootballApiTeamMap.Resolve(api.HomeTeamId, api.HomeTeamName);
                var awayCode = FootballApiTeamMap.Resolve(api.AwayTeamId, api.AwayTeamName);

                if (homeCode == null || awayCode == null)
                {
                    unresolved.Add($"api_id={api.FixtureId} {api.HomeTeamName} vs {api.AwayTeamName} — FIFA code unresolved");
                    continue;
                }

                if (!fixtureByTeamPair.TryGetValue((homeCode, awayCode), out var fixture))
                    // Knockout fixtures (R32–Final) have no Fixture document — skip silently.
                    continue;

                fixture.FootballApiFixtureId = api.FixtureId;
                session.Store(fixture);

                matched.Add(new { fixture.Id, homeCode, awayCode, apiFixtureId = api.FixtureId });
            }

            if (matched.Count > 0)
                await session.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                matched    = matched.Count,
                fixtures   = matched,
                unresolved,
            });
        })
        .WithName("SyncApiFixtureIds")
        .WithSummary("Backfills FootballApiFixtureId on all group-stage Fixture documents from the Football API. Returns equivalent SQL for direct Azure execution.");

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
            ITournamentContext tournament,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            if (fixture.FootballApiFixtureId is null)
                return Results.UnprocessableEntity(new
                {
                    error = "Fixture has no FootballApiFixtureId — call POST /admin/fixtures/sync-api-ids first."
                });

            IReadOnlyList<ApiMatchEvent> allEvents;
            try
            {
                allEvents = await apiClient.GetAllEventsAsync(fixture.FootballApiFixtureId.Value, ct);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Football API call failed: {ex.Message}", statusCode: 502);
            }

            var allPlayers = await session.Query<Player>()
                .Where(p => p.TournamentId == tournament.TournamentId)
                .ToListAsync(ct);
            var playerByName = allPlayers
                .GroupBy(p => ResultIngestionJob.StripDiacritics(p.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var playersByLastName = allPlayers
                .ToLookup(p => ResultIngestionJob.LastWord(p.Name), StringComparer.OrdinalIgnoreCase);

            var goalEvents = allEvents.Where(e => e.IsGoal).ToList();
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
                var goalType = evt.IsOwnGoal ? GoalType.OwnGoal : evt.IsPenalty ? GoalType.PenaltyInMatch : GoalType.OpenPlay;
                var goalId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"{fixture.FootballApiFixtureId}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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
                    $"card:{fixture.FootballApiFixtureId}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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
                    $"sub:{fixture.FootballApiFixtureId}:{playerOutName}:{playerInName}:{evt.Time?.Elapsed ?? 0}");
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
                    $"var:{fixture.FootballApiFixtureId}:{decType}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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

            fixture.EventsIngested = true;
            session.Store(fixture);
            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                fixtureId,
                footballApiFixtureId = fixture.FootballApiFixtureId,
                totalEvents          = allEvents.Count,
                goalsStored,
                cardsStored,
                subsStored           = subEvents.Count,
                playerMisses,
            });
        })
        .WithName("FetchFixtureEvents")
        .WithSummary("Fetches events from the Football API for any completed fixture and writes them. Backfills matches missed by the date-window ingestion job. Idempotent.");

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
            ITournamentContext tournament,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            if (fixture.FootballApiFixtureId is null)
                return Results.UnprocessableEntity(new
                {
                    error = "Fixture has no FootballApiFixtureId — call POST /admin/fixtures/sync-api-ids first."
                });

            var existingGoals = await session.Query<GoalEvent>()
                .Where(g => g.TournamentId == tournament.TournamentId && g.FixtureId == fixtureId).ToListAsync(ct);
            var existingCards = await session.Query<CardEvent>()
                .Where(c => c.TournamentId == tournament.TournamentId && c.FixtureId == fixtureId).ToListAsync(ct);
            var existingSubs = await session.Query<SubstitutionEvent>()
                .Where(s => s.TournamentId == tournament.TournamentId && s.FixtureId == fixtureId).ToListAsync(ct);
            var existingVars = await session.Query<VarEvent>()
                .Where(v => v.TournamentId == tournament.TournamentId && v.FixtureId == fixtureId).ToListAsync(ct);

            foreach (var g in existingGoals) session.Delete(g);
            foreach (var c in existingCards) session.Delete(c);
            foreach (var s in existingSubs) session.Delete(s);
            foreach (var v in existingVars) session.Delete(v);

            fixture.EventsIngested = false;
            session.Store(fixture);
            await session.SaveChangesAsync(ct);

            IReadOnlyList<ApiMatchEvent> allEvents;
            try
            {
                allEvents = await apiClient.GetAllEventsAsync(fixture.FootballApiFixtureId.Value, ct);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    $"Events cleared but Football API call failed: {ex.Message}. The ingestion job will backfill on the next cycle.",
                    statusCode: 502);
            }

            var allPlayers = await session.Query<Player>()
                .Where(p => p.TournamentId == tournament.TournamentId)
                .ToListAsync(ct);
            var playerByName = allPlayers
                .GroupBy(p => ResultIngestionJob.StripDiacritics(p.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var playersByLastName = allPlayers
                .ToLookup(p => ResultIngestionJob.LastWord(p.Name), StringComparer.OrdinalIgnoreCase);

            var goalEvents = allEvents.Where(e => e.IsGoal).ToList();
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
                var goalType = evt.IsOwnGoal ? GoalType.OwnGoal : evt.IsPenalty ? GoalType.PenaltyInMatch : GoalType.OpenPlay;
                var goalId = ResultIngestionJob.CreateDeterministicGuid(ns,
                    $"{fixture.FootballApiFixtureId}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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
                    $"card:{fixture.FootballApiFixtureId}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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
                    $"sub:{fixture.FootballApiFixtureId}:{playerOutName}:{playerInName}:{evt.Time?.Elapsed ?? 0}");
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
                    $"var:{fixture.FootballApiFixtureId}:{decType}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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

            fixture.EventsIngested = true;
            session.Store(fixture);
            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                fixtureId,
                footballApiFixtureId = fixture.FootballApiFixtureId,
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
            ITournamentContext tournament,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var allCompleted = await session
                .Query<Fixture>()
                .Where(f => f.TournamentId == tournament.TournamentId
                         && f.Status == MatchStatus.Completed
                         && f.FootballApiFixtureId != null)
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

            var allPlayers = await session.Query<Player>()
                .Where(p => p.TournamentId == tournament.TournamentId)
                .ToListAsync(ct);
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

                var goalEvents = allEvents.Where(e => e.IsGoal).ToList();
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
                    var goalType = evt.IsOwnGoal ? GoalType.OwnGoal : evt.IsPenalty ? GoalType.PenaltyInMatch : GoalType.OpenPlay;
                    var goalId   = ResultIngestionJob.CreateDeterministicGuid(ns,
                        $"{fixture.FootballApiFixtureId}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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
                        $"card:{fixture.FootballApiFixtureId}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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
                        $"sub:{fixture.FootballApiFixtureId}:{playerOutName}:{playerInName}:{evt.Time?.Elapsed ?? 0}");
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
                        $"var:{fixture.FootballApiFixtureId}:{decType}:{playerName}:{evt.Time?.Elapsed ?? 0}");
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
