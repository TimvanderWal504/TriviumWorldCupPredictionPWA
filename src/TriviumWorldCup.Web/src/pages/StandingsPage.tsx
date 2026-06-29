import { useEffect, useState } from 'react';
import { Spinner } from '../components/ui/Spinner.tsx';

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
  knockoutPoints: number;
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

export function StandingsPage() {
  const [standings, setStandings] = useState<MyStandings | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchMyStandings()
      .then(setStandings)
      .catch(() => setError('Failed to load standings. Please try again.'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return (
    <div className="flex items-center justify-center py-20">
      <Spinner size="lg" label="Loading standings" />
    </div>
  );
  if (error) return <div className="p-8 rounded-card mx-6 mt-6 text-[13px]" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>{error}</div>;
  if (!standings) return <div className="p-8 text-fg-muted">Unable to load standings.</div>;

  const hasNoScores = standings.totalPoints === 0 && standings.goldenSix.length === 0;

  return (
    <div className="max-w-2xl mx-auto px-4 py-4 space-y-4">

      {/* Rank + total */}
      <div className="rounded-card bg-surface border border-border p-5 flex items-center justify-between">
        <div>
          <p className="text-[11px] font-display font-bold uppercase tracking-wider text-fg-muted">Current Rank</p>
          <p className="font-display font-black text-4xl tnum mt-1">
            {standings.rank}
            <span className="text-lg text-fg-muted font-bold"> / {standings.totalMembers}</span>
          </p>
        </div>
        <div className="text-right">
          <p className="text-[11px] font-display font-bold uppercase tracking-wider text-fg-muted">Total Points</p>
          <p className="font-display font-black text-4xl tnum mt-1" style={{ color: 'var(--primary)' }}>
            {standings.totalPoints}
          </p>
        </div>
      </div>

      {/* Points breakdown */}
      <div className="rounded-card bg-surface border border-border overflow-hidden">
        <div className="px-5 py-3 border-b border-border bg-surface-2">
          <h2 className="text-[10px] font-display font-bold uppercase tracking-wider text-fg-muted">Points Breakdown</h2>
        </div>
        <div className="divide-y divide-border">
          {[
            ['Group matches', standings.groupMatchPoints],
            ['Knockout phase', standings.knockoutPoints],
            ['Champion prediction', standings.championPoints],
            ['Golden Six', standings.goldenSixPoints],
          ].map(([label, pts]) => (
            <div key={label as string} className="px-5 py-3 flex items-center justify-between">
              <span className="text-sm text-fg-secondary">{label}</span>
              <span className="text-sm text-fg tnum">{pts} pts</span>
            </div>
          ))}
          <div className="px-5 py-3 flex items-center justify-between">
            <span className="font-semibold text-fg">Total</span>
            <span className="font-display font-bold tnum" style={{ color: 'var(--primary)' }}>
              {standings.totalPoints} pts
            </span>
          </div>
        </div>
      </div>

      {hasNoScores && (
        <div className="rounded-card bg-surface border border-border p-5 text-fg-muted text-sm">
          No matches scored yet. Check back after the first results.
        </div>
      )}

      {/* Golden Six */}
      {standings.goldenSix.length > 0 && (
        <div className="rounded-card bg-surface border border-border overflow-hidden">
          <div className="px-5 py-3 border-b border-border bg-surface-2">
            <h2 className="text-[10px] font-display font-bold uppercase tracking-wider text-fg-muted">
              Golden Six (top scorers)
            </h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-fg-muted text-[10px] font-display font-bold uppercase tracking-wider border-b border-border">
                  <th className="px-5 py-2 text-left">Player</th>
                  <th className="px-5 py-2 text-left">Team</th>
                  <th className="px-5 py-2 text-left">Pos</th>
                  <th className="px-5 py-2 text-right">Goals</th>
                  <th className="px-5 py-2 text-right">Pts</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {standings.goldenSix.map(player => (
                  <tr key={player.playerId} className="text-fg-secondary hover:bg-surface-2 transition-colors">
                    <td className="px-5 py-3 font-medium text-fg">{player.name}</td>
                    <td className="px-5 py-3 font-mono text-[12px]">{player.teamId}</td>
                    <td className="px-5 py-3">
                      <span className="inline-block bg-surface-3 text-fg-secondary text-xs px-2 py-0.5 rounded-chip font-mono">
                        {player.position}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-right tnum">{player.goals}</td>
                    <td className="px-5 py-3 text-right tnum font-semibold" style={{ color: 'var(--primary)' }}>
                      {player.points}
                    </td>
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
