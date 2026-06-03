import { useEffect, useState } from 'react';
import { useAuth } from '../auth/useAuth.ts';

// ── API types ────────────────────────────────────────────────────────────────

interface LeaderboardEntry {
  rank: number;
  userId: string;
  displayName: string;
  totalPoints: number;
  groupMatchPoints: number;
  championPoints: number;
  goldenSixPoints: number;
}

interface GroupPredictionDetail {
  fixtureId: string;
  homeTeamId: string;
  awayTeamId: string;
  predictedHome: number;
  predictedAway: number;
  actualHome: number | null;
  actualAway: number | null;
  kickoffUtc: string;
  locked: boolean;
}

interface GoldenSixDetail {
  playerId: string;
  name: string;
  teamId: string;
  position: string;
  goals: number;
  points: number;
}

interface MemberDrillDown {
  userId: string;
  displayName: string;
  totalPoints: number;
  groupPredictions: GroupPredictionDetail[];
  goldenSix: GoldenSixDetail[];
  championTeamId: string | null;
  championTeamName: string | null;
}

// ── API fetch helpers ────────────────────────────────────────────────────────

async function fetchLeaderboard(): Promise<LeaderboardEntry[]> {
  const res = await fetch('/leaderboard', { credentials: 'include' });
  if (!res.ok) throw new Error(`Leaderboard fetch failed: ${res.status}`);
  return res.json() as Promise<LeaderboardEntry[]>;
}

async function fetchDrillDown(userId: string): Promise<MemberDrillDown> {
  const res = await fetch(`/leaderboard/${encodeURIComponent(userId)}`, {
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`Drill-down fetch failed: ${res.status}`);
  return res.json() as Promise<MemberDrillDown>;
}

// ── Sub-components ───────────────────────────────────────────────────────────

function RankBadge({ rank }: { rank: number }) {
  if (rank === 1) return <span className="font-bold text-yellow-400">1</span>;
  if (rank === 2) return <span className="font-bold text-slate-300">2</span>;
  if (rank === 3) return <span className="font-bold text-amber-600">3</span>;
  return <span className="text-slate-400">{rank}</span>;
}

interface DrillDownPanelProps {
  drillDown: MemberDrillDown;
  isOwnProfile: boolean;
  onClose: () => void;
}

function DrillDownPanel({ drillDown, isOwnProfile, onClose }: DrillDownPanelProps) {
  return (
    <div className="bg-slate-800 rounded-xl p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-bold text-white">{drillDown.displayName}</h2>
          <p className="text-slate-400 text-sm mt-0.5">
            {drillDown.totalPoints} total points
          </p>
        </div>
        <button
          onClick={onClose}
          className="text-slate-400 hover:text-white transition-colors text-sm"
        >
          Back to leaderboard
        </button>
      </div>

      {/* Champion pick */}
      {drillDown.championTeamId !== null && (
        <section>
          <h3 className="text-sm font-semibold text-blue-400 uppercase tracking-wide mb-2">
            Champion pick
          </h3>
          <p className="text-slate-300 text-sm">
            <span className="font-medium text-white">
              {drillDown.championTeamName ?? drillDown.championTeamId}
            </span>
            {' '}({drillDown.championTeamId})
          </p>
        </section>
      )}

      {/* Golden Six */}
      {drillDown.goldenSix.length > 0 && (
        <section>
          <h3 className="text-sm font-semibold text-blue-400 uppercase tracking-wide mb-3">
            Golden Six picks
          </h3>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-slate-400 border-b border-slate-700">
                  <th className="pb-2 pr-4 font-medium">Player</th>
                  <th className="pb-2 pr-4 font-medium">Team</th>
                  <th className="pb-2 pr-4 font-medium">Pos</th>
                  <th className="pb-2 pr-4 font-medium text-right">Goals</th>
                  <th className="pb-2 font-medium text-right">Points</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700/50">
                {drillDown.goldenSix.map((p) => (
                  <tr key={p.playerId} className="text-slate-300">
                    <td className="py-2 pr-4 font-medium text-white">{p.name}</td>
                    <td className="py-2 pr-4">{p.teamId}</td>
                    <td className="py-2 pr-4">
                      <span className="bg-slate-700 rounded px-1.5 py-0.5 text-xs font-mono">
                        {p.position}
                      </span>
                    </td>
                    <td className="py-2 pr-4 text-right tabular-nums">{p.goals}</td>
                    <td className="py-2 text-right tabular-nums font-semibold text-white">
                      {p.points}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {/* Group predictions */}
      <section>
        <h3 className="text-sm font-semibold text-blue-400 uppercase tracking-wide mb-3">
          Match predictions{' '}
          {!isOwnProfile && (
            <span className="text-slate-500 font-normal normal-case">
              (locked matches only)
            </span>
          )}
        </h3>

        {drillDown.groupPredictions.length === 0 ? (
          <p className="text-slate-500 text-sm">
            {isOwnProfile
              ? 'No match predictions submitted yet.'
              : 'No locked match predictions to show.'}
          </p>
        ) : (
          <div className="space-y-2">
            {drillDown.groupPredictions.map((pred) => {
              const hasResult = pred.actualHome !== null && pred.actualAway !== null;
              return (
                <div
                  key={pred.fixtureId}
                  className="flex items-center justify-between bg-slate-700/50 rounded-lg px-4 py-3 text-sm"
                >
                  {/* Teams + prediction */}
                  <div className="flex items-center gap-2 text-slate-300 min-w-0">
                    <span className="font-mono font-semibold text-white text-xs">
                      {pred.homeTeamId}
                    </span>
                    <span className="text-slate-500 text-xs">vs</span>
                    <span className="font-mono font-semibold text-white text-xs">
                      {pred.awayTeamId}
                    </span>
                  </div>

                  <div className="flex items-center gap-4 shrink-0 ml-4">
                    {/* Prediction */}
                    <div className="text-center">
                      <p className="text-xs text-slate-500 mb-0.5">Predicted</p>
                      <p className="font-semibold text-white tabular-nums">
                        {pred.predictedHome}–{pred.predictedAway}
                      </p>
                    </div>

                    {/* Actual result */}
                    {hasResult ? (
                      <div className="text-center">
                        <p className="text-xs text-slate-500 mb-0.5">Result</p>
                        <p className="font-semibold tabular-nums text-slate-300">
                          {pred.actualHome}–{pred.actualAway}
                        </p>
                      </div>
                    ) : pred.locked ? (
                      <div className="text-center">
                        <p className="text-xs text-slate-500 mb-0.5">Result</p>
                        <p className="text-slate-500 text-xs">TBD</p>
                      </div>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}

// ── Main LeaderboardPage ─────────────────────────────────────────────────────

export function LeaderboardPage() {
  const { user } = useAuth();

  const [entries, setEntries] = useState<LeaderboardEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [drillDown, setDrillDown] = useState<MemberDrillDown | null>(null);
  const [drillDownLoading, setDrillDownLoading] = useState(false);
  const [drillDownError, setDrillDownError] = useState<string | null>(null);

  useEffect(() => {
    fetchLeaderboard()
      .then(setEntries)
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : 'Failed to load leaderboard.')
      )
      .finally(() => setLoading(false));
  }, []);

  const handleRowClick = async (entry: LeaderboardEntry) => {
    if (!user) return; // not signed in — silently ignore

    setDrillDown(null);
    setDrillDownError(null);
    setDrillDownLoading(true);

    try {
      const data = await fetchDrillDown(entry.userId);
      setDrillDown(data);
    } catch (err: unknown) {
      setDrillDownError(
        err instanceof Error ? err.message : 'Failed to load member details.'
      );
    } finally {
      setDrillDownLoading(false);
    }
  };

  // ── Loading state ──────────────────────────────────────────────────────────
  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <p className="text-slate-400">Loading leaderboard…</p>
      </div>
    );
  }

  // ── Error state ────────────────────────────────────────────────────────────
  if (error) {
    return (
      <div className="max-w-2xl mx-auto p-6">
        <p className="text-red-400 bg-red-950/40 rounded-lg px-4 py-3 text-sm">{error}</p>
      </div>
    );
  }

  // ── Empty state ────────────────────────────────────────────────────────────
  if (entries.length === 0) {
    return (
      <div className="max-w-2xl mx-auto p-6 text-center py-20">
        <p className="text-slate-400">
          No scores yet — leaderboard will populate after the first results.
        </p>
      </div>
    );
  }

  // ── Drill-down view ────────────────────────────────────────────────────────
  if (drillDown !== null) {
    return (
      <div className="max-w-3xl mx-auto p-6">
        <DrillDownPanel
          drillDown={drillDown}
          isOwnProfile={user?.userId === drillDown.userId}
          onClose={() => setDrillDown(null)}
        />
      </div>
    );
  }

  // ── Main leaderboard table ─────────────────────────────────────────────────
  return (
    <div className="max-w-3xl mx-auto p-6 space-y-4">
      <h1 className="text-2xl font-bold text-white">Leaderboard</h1>

      {drillDownLoading && (
        <p className="text-slate-400 text-sm">Loading member details…</p>
      )}
      {drillDownError && (
        <p className="text-red-400 text-sm bg-red-950/40 rounded-lg px-4 py-2">
          {drillDownError}
        </p>
      )}

      <div className="bg-slate-800 rounded-xl overflow-hidden">
        {/* Table header */}
        <div className="grid grid-cols-[3rem_1fr_5rem_5rem_5rem_5rem] gap-2 px-4 py-3 bg-slate-700/60 text-xs font-medium text-slate-400 uppercase tracking-wide">
          <div className="text-center">#</div>
          <div>Member</div>
          <div className="text-right">Total</div>
          <div className="text-right">Matches</div>
          <div className="text-right">Champ</div>
          <div className="text-right">G6</div>
        </div>

        {/* Table rows */}
        <div className="divide-y divide-slate-700/50">
          {entries.map((entry) => {
            const isCurrentUser = user?.userId === entry.userId;
            return (
              <button
                key={entry.userId}
                onClick={user ? () => void handleRowClick(entry) : undefined}
                disabled={!user}
                className={[
                  'w-full grid grid-cols-[3rem_1fr_5rem_5rem_5rem_5rem] gap-2 px-4 py-3.5 text-sm text-left transition-colors',
                  user
                    ? 'hover:bg-slate-700/50 cursor-pointer'
                    : 'cursor-default',
                  isCurrentUser ? 'bg-blue-950/30' : '',
                ].join(' ')}
              >
                <div className="text-center text-base">
                  <RankBadge rank={entry.rank} />
                </div>
                <div className="flex items-center gap-2 min-w-0">
                  <span
                    className={[
                      'font-medium truncate',
                      isCurrentUser ? 'text-blue-300' : 'text-white',
                    ].join(' ')}
                  >
                    {entry.displayName}
                  </span>
                  {isCurrentUser && (
                    <span className="text-xs text-blue-400 shrink-0">(you)</span>
                  )}
                </div>
                <div className="text-right font-bold text-white tabular-nums">
                  {entry.totalPoints}
                </div>
                <div className="text-right text-slate-400 tabular-nums">
                  {entry.groupMatchPoints}
                </div>
                <div className="text-right text-slate-400 tabular-nums">
                  {entry.championPoints}
                </div>
                <div className="text-right text-slate-400 tabular-nums">
                  {entry.goldenSixPoints}
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {user && (
        <p className="text-slate-500 text-xs text-center">
          Click a row to see that member's predictions.
        </p>
      )}
    </div>
  );
}
