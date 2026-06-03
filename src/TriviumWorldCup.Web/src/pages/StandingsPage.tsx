import { useEffect, useState } from 'react';

interface GoldenSixPlayer {
  playerId: string;
  name: string;
  teamId: string;
  position: string;
  goals: number;
  points: number;
}

interface MyStandings {
  userId: string;
  totalPoints: number;
  groupMatchPoints: number;
  championPoints: number;
  goldenSixPoints: number;
  rank: number;
  totalMembers: number;
  goldenSix: GoldenSixPlayer[];
}

async function fetchMyStandings(): Promise<MyStandings | null> {
  const res = await fetch('/scores/me', { credentials: 'include' });
  if (res.ok) return res.json() as Promise<MyStandings>;
  return null;
}

/**
 * My Standings page — shows the authenticated user's points breakdown, rank, and Golden Six detail.
 * TWC-10.
 */
export function StandingsPage() {
  const [standings, setStandings] = useState<MyStandings | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchMyStandings()
      .then(data => {
        setStandings(data);
      })
      .catch(() => {
        setError('Failed to load standings. Please try again.');
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="p-8 text-slate-400">Loading standings…</div>
    );
  }

  if (error) {
    return (
      <div className="p-8 text-red-400 bg-red-950/40 rounded-lg mx-6 mt-6">{error}</div>
    );
  }

  if (!standings) {
    return (
      <div className="p-8 text-slate-400">Unable to load standings.</div>
    );
  }

  const hasNoScores =
    standings.totalPoints === 0 &&
    standings.goldenSix.length === 0;

  return (
    <div className="max-w-2xl mx-auto p-6 space-y-6">
      <h1 className="text-2xl font-bold text-white">My Standings</h1>

      {/* Rank badge */}
      <div className="bg-slate-800 border border-slate-700 rounded-xl p-5 flex items-center justify-between">
        <div>
          <p className="text-sm text-slate-400 uppercase tracking-wide">Current rank</p>
          <p className="text-3xl font-bold text-white mt-1">
            {standings.rank}
            <span className="text-lg text-slate-400 font-normal"> / {standings.totalMembers}</span>
          </p>
        </div>
        <div className="text-right">
          <p className="text-sm text-slate-400 uppercase tracking-wide">Total points</p>
          <p className="text-3xl font-bold text-blue-400 mt-1">{standings.totalPoints}</p>
        </div>
      </div>

      {/* Points breakdown */}
      <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
        <div className="px-5 py-3 border-b border-slate-700">
          <h2 className="text-sm font-semibold text-slate-300 uppercase tracking-wide">Points breakdown</h2>
        </div>
        <div className="divide-y divide-slate-700">
          <BreakdownRow label="Group matches" points={standings.groupMatchPoints} />
          <BreakdownRow label="Champion prediction" points={standings.championPoints} />
          <BreakdownRow label="Golden Six" points={standings.goldenSixPoints} />
          <div className="px-5 py-3 flex items-center justify-between">
            <span className="text-sm font-semibold text-white">Total</span>
            <span className="text-sm font-bold text-blue-400">{standings.totalPoints} pts</span>
          </div>
        </div>
      </div>

      {/* No scores message */}
      {hasNoScores && (
        <div className="bg-slate-800 border border-slate-700 rounded-xl p-5 text-slate-400 text-sm">
          No matches scored yet — check back after the first results.
        </div>
      )}

      {/* Golden Six table */}
      {standings.goldenSix.length > 0 && (
        <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
          <div className="px-5 py-3 border-b border-slate-700">
            <h2 className="text-sm font-semibold text-slate-300 uppercase tracking-wide">Golden Six</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-slate-400 text-xs uppercase tracking-wide border-b border-slate-700">
                  <th className="px-5 py-2 text-left">Player</th>
                  <th className="px-5 py-2 text-left">Team</th>
                  <th className="px-5 py-2 text-left">Pos</th>
                  <th className="px-5 py-2 text-right">Goals</th>
                  <th className="px-5 py-2 text-right">Points</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-700">
                {standings.goldenSix.map(player => (
                  <tr key={player.playerId} className="text-slate-200 hover:bg-slate-700/50 transition-colors">
                    <td className="px-5 py-3 font-medium">{player.name}</td>
                    <td className="px-5 py-3 text-slate-400">{player.teamId}</td>
                    <td className="px-5 py-3">
                      <span className="inline-block bg-slate-700 text-slate-300 text-xs px-2 py-0.5 rounded">
                        {player.position}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-right">{player.goals}</td>
                    <td className="px-5 py-3 text-right font-semibold text-blue-400">{player.points}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

function BreakdownRow({ label, points }: { label: string; points: number }) {
  return (
    <div className="px-5 py-3 flex items-center justify-between">
      <span className="text-sm text-slate-300">{label}</span>
      <span className="text-sm text-slate-200">{points} pts</span>
    </div>
  );
}
