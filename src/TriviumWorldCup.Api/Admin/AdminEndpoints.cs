using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Link;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Knockout;
using TriviumWorldCup.Api.Scoring;

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
                lastSuccessfulPoll = statusStore.LastSuccessfulPoll,
                lastAttemptedPoll  = statusStore.LastAttemptedPoll,
                lastError          = statusStore.LastError,
                totalPollCount     = statusStore.TotalPollCount,
                errorCount         = statusStore.ErrorCount,
                pendingFixtureCount = pendingCount,
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

            fixture.HomeScore = request.HomeScore;
            fixture.AwayScore = request.AwayScore;
            fixture.Status    = MatchStatus.Completed;
            session.Store(fixture);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "fixture",
                TargetId         = fixtureId,
                Description      = $"Set result {request.HomeScore}-{request.AwayScore}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);

            // Attempt to resolve R32 bracket (exits early if group stage not yet complete).
            await bracketResolver.ResolveGroupStageAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

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
public sealed record SetResultRequest(int HomeScore, int AwayScore);

/// <summary>One item in the POST /admin/users/{userId}/predictions/inject body.</summary>
public sealed record InjectPredictionItem(string FixtureId, int Home, int Away);

/// <summary>Request body for POST /admin/fixtures/{id}/goals.</summary>
public sealed record AddGoalRequest(Guid PlayerId, string Type, int Minute);

/// <summary>Request body for POST /admin/knockout/{slotKey}/result.</summary>
public sealed record SetKnockoutResultRequest(int HomeScore, int AwayScore, string WinnerTeamId);
