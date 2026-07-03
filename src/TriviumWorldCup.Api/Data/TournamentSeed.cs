using Marten;
using TriviumWorldCup.Api.Data.SeedData;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Data;

/// <summary>
/// Idempotent seed for the tournament data.
/// Called from Program.cs at application startup.
///
/// Idempotency check: if any Team documents exist in the store, the seed is
/// considered already run and exits immediately. Run twice → no duplicates.
///
/// Knockout slot migration runs unconditionally on every startup: it updates
/// the structural fields (slot wiring, kickoff, venue/city) from KnockoutSlotsData
/// without disturbing runtime state (team assignments, scores, status, WinnerTeamId).
/// This allows bracket corrections to be deployed without a full re-seed.
///
/// All documents use string-based Ids equal to their natural keys:
///   Team      → FifaCode   (e.g. "BRA")
///   Group     → Letter     (e.g. "A")
///   Fixture   → MatchNumber as string (e.g. "1" … "72")
///   KnockoutSlot → SlotKey (e.g. "R32-1", "FIN")
///   Player    → Guid (new Guid per player — not a natural key)
/// Marten upserts by Id so re-running after a partial seed is also safe.
/// </summary>
public static class TournamentSeed
{
    public static async Task SeedAsync(IDocumentStore store, CancellationToken cancellationToken = default)
    {
        await using var session = store.LightweightSession();

        // Idempotency guard: if teams already exist, skip the initial seed.
        var anyTeam = await session.Query<Team>().AnyAsync(cancellationToken);
        if (!anyTeam)
        {
            foreach (var team in TeamsData.All)
                session.Store(team);

            foreach (var group in GroupsData.All)
                session.Store(group);

            foreach (var fixture in FixturesData.All)
                session.Store(fixture);

            foreach (var player in PlayersData.All)
                session.Store(player);

            foreach (var invite in InviteUsersData.All)
                session.Store(invite);

            foreach (var profile in UserProfilesData.All)
                session.Store(profile);

            // On first seed, store all slots as-is (no existing documents to merge with).
            foreach (var slot in KnockoutSlotsData.All)
                session.Store(slot);

            await session.SaveChangesAsync(cancellationToken);
            return;
        }

        // Always migrate knockout slot structural data so bracket corrections in code
        // are reflected in the DB without requiring a full re-seed.
        await MigrateKnockoutSlotsAsync(session, cancellationToken);
    }

    /// <summary>
    /// Updates knockout slot structural fields (wiring, kickoff, venue/city) from
    /// KnockoutSlotsData without touching runtime state (team IDs, scores, status).
    /// Clears HomeTeamId/AwayTeamId only on slots whose wiring changed — the bracket
    /// resolver will repopulate them on the next recompute.
    /// </summary>
    private static async Task MigrateKnockoutSlotsAsync(
        Marten.IDocumentSession session,
        CancellationToken ct)
    {
        var existing = await session.Query<KnockoutSlot>().ToListAsync(ct);
        var byKey = existing.ToDictionary(s => s.SlotKey);

        var changed = 0;
        var slotsWithWiringChange = new List<KnockoutSlot>();
        foreach (var template in KnockoutSlotsData.All)
        {
            if (!byKey.TryGetValue(template.SlotKey, out var slot))
            {
                // New slot (shouldn't happen after first seed, but handle gracefully).
                session.Store(template);
                changed++;
                continue;
            }

            var result = ApplySlotMigration(slot, template);
            if (!result.Changed) continue;

            if (result.WiringChanged)
                slotsWithWiringChange.Add(slot);

            session.Store(slot);
            changed++;
        }

        // TWC-63: a wiring change means HomeTeamId/AwayTeamId were cleared, so any existing
        // predictions for that slot now reference a team that isn't a participant. Clear them
        // so scoring/UI treat it as "no pick" — see KnockoutPredictionInvalidator for the
        // delete-vs-flag rationale.
        if (slotsWithWiringChange.Count > 0)
        {
            var slotKeys = slotsWithWiringChange.Select(s => s.SlotKey).ToList();
            var affectedPredictions = await session.Query<KnockoutPrediction>()
                .Where(p => p.SlotKey.IsOneOf(slotKeys))
                .ToListAsync(ct);

            var predictionsBySlot = affectedPredictions.GroupBy(p => p.SlotKey);
            foreach (var group in predictionsBySlot)
            {
                var slot = slotsWithWiringChange.First(s => s.SlotKey == group.Key);
                foreach (var stale in KnockoutPredictionInvalidator.FindStale(slot, group))
                    session.Delete(stale);
            }
        }

        if (changed > 0)
            await session.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Applies structural migration from <paramref name="template"/> onto <paramref name="slot"/>
    /// in place. Returns whether any field changed (caller should persist) and, separately,
    /// whether the wiring itself changed (caller should also invalidate stale predictions —
    /// see TWC-63). Pure — no DB dependency — so this is fully unit-testable.
    /// </summary>
    internal static SlotMigrationResult ApplySlotMigration(KnockoutSlot slot, KnockoutSlot template)
    {
        var wiringChanged =
            slot.HomeSlotSource.Type      != template.HomeSlotSource.Type      ||
            slot.HomeSlotSource.Reference != template.HomeSlotSource.Reference ||
            slot.AwaySlotSource.Type      != template.AwaySlotSource.Type      ||
            slot.AwaySlotSource.Reference != template.AwaySlotSource.Reference;

        var metaChanged =
            slot.KickoffUtc != template.KickoffUtc ||
            slot.Venue      != template.Venue      ||
            slot.City       != template.City       ||
            slot.Round      != template.Round      ||
            slot.SlotNumber != template.SlotNumber;

        if (!wiringChanged && !metaChanged) return new SlotMigrationResult(false, false);

        slot.HomeSlotSource = template.HomeSlotSource;
        slot.AwaySlotSource = template.AwaySlotSource;
        slot.KickoffUtc     = template.KickoffUtc;
        slot.Venue          = template.Venue;
        slot.City           = template.City;
        slot.Round          = template.Round;
        slot.SlotNumber     = template.SlotNumber;

        if (wiringChanged)
        {
            // Wiring changed → team assignments derived from old wiring are stale.
            // Clear them so the resolver repopulates from the correct source. Also clear
            // any manual admin override flags — a wiring change invalidates a prior
            // override (it applied under the old participant source, not whatever the
            // corrected wiring now feeds in), and KnockoutBracketResolver skips writes on
            // slots where *Overridden is set, so leaving it set would leave the slot
            // permanently teamless after this migration.
            slot.HomeTeamId = null;
            slot.AwayTeamId = null;
            slot.HomeTeamOverridden = false;
            slot.AwayTeamOverridden = false;
        }

        return new SlotMigrationResult(true, wiringChanged);
    }
}

/// <summary>Result of <see cref="TournamentSeed.ApplySlotMigration"/>.</summary>
internal readonly record struct SlotMigrationResult(bool Changed, bool WiringChanged);
