import { useEffect, useRef, useState } from 'react';

// ── Types ─────────────────────────────────────────────────────────────────────

interface LiveFixtureDto {
  id: string;
  matchNumber: number;
  groupLetter: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  kickoffUtc: string;
  status: string;
  homeScore: number | null;
  awayScore: number | null;
  venue: string;
  city: string;
}

interface GoalEventDto {
  fixtureId: string;
  playerId: string;
  playerName: string;
  teamId: string;
  type: string;
  minute: number;
}

interface LiveFixturesResponse {
  fixtures: LiveFixtureDto[];
  goals: GoalEventDto[];
  liveWindowActive: boolean;
}

// ── API helper ────────────────────────────────────────────────────────────────

async function fetchLiveFixtures(): Promise<LiveFixturesResponse> {
  const res = await fetch('/fixtures/live', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load live fixtures (${res.status})`);
  return res.json() as Promise<LiveFixturesResponse>;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatKickoff(kickoffUtc: string): string {
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function StatusBadge({ status }: { status: string }) {
  if (status === 'InProgress') {
    return (
      <span className="inline-flex items-center gap-1 bg-red-600 text-white text-xs font-bold px-2 py-0.5 rounded">
        <span className="w-1.5 h-1.5 rounded-full bg-white animate-pulse inline-block" />
        LIVE
      </span>
    );
  }
  if (status === 'Completed') {
    return (
      <span className="bg-slate-600 text-slate-200 text-xs font-semibold px-2 py-0.5 rounded">
        FT
      </span>
    );
  }
  // Scheduled / Soon
  return (
    <span className="bg-amber-700/70 text-amber-200 text-xs font-semibold px-2 py-0.5 rounded">
      Soon
    </span>
  );
}

function ScoreDisplay({ fixture }: { fixture: LiveFixtureDto }) {
  const hasScore =
    fixture.homeScore !== null &&
    fixture.awayScore !== null &&
    (fixture.status === 'InProgress' || fixture.status === 'Completed');

  if (hasScore) {
    return (
      <span className="text-2xl font-bold text-white tabular-nums">
        {fixture.homeScore} – {fixture.awayScore}
      </span>
    );
  }
  return <span className="text-xl font-semibold text-slate-400">vs</span>;
}

// ── Fixture card ──────────────────────────────────────────────────────────────

interface FixtureCardProps {
  fixture: LiveFixtureDto;
  goals: GoalEventDto[];
}

function LiveFixtureCard({ fixture, goals }: FixtureCardProps) {
  const fixtureGoals = goals.filter(g => g.fixtureId === fixture.id);
  const isLive = fixture.status === 'InProgress';

  return (
    <div
      className={`rounded-xl border p-4 flex flex-col gap-3 ${
        isLive
          ? 'bg-slate-800 border-red-500/60'
          : fixture.status === 'Completed'
          ? 'bg-slate-800/70 border-slate-700'
          : 'bg-slate-800 border-amber-500/40'
      }`}
    >
      {/* Header */}
      <div className="flex items-center justify-between text-xs text-slate-400">
        <span>
          Match {fixture.matchNumber} &middot; Group {fixture.groupLetter} &middot; {fixture.venue}, {fixture.city}
        </span>
        <StatusBadge status={fixture.status} />
      </div>

      {/* Kickoff time */}
      <div className="text-xs text-slate-400">{formatKickoff(fixture.kickoffUtc)}</div>

      {/* Teams and score */}
      <div className="flex items-center gap-3">
        <span className="flex-1 text-right text-white font-semibold truncate">
          {fixture.homeTeamName}
        </span>
        <ScoreDisplay fixture={fixture} />
        <span className="flex-1 text-left text-white font-semibold truncate">
          {fixture.awayTeamName}
        </span>
      </div>

      {/* Goal events */}
      {fixtureGoals.length > 0 && (
        <ul className="flex flex-col gap-1 pt-1 border-t border-slate-700">
          {fixtureGoals.map((g, idx) => (
            <li key={idx} className="text-xs text-slate-300 flex items-center gap-2">
              <span className="text-slate-500 tabular-nums w-8 text-right">{g.minute}&apos;</span>
              <span>{g.playerName}</span>
              <span className="text-slate-500">({g.teamId})</span>
              {g.type === 'OwnGoal' && (
                <span className="text-red-400 text-xs">(og)</span>
              )}
              {g.type === 'PenaltyInMatch' && (
                <span className="text-yellow-400 text-xs">(pen)</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

const POLL_INTERVAL_MS = 20_000;

/**
 * Live scores page — shows in-progress and recently completed fixtures with
 * goal events, polling every 20 seconds while the live window is active.
 * Standings/leaderboard points do not change mid-match; they update at full-time.
 * TWC-17
 */
export function LiveScoresPage() {
  const [data, setData] = useState<LiveFixturesResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  function stopPolling() {
    if (intervalRef.current !== null) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }

  async function load() {
    try {
      const response = await fetchLiveFixtures();
      setData(response);
      setLoadError(null);

      // Stop polling if the live window is no longer active.
      if (!response.liveWindowActive) {
        stopPolling();
      }
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Failed to load live scores.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    // Initial fetch
    void load();

    // Start polling; the interval persists until liveWindowActive goes false or unmount.
    intervalRef.current = setInterval(() => {
      void load();
    }, POLL_INTERVAL_MS);

    return () => {
      stopPolling();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20 text-slate-400">
        Loading live scores…
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="flex items-center justify-center py-20 text-red-400">
        {loadError}
      </div>
    );
  }

  const noMatches = !data || data.fixtures.length === 0;
  const liveActive = data?.liveWindowActive === true;

  return (
    <div className="max-w-3xl mx-auto px-4 py-6">
      <div className="flex items-center justify-between mb-1">
        <h1 className="text-2xl font-bold text-white">Live Scores</h1>
        {liveActive && (
          <span className="flex items-center gap-1.5 text-xs text-red-400 font-medium">
            <span className="w-2 h-2 rounded-full bg-red-500 animate-pulse inline-block" />
            Live updates every 20s
          </span>
        )}
      </div>

      <p className="text-slate-500 text-xs mb-6">
        Standings update after each full-time result.
      </p>

      {noMatches && !liveActive ? (
        <p className="text-slate-400 text-center py-16">
          No matches currently live. Live scores will appear here when a match is in progress.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          {data!.fixtures.map(fixture => (
            <LiveFixtureCard
              key={fixture.id}
              fixture={fixture}
              goals={data!.goals}
            />
          ))}
        </div>
      )}
    </div>
  );
}
