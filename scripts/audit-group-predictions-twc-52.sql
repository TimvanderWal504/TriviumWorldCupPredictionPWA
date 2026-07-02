-- TWC-52 audit: GroupPrediction documents submitted after their fixture's kickoff.
--
-- SCHEMA GAP (documented, not fixed here — see TWC-52 report):
-- GroupPrediction has no field that distinguishes an admin-injected prediction
-- (POST /admin/users/{userId}/predictions/inject) from a genuine user submission
-- (POST/PUT /predictions/group/{fixtureId}, or — before this fix — the removed
-- self-service POST /predictions/group/inject). Both code paths write an
-- identical document shape: {Id, UserId, FixtureId, HomeScore, AwayScore, SubmittedAt}.
-- There is no AdminUserId/AdminDisplayName/Source marker analogous to ResultOverride.
--
-- Consequently this query cannot tell "legitimate admin backfill of a completed
-- fixture" apart from "exploited self-service write after kickoff" — both satisfy
-- SubmittedAt > Fixture.KickoffUtc, because the admin endpoint intentionally bypasses
-- the lock too. It surfaces every candidate for manual review; a human (or the git/
-- deploy history correlated with SubmittedAt timestamps around 7 June 2026, when the
-- admin endpoint shipped) must judge each row.
--
-- Run against the `twc` Marten schema (adjust schema name if deployed differently).

SELECT
    gp.data ->> 'UserId'                                   AS user_id,
    gp.data ->> 'FixtureId'                                AS fixture_id,
    (gp.data ->> 'HomeScore')::int                         AS home_score,
    (gp.data ->> 'AwayScore')::int                         AS away_score,
    (gp.data ->> 'SubmittedAt')::timestamptz                AS submitted_at,
    (f.data ->> 'KickoffUtc')::timestamptz                  AS kickoff_utc,
    (gp.data ->> 'SubmittedAt')::timestamptz
        - (f.data ->> 'KickoffUtc')::timestamptz            AS submitted_after_kickoff_by
FROM twc.mt_doc_groupprediction gp
JOIN twc.mt_doc_fixture f
    ON f.id = gp.data ->> 'FixtureId'
WHERE (gp.data ->> 'SubmittedAt')::timestamptz > (f.data ->> 'KickoffUtc')::timestamptz
ORDER BY submitted_after_kickoff_by DESC;
