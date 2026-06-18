import { useEffect, useState } from 'react';
import { ChevronLeft } from 'lucide-react';
import { useAuth } from '../auth/useAuth.ts';

interface LeaderboardEntry {
  rank: number;
  userId: string;
  displayName: string;
  countryCode?: string;
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

async function fetchLeaderboard(): Promise<LeaderboardEntry[]> {
  const res = await fetch('/leaderboard', { credentials: 'include' });
  if (!res.ok) throw new Error(`Leaderboard fetch failed: ${res.status}`);
  return res.json() as Promise<LeaderboardEntry[]>;
}

async function fetchDrillDown(userId: string): Promise<MemberDrillDown> {
  const res = await fetch(`/leaderboard/${encodeURIComponent(userId)}`, { credentials: 'include' });
  if (!res.ok) throw new Error(`Drill-down fetch failed: ${res.status}`);
  return res.json() as Promise<MemberDrillDown>;
}
interface PodiumSectionProps {
  entries: LeaderboardEntry[];
  currentUserId?: string;
  onSelect: (entry: LeaderboardEntry) => void;
  selectable: boolean;
}

function PodiumSection({ entries, currentUserId, onSelect, selectable }: PodiumSectionProps) {
  const first  = entries.find(e => e.rank === 1);
  const second = entries.find(e => e.rank === 2);
  const third  = entries.find(e => e.rank === 3);
  if (!first) return null;

  type Slot = { entry: LeaderboardEntry; colorVar: string; barH: string; avatarSize: number };
  const slots: Slot[] = [
    second && { entry: second, colorVar: 'var(--color-podium-silver)', barH: 'h-14', avatarSize: 48 },
    { entry: first,  colorVar: 'var(--color-podium-gold)',   barH: 'h-24', avatarSize: 56 },
    third  && { entry: third,  colorVar: 'var(--color-podium-bronze)', barH: 'h-10', avatarSize: 44 },
  ].filter(Boolean) as Slot[];

  return (
    <div className="flex items-end justify-center gap-0.5 px-2 pt-6">
      {slots.map(({ entry, colorVar, barH, avatarSize }) => {
        const isCurrentUser = currentUserId === entry.userId;
        return (
          <button
            key={entry.userId}
            onClick={selectable ? () => onSelect(entry) : undefined}
            disabled={!selectable}
            className={`flex flex-col items-center flex-1 min-w-0 ${selectable ? 'cursor-pointer group' : 'cursor-default'}`}
          >
            <span className={`text-[12px] font-semibold text-center truncate w-full px-1 ${isCurrentUser ? 'text-secondary' : 'text-fg'}`}>
              {entry.displayName}
              {isCurrentUser && <span className="text-secondary ml-1 text-[11px]">(you)</span>}
            </span>
            <span className="text-[13px] font-black tnum mb-2" style={{ color: colorVar }}>
              {entry.totalPoints} pts
            </span>
            <div
              className="rounded-full overflow-hidden bg-surface-2 mb-2 shrink-0"
              style={{ width: avatarSize, height: avatarSize, border: `2.5px solid ${colorVar}` }}
            >
              {entry.countryCode ? (
                <img
                  src={`https://flagcdn.com/w80/${entry.countryCode.toLowerCase()}.png`}
                  alt={entry.countryCode}
                  className="w-full h-full object-cover"
                />
              ) : (
                <div className="w-full h-full flex items-center justify-center font-display font-black text-fg-muted text-lg">
                  {entry.displayName[0]}
                </div>
              )}
            </div>
            <div
              className={`${barH} w-full rounded-t-md flex items-center justify-center transition-opacity group-hover:opacity-100`}
              style={{ background: colorVar, opacity: 0.88 }}
            >
              <span className="font-display font-black text-[28px]" style={{ color: 'rgba(0,0,0,0.28)' }}>
                {entry.rank}
              </span>
            </div>
          </button>
        );
      })}
    </div>
  );
}

function RankBadge({ rank }: { rank: number }) {
  const podiumStyles: Record<number, { color: string; size: string }> = {
    1: { color: 'var(--color-podium-gold)', size: 'text-[20px]' },
    2: { color: 'var(--color-podium-silver)', size: 'text-[18px]' },
    3: { color: 'var(--color-podium-bronze)', size: 'text-[16px]' },
  };

  const style = podiumStyles[rank];

  if (style) {
    return (
      <span 
        className={`font-display font-black grid place-items-center w-7 h-7 tnum ${style.size}`}
        style={{ color: style.color }}
      >
        {rank}
      </span>
    );
  }

  return (
    <span className="font-display font-black text-fg-muted text-[14px] grid place-items-center w-7 h-7 tnum">
      {rank}
    </span>
  );
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="text-[11px] font-display font-bold uppercase tracking-[0.08em] text-fg-muted mb-2">
      {children}
    </p>
  );
}

interface DrillDownPanelProps {
  drillDown: MemberDrillDown;
  isOwnProfile: boolean;
  onClose: () => void;
}

function DrillDownPanel({ drillDown, isOwnProfile, onClose }: DrillDownPanelProps) {
  return (
    <div className="px-4 pb-6">
      <button onClick={onClose} className="flex items-center gap-1.5 text-[13px] font-medium mb-4 transition-colors"
              style={{ color: 'var(--link)' }}>
        <ChevronLeft size={16} />Back to leaderboard
      </button>

      <div className="rounded-card bg-surface border border-border p-5 space-y-5">
        <div>
          <div className="flex items-center gap-2">
            <h2 className={`font-display font-bold text-xl tracking-tight ${isOwnProfile ? 'text-secondary' : ''}`}>
              {drillDown.displayName}
            </h2>
            {isOwnProfile && (
              <span className="text-[11px] font-semibold px-1.5 py-0.5 rounded-full bg-blue-500/10 text-secondary">You</span>
            )}
          </div>
          <p className="text-[13px] text-fg-muted mt-0.5">{drillDown.totalPoints} pts</p>
        </div>

        {/* Total points */}
        <div className="rounded-card bg-surface-2 p-3 text-center">
          <p className="font-display font-black text-3xl tnum" style={{ color: 'var(--primary)' }}>{drillDown.totalPoints}</p>
          <p className="text-[11px] text-fg-muted mt-0.5">Total points</p>
        </div>

        {/* Champion pick */}
        {drillDown.championTeamId && (
          <div>
            <SectionLabel>Champion pick</SectionLabel>
            <p className="text-fg-secondary text-sm">
              <span className="font-semibold text-fg">{drillDown.championTeamName ?? drillDown.championTeamId}</span>
              {' '}<span className="font-mono text-fg-muted">({drillDown.championTeamId})</span>
            </p>
          </div>
        )}

        {/* Golden Six */}
        {drillDown.goldenSix.length > 0 && (
          <div>
            <SectionLabel>Golden Six</SectionLabel>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-fg-muted border-b border-border">
                    <th className="pb-2 pr-4 font-display font-bold text-[10px] uppercase tracking-wider">Player</th>
                    <th className="pb-2 pr-4 font-display font-bold text-[10px] uppercase tracking-wider">Team</th>
                    <th className="pb-2 pr-4 font-display font-bold text-[10px] uppercase tracking-wider text-right">Goals</th>
                    <th className="pb-2 font-display font-bold text-[10px] uppercase tracking-wider text-right">Pts</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {drillDown.goldenSix.map(p => (
                    <tr key={p.playerId} className="text-fg-secondary">
                      <td className="py-2 pr-4 font-medium text-fg">{p.name}</td>
                      <td className="py-2 pr-4 font-mono text-[12px]">{p.teamId}</td>
                      <td className="py-2 pr-4 text-right tnum">{p.goals}</td>
                      <td className="py-2 text-right tnum font-semibold text-fg">{p.points}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Group predictions — locked matches only, most recent first */}
        <div>
          <SectionLabel>Match predictions <span className="text-fg-muted font-normal normal-case">(started &amp; finished)</span></SectionLabel>
          {(() => {
            const played = [...drillDown.groupPredictions]
              .filter(p => p.locked)
              .sort((a, b) => new Date(b.kickoffUtc).getTime() - new Date(a.kickoffUtc).getTime());
            return played.length === 0 ? (
              <p className="text-fg-muted text-sm">No started or finished matches yet.</p>
            ) : (
              <div className="space-y-1.5">
                {played.map(pred => {
                  const hasResult = pred.actualHome !== null && pred.actualAway !== null;
                  return (
                    <div key={pred.fixtureId} className="flex items-center justify-between rounded-input px-4 py-3 text-sm bg-surface-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="font-mono font-semibold text-fg text-xs">{pred.homeTeamId}</span>
                        <span className="text-fg-muted text-xs">vs</span>
                        <span className="font-mono font-semibold text-fg text-xs">{pred.awayTeamId}</span>
                      </div>
                      <div className="flex items-center gap-4 shrink-0 ml-4">
                        <div className="text-center">
                          <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Predicted</p>
                          <p className="font-semibold text-fg tnum">{pred.predictedHome}–{pred.predictedAway}</p>
                        </div>
                        {hasResult ? (
                          <div className="text-center">
                            <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Result</p>
                            <p className="font-semibold text-fg-secondary tnum">{pred.actualHome}–{pred.actualAway}</p>
                          </div>
                        ) : (
                          <div className="text-center">
                            <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Result</p>
                            <p className="text-fg-muted text-xs">TBD</p>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            );
          })()}
        </div>

        <p className="text-[12px] text-fg-muted">Predictions are shown once a match has started, newest first.</p>
      </div>
    </div>
  );
}

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
      .catch((err: unknown) => setError(err instanceof Error ? err.message : 'Failed to load leaderboard.'))
      .finally(() => setLoading(false));
  }, []);

  const handleRowClick = async (entry: LeaderboardEntry) => {
    if (!user) return;
    setDrillDown(null);
    setDrillDownError(null);
    setDrillDownLoading(true);
    try {
      setDrillDown(await fetchDrillDown(entry.userId));
    } catch (err: unknown) {
      setDrillDownError(err instanceof Error ? err.message : 'Failed to load member details.');
    } finally {
      setDrillDownLoading(false);
    }
  };

  if (loading) return <div className="flex items-center justify-center py-20 text-fg-muted">Loading leaderboard…</div>;
  if (error) return (
    <div className="max-w-2xl mx-auto px-4 py-6">
      <p className="text-[13px] px-4 py-3 rounded-card" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>{error}</p>
    </div>
  );

  if (drillDown !== null) {
    return (
      <div className="max-w-3xl mx-auto pt-4">
        <DrillDownPanel drillDown={drillDown} isOwnProfile={user?.userId === drillDown.userId} onClose={() => setDrillDown(null)} />
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-6">
        <div className="rounded-card bg-surface border border-border px-4 py-20 text-center">
          <p className="text-fg-muted text-sm">No scores yet. The leaderboard will populate after the first results.</p>
        </div>
      </div>
    );
  }

  const top3 = entries.filter(e => e.rank <= 3);
  const rest  = entries.filter(e => e.rank > 3);

  return (
    <div className="max-w-3xl mx-auto px-4 py-4 space-y-3">
      {drillDownLoading && <p className="text-fg-muted text-sm">Loading member details…</p>}
      {drillDownError && (
        <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>
          {drillDownError}
        </p>
      )}

      <div className="rounded-card bg-surface border border-border overflow-hidden">
        {top3.length > 0 && (
          <PodiumSection
            entries={top3}
            currentUserId={user?.userId}
            onSelect={(entry) => void handleRowClick(entry)}
            selectable={!!user}
          />
        )}

        {rest.length > 0 && (
          <>
            <div className="grid grid-cols-[2.25rem_1fr_3.25rem] gap-2.5 px-4 py-2.5 mt-3 bg-surface-2 text-[10px] font-display font-bold uppercase tracking-wider text-fg-muted">
              <span className="text-center">#</span>
              <span>Member</span>
              <span className="text-right">Pts</span>
            </div>
            <div className="divide-y divide-border">
              {rest.map(entry => {
                const isCurrentUser = user?.userId === entry.userId;
                return (
                  <button
                    key={entry.userId}
                    onClick={user ? () => void handleRowClick(entry) : undefined}
                    disabled={!user}
                    className={`w-full grid grid-cols-[2.25rem_1fr_3.25rem] gap-2.5 px-4 py-3.5 text-left transition-colors ${
                      user ? 'hover:bg-surface-2 cursor-pointer' : 'cursor-default'
                    } ${isCurrentUser ? 'bg-blue-500/10' : ''}`}
                  >
                    <div className="flex justify-center"><RankBadge rank={entry.rank} /></div>
                    <div className="flex items-center gap-2 min-w-0">
                      {entry.countryCode && (
                        <img
                          src={`https://flagcdn.com/w40/${entry.countryCode.toLowerCase()}.png`}
                          alt={entry.countryCode}
                          width={20} height={14}
                          className="shrink-0 rounded-sm"
                        />
                      )}
                      <span className={`font-semibold truncate ${isCurrentUser ? 'text-secondary' : 'text-fg'}`}>
                        {entry.displayName}
                      </span>
                      {isCurrentUser && <span className="text-[11px] text-secondary shrink-0">(you)</span>}
                    </div>
                    <div className="font-display font-black text-[18px] tnum text-right">{entry.totalPoints}</div>
                  </button>
                );
              })}
            </div>
          </>
        )}


      </div>

      {user && (
        <p className="text-[12px] text-fg-muted text-center">Click a row to see that member&apos;s predictions.</p>
      )}
    </div>
  );
}
