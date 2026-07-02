import { useEffect, useRef, useState } from 'react';
import { ChevronLeft, Search, X } from 'lucide-react';
import { useAuth } from '../auth/useAuth.ts';
import { Spinner } from '../components/ui/Spinner.tsx';
import { SkeletonLeaderboard, SkeletonRankCard } from '../components/ui/Skeleton.tsx';

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
  points: number | null;
}

interface GoldenSixDetail {
  playerId: string;
  name: string;
  teamId: string;
  position: string;
  goals: number;
  points: number;
}

interface KnockoutPredictionDetail {
  slotKey: string;
  round: string;
  homeTeamId: string | null;
  awayTeamId: string | null;
  predictedWinnerTeamId: string;
  predictedHomeScore: number | null;
  predictedAwayScore: number | null;
  actualHomeScore: number | null;
  actualAwayScore: number | null;
  actualWinnerTeamId: string | null;
  kickoffUtc: string | null;
  locked: boolean;
  multiplier: number;
  scorePoints: number | null;
  winnerPoints: number | null;
}

interface MemberDrillDown {
  userId: string;
  displayName: string;
  totalPoints: number;
  groupPredictions: GroupPredictionDetail[];
  knockoutPredictions: KnockoutPredictionDetail[];
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

  const nameSize: Record<number, string> = { 1: 'text-[20px]', 2: 'text-[18px]', 3: 'text-[16px]' };
  const ptsSize:  Record<number, string> = { 1: 'text-[19px]', 2: 'text-[17px]', 3: 'text-[15px]' };

  type Slot = { entry: LeaderboardEntry; colorVar: string; barH: string; avatarSize: number };
  const slots: Slot[] = [
    second && { entry: second, colorVar: 'var(--color-podium-silver)', barH: 'h-18', avatarSize: 50 },
    { entry: first,  colorVar: 'var(--color-podium-gold)',   barH: 'h-24', avatarSize: 56 },
    third  && { entry: third,  colorVar: 'var(--color-podium-bronze)', barH: 'h-14', avatarSize: 44 },
  ].filter(Boolean) as Slot[];

  return (
    <div className="flex items-end justify-center gap-0.5 pt-6 px-1">
      {slots.map(({ entry, colorVar, barH, avatarSize }) => {
        const isCurrentUser = currentUserId === entry.userId;
        return (
          <button
            key={entry.userId}
            onClick={selectable ? () => onSelect(entry) : undefined}
            disabled={!selectable}
            className={`flex flex-col items-center flex-1 min-w-0 ${selectable ? 'cursor-pointer group' : 'cursor-default'}`}
          >
            <span className={`${nameSize[entry.rank]} font-bold text-center truncate w-full px-1 ${isCurrentUser ? 'text-secondary' : 'text-fg'}`}>
              {entry.displayName}
              {isCurrentUser && <span className="text-secondary ml-1 text-[11px]">(you)</span>}
            </span>
            <span className={`${ptsSize[entry.rank]} font-black tnum mb-2`} style={{ color: colorVar }}>
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

        {/* Knockout predictions */}
        {drillDown.knockoutPredictions.length > 0 && (
          <div>
            <SectionLabel>Knockout predictions <span className="text-fg-muted font-normal normal-case">(started &amp; finished)</span></SectionLabel>
            <div className="space-y-1.5">
              {drillDown.knockoutPredictions.map(pred => {
                const correct      = pred.actualWinnerTeamId !== null && pred.predictedWinnerTeamId === pred.actualWinnerTeamId;
                const wrong        = pred.actualWinnerTeamId !== null && pred.predictedWinnerTeamId !== pred.actualWinnerTeamId;
                const hasResult    = pred.actualWinnerTeamId !== null;
                const totalPoints  = pred.scorePoints !== null && pred.winnerPoints !== null ? pred.scorePoints + pred.winnerPoints : null;
                return (
                  <div key={pred.slotKey} className="rounded-input bg-surface-2 text-sm overflow-hidden">
                    {/* Slot key label */}
                    <div className="flex items-center gap-1.5 px-4 pt-2.5 pb-1">
                      <span className="font-mono text-[10px] text-fg-muted uppercase tracking-wider">{pred.slotKey}</span>
                    </div>

                    {/* Score row — same layout as group phase */}
                    <div className="flex items-center justify-between px-4 pb-2.5">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="font-mono font-semibold text-fg text-xs">{pred.homeTeamId ?? '?'}</span>
                        <span className="text-fg-muted text-xs">vs</span>
                        <span className="font-mono font-semibold text-fg text-xs">{pred.awayTeamId ?? '?'}</span>
                      </div>
                      <div className="flex items-center gap-4 shrink-0 ml-4">
                        <div className="text-center">
                          <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Predicted</p>
                          {pred.predictedHomeScore !== null && pred.predictedAwayScore !== null
                            ? <p className="font-semibold text-fg tnum">{pred.predictedHomeScore}–{pred.predictedAwayScore}</p>
                            : <p className="text-fg-muted text-xs">—</p>}
                        </div>
                        <div className="text-center">
                          <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Result</p>
                          {pred.actualHomeScore !== null && pred.actualAwayScore !== null
                            ? <p className="font-semibold text-fg-secondary tnum">{pred.actualHomeScore}–{pred.actualAwayScore}</p>
                            : <p className="text-fg-muted text-xs">TBD</p>}
                        </div>
                        {/* Score pts — same slot as group phase Pts column */}
                        <div className="text-center">
                          <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Pts</p>
                          {pred.scorePoints !== null
                            ? <p className={`font-bold text-[13px] tnum ${pred.scorePoints > 0 ? 'text-green-400' : 'text-fg-muted'}`}>+{pred.scorePoints}</p>
                            : <p className="text-fg-muted text-xs">—</p>}
                        </div>
                      </div>
                    </div>

                    {/* Winner row + winner pts breakdown */}
                    <div className="flex items-center justify-between border-t border-border px-4 py-2.5 gap-4">
                      {/* Predicted winner | Actual winner side by side */}
                      <div className="flex items-center gap-4">
                        <div className="text-center">
                          <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Pred. winner</p>
                          <p className={`font-semibold text-[13px] ${correct ? 'text-green-400' : wrong ? 'text-red-400' : 'text-fg'}`}>
                            {pred.predictedWinnerTeamId}
                            {pred.multiplier > 1 && (
                              <span className="ml-1 text-[10px] font-bold px-1 py-0.5 rounded" style={{ background: 'rgba(99,102,241,0.15)', color: 'var(--primary)' }}>
                                ×{pred.multiplier}
                              </span>
                            )}
                          </p>
                        </div>
                        <div className="text-center">
                          <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Actual winner</p>
                          {hasResult
                            ? <p className="font-semibold text-[13px] text-fg-secondary">{pred.actualWinnerTeamId}</p>
                            : <p className="text-fg-muted text-xs">TBD</p>}
                        </div>
                      </div>

                      {/* Winner pts + total */}
                      {totalPoints !== null && (
                        <div className="flex items-center gap-2 shrink-0">
                          <div className="text-center">
                            <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">
                              Winner{pred.multiplier > 1 ? ` ×${pred.multiplier}` : ''}
                            </p>
                            <p className={`font-semibold text-[13px] tnum ${pred.winnerPoints! > 0 ? 'text-green-400' : 'text-fg-muted'}`}>
                              +{pred.winnerPoints}
                            </p>
                          </div>
                          <div className="text-center border-l border-border pl-2">
                            <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Total</p>
                            <p className={`font-bold text-[13px] tnum ${totalPoints > 0 ? 'text-green-400' : 'text-fg-muted'}`}>
                              +{totalPoints}
                            </p>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
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
                        {hasResult && pred.points !== null && (
                          <div className="text-center">
                            <p className="text-[10px] text-fg-muted mb-0.5 uppercase tracking-wider font-display font-bold">Pts</p>
                            <p className={`font-bold text-[13px] tnum ${pred.points > 0 ? 'text-green-400' : 'text-fg-muted'}`}>+{pred.points}</p>
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
  const [searchQuery, setSearchQuery] = useState('');
  const searchInputRef = useRef<HTMLInputElement>(null);

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

  if (loading) return (
    <div className="max-w-3xl mx-auto px-4 py-4 space-y-3">
      <SkeletonRankCard />
      <SkeletonLeaderboard />
    </div>
  );
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

  const isSearching = searchQuery.trim().length > 0;
  const normalised  = searchQuery.trim().toLowerCase();
  const filteredEntries = isSearching
    ? entries.filter(e => e.displayName.toLowerCase().includes(normalised))
    : entries;

  const top3 = filteredEntries.filter(e => e.rank <= 3);
  const rest  = filteredEntries.filter(e => e.rank > 3);

  // When searching, render all filtered entries (including ranks 1–3) in the flat list.
  const listEntries = isSearching ? filteredEntries : rest;

  const myEntry = user ? entries.find(e => e.userId === user.userId) : undefined;

  return (
    <>
    <div className="max-w-3xl mx-auto px-4 pt-4 pb-[5.5rem] space-y-3">
      {drillDownLoading && (
        <div className="flex justify-center py-4">
          <Spinner size="md" />
        </div>
      )}
      {drillDownError && (
        <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>
          {drillDownError}
        </p>
      )}

      {/* Your position banner */}
      {myEntry && (
        <div className="rounded-card bg-surface border border-border p-5 flex items-center justify-between">
          <div>
            <p className="text-[11px] font-display font-bold uppercase tracking-wider text-fg-muted">Your Rank</p>
            <p className="font-display font-black text-4xl tnum mt-1">
              {myEntry.rank}
              <span className="text-lg text-fg-muted font-bold"> / {entries.length}</span>
            </p>
          </div>
          <div className="text-right">
            <p className="text-[11px] font-display font-bold uppercase tracking-wider text-fg-muted">Your Points</p>
            <p className="font-display font-black text-4xl tnum mt-1" style={{ color: 'var(--primary)' }}>
              {myEntry.totalPoints}
            </p>
          </div>
        </div>
      )}

      <div className="rounded-card bg-surface border border-border overflow-hidden">
        {/* Podium — hidden while a search is active */}
        {!isSearching && top3.length > 0 && (
          <PodiumSection
            entries={top3}
            currentUserId={user?.userId}
            onSelect={(entry) => void handleRowClick(entry)}
            selectable={!!user}
          />
        )}

        {listEntries.length > 0 ? (
          <>
            <div className="grid grid-cols-[2rem_1fr_3.5rem] gap-x-3 px-4 py-2.5 bg-surface-2 text-[10px] font-display font-bold uppercase tracking-wider text-fg-muted items-center">
              <span className="text-center">#</span>
              <span>Member</span>
              <span className="text-right">Pts</span>
            </div>
            <div className="divide-y divide-border">
              {listEntries.map(entry => {
                const isCurrentUser = user?.userId === entry.userId;
                return (
                  <button
                    key={entry.userId}
                    onClick={user ? () => void handleRowClick(entry) : undefined}
                    disabled={!user}
                    className={`w-full grid grid-cols-[2rem_1fr_3.5rem] gap-x-3 px-4 py-3 text-left items-center transition-colors ${
                      user ? 'hover:bg-surface-2 cursor-pointer' : 'cursor-default'
                    } ${isCurrentUser ? 'bg-blue-500/10' : ''}`}
                  >
                    <span className="font-display font-black text-fg-muted text-[13px] text-center tnum">
                      {entry.rank}
                    </span>
                    <div className="flex items-center gap-2 min-w-0">
                      {entry.countryCode && (
                        <img
                          src={`https://flagcdn.com/w40/${entry.countryCode.toLowerCase()}.png`}
                          alt={entry.countryCode}
                          width={20} height={14}
                          className="shrink-0 rounded-sm"
                        />
                      )}
                      <span className={`font-semibold truncate text-[14px] ${isCurrentUser ? 'text-secondary' : 'text-fg'}`}>
                        {entry.displayName}
                      </span>
                      {isCurrentUser && <span className="text-[11px] text-secondary shrink-0">(you)</span>}
                    </div>
                    <span className="font-display font-black text-[18px] tnum text-right">{entry.totalPoints}</span>
                  </button>
                );
              })}
            </div>
          </>
        ) : isSearching ? (
          <div className="px-4 py-10 text-center">
            <p className="text-fg-muted text-sm">No members match &ldquo;{searchQuery}&rdquo;.</p>
          </div>
        ) : null}
      </div>

      {user && (
        <p className="text-[12px] text-fg-muted text-center">Click a row to see that member&apos;s predictions.</p>
      )}
    </div>

    {/* Fixed search bar — sits flush above the bottom nav, same width as leaderboard content */}
    <div className="fixed bottom-14 inset-x-0 z-30">
      <div className="max-w-3xl mx-auto px-4">
        <div className="relative flex items-center bg-bg-elevated/95 backdrop-blur-sm border border-border rounded-input shadow-lg">
          <Search size={15} className="absolute left-3 text-fg-muted pointer-events-none" />
          <input
            ref={searchInputRef}
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="Search by name…"
            className="w-full bg-transparent pl-8 pr-8 py-2.5 text-[14px] text-fg placeholder:text-fg-muted focus:outline-none"
          />
          {isSearching && (
            <button
              onClick={() => { setSearchQuery(''); searchInputRef.current?.focus(); }}
              className="absolute right-2.5 text-fg-muted hover:text-fg transition-colors"
              aria-label="Clear search"
            >
              <X size={15} />
            </button>
          )}
        </div>
      </div>
    </div>
    </>
  );
}
