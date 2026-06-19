using Marten;
using TriviumWorldCup.Api.Domain;
using TournamentAggregate = TriviumWorldCup.Api.Domain.Tournament;

namespace TriviumWorldCup.Api.Data;

/// <summary>
/// GEN-1 (TWC-35): One-time migration that:
///   (a) Inserts the world-cup-2026 Tournament document if it does not already exist.
///   (b) Bulk-updates TournamentId = 'world-cup-2026' in the JSONB data column on every
///       tournament-scoped twc.mt_doc_* table where TournamentId is currently NULL.
///   (c) Widens the composite document ID for GroupPrediction and KnockoutPrediction from
///       "{UserId}_{Key}" to "world-cup-2026_{UserId}_{Key}" on existing rows — so that
///       key format is consistent with new writes after the migration.
///
/// Safe to run on every startup — all SQL is idempotent (uses WHERE TournamentId IS NULL).
///
/// DO NOT execute against the live Azure staging database manually — this migration runs
/// automatically at application startup via Program.cs.
/// </summary>
public static class TournamentIdMigration
{
    private const string DefaultTournamentId = SingleTournamentContext.DefaultTournamentId;

    /// <summary>
    /// Applies the GEN-1 migration. Idempotent: safe to run multiple times.
    /// </summary>
    public static async Task RunAsync(IDocumentStore store, CancellationToken cancellationToken = default)
    {
        await using var session = store.LightweightSession();

        // ── (a) Ensure the Tournament document exists ─────────────────────────
        var existing = await session.LoadAsync<TournamentAggregate>(DefaultTournamentId, cancellationToken);
        if (existing is null)
        {
            session.Store(TournamentSeed.WorldCup2026);
            await session.SaveChangesAsync(cancellationToken);
        }

        // ── (b) & (c): Raw SQL backfill ───────────────────────────────────────
        // Use the Marten session's underlying Npgsql connection for efficient
        // bulk updates without loading all documents into application memory.
        var conn = session.Connection;
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        // Tables where we only need to add TournamentId to the JSON (natural key IDs don't change).
        var naturalKeyTables = new[]
        {
            "mt_doc_team",
            "mt_doc_group",
            "mt_doc_fixture",
            "mt_doc_knockoutslot",
            "mt_doc_player",
            "mt_doc_tournamentprediction",
            "mt_doc_memberscore",
            "mt_doc_goalevent",
            "mt_doc_cardevent",
            "mt_doc_substitutionevent",
            "mt_doc_varevent",
        };

        foreach (var table in naturalKeyTables)
        {
            await ExecuteNonQueryAsync(conn, $$"""
                UPDATE twc.{{table}}
                SET data = jsonb_set(data, '{TournamentId}', '"{{DefaultTournamentId}}"', true)
                WHERE data->>'TournamentId' IS NULL
                   OR data->>'TournamentId' = ''
                """, cancellationToken);
        }

        // Tables whose composite IDs must also be widened.
        // GroupPrediction: old ID = "{UserId}_{FixtureId}" → new ID = "world-cup-2026_{UserId}_{FixtureId}"
        await WidenCompositeIds(conn, "mt_doc_groupprediction", cancellationToken);

        // KnockoutPrediction: old ID = "{UserId}_{SlotKey}" → new ID = "world-cup-2026_{UserId}_{SlotKey}"
        await WidenCompositeIds(conn, "mt_doc_knockoutprediction", cancellationToken);
    }

    /// <summary>
    /// Widens the document ID from "{key}" to "world-cup-2026_{key}" AND sets TournamentId
    /// in the JSON, for rows that have not yet been migrated (TournamentId IS NULL).
    /// </summary>
    private static async Task WidenCompositeIds(
        System.Data.Common.DbConnection conn,
        string table,
        CancellationToken cancellationToken)
    {
        // Step 1: Add TournamentId to JSON.
        await ExecuteNonQueryAsync(conn, $$"""
            UPDATE twc.{{table}}
            SET data = jsonb_set(data, '{TournamentId}', '"{{DefaultTournamentId}}"', true)
            WHERE data->>'TournamentId' IS NULL
               OR data->>'TournamentId' = ''
            """, cancellationToken);

        // Step 2: Widen the `id` column from "{key}" to "world-cup-2026_{key}".
        // Only rows whose id does NOT already start with "world-cup-2026_" are updated.
        // We also update the Id field inside the JSONB data for consistency.
        await ExecuteNonQueryAsync(conn, $$"""
            UPDATE twc.{{table}}
            SET
                id   = '{{DefaultTournamentId}}_' || id,
                data = jsonb_set(data, '{Id}', to_jsonb('{{DefaultTournamentId}}_' || (data->>'Id')), true)
            WHERE id NOT LIKE '{{DefaultTournamentId}}_%'
            """, cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        System.Data.Common.DbConnection conn,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
