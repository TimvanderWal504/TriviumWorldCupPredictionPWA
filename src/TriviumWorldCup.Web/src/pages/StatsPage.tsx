import { useEffect, useState } from 'react';
import { flagUrl } from '../utils/flagUrl.ts';
import { Spinner } from '../components/ui/Spinner.tsx';

// ── Types ────────────────────────────────────────────────────────────────────

interface ChampionPick {
  teamId: string; teamName: string; countryCode: string | null; count: number;
}

interface GoldenSixPick {
  playerId: string; playerName: string; teamName: string; teamId: string;
  countryCode: string | null; position: string; pickCount: number; goals: number;
}

interface FixtureOutcome {
  fixtureId: string; matchNumber: number; groupLetter: string;
  homeTeamId: string; homeTeamName: string; homeCountryCode: string | null;
  awayTeamId: string; awayTeamName: string; awayCountryCode: string | null;
  kickoffUtc: string; totalPredictions: number;
  homeWinCount: number; drawCount: number; awayWinCount: number;
  avgHomeScore: number | null; avgAwayScore: number | null;
  actualHomeScore: number | null; actualAwayScore: number | null;
}

interface SlotTeamCount { teamId: string; teamName: string; countryCode: string | null; count: number; }

interface SlotDistribution {
  slotKey: string; round: string; slotNumber: number;
  homeTeamId: string | null; homeTeamName: string | null; homeCountryCode: string | null;
  awayTeamId: string | null; awayTeamName: string | null; awayCountryCode: string | null;
  totalPredictions: number; teamCounts: SlotTeamCount[];
  actualHomeScore: number | null; actualAwayScore: number | null; actualWinnerTeamId: string | null;
  avgPredictedHomeScore: number | null; avgPredictedAwayScore: number | null;
}

interface FinalistPair {
  team1Id: string; team1Name: string; team1CountryCode: string | null;
  team2Id: string; team2Name: string; team2CountryCode: string | null;
  count: number;
}

interface HistogramBucket { label: string; min: number; max: number; count: number; }

interface AdminStats {
  participation: {
    totalUsers: number;
    usersWithTournamentPrediction: number;
    usersWithAnyGroupPrediction: number;
    totalGroupFixtures: number;
    totalGroupPredictionsSubmitted: number;
    totalPossibleGroupPredictions: number;
    submissionTimeline: { date: string; count: number }[];
  };
  tournamentPredictions: {
    championPicks: ChampionPick[];
    topGoldenSixPicks: GoldenSixPick[];
    goldenSixByPosition: { fwd: number; mid: number; def: number; gk: number };
    uniqueGoldenSixCombos: number;
  };
  groupPredictions: {
    fixtureOutcomes: FixtureOutcome[];
    topScorelinesOverall: { homeScore: number; awayScore: number; count: number }[];
    topKnockoutScorelinesOverall: { homeScore: number; awayScore: number; count: number }[];
  };
  knockoutPredictions: {
    slotDistributions: SlotDistribution[];
    finalistPairs: FinalistPair[];
  };
  scores: {
    histogram: HistogramBucket[];
    avgTotal: number; avgGroupPoints: number; avgKnockoutPoints: number;
    avgChampionPoints: number; avgGoldenSixPoints: number;
    avgExactScorelinesCount: number;
    avgCorrectOutcomeCount: number;
    maxTotalPoints: number; minTotalPoints: number;
  };
}

// ── Helpers ──────────────────────────────────────────────────────────────────

const ROUND_LABELS: Record<string, string> = {
  R32: 'Round of 32', R16: 'Round of 16', QF: 'Quarter-finals',
  SF: 'Semi-finals', ThirdPlace: 'Third-place play-off', Final: 'Final',
};

const POSITION_COLORS: Record<string, string> = {
  FWD: 'var(--win)', MID: 'var(--secondary)', DEF: 'var(--warning)', GK: 'var(--live)',
};

function shortDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

function pct(n: number, total: number) {
  if (total === 0) return 0;
  return Math.round((n / total) * 100);
}

function Flag({ teamId, size = 20 }: { teamId: string; size?: number }) {
  const src = flagUrl(teamId);
  if (!src) return null;
  return <img src={src} width={size} height={size * 0.67} className="rounded-sm object-cover shrink-0" alt="" />;
}

function StatCard({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div className="rounded-card bg-surface border border-border p-4 flex flex-col gap-1">
      <span className="text-[11px] font-display font-bold uppercase tracking-wider text-fg-muted">{label}</span>
      <span className="text-2xl font-bold text-fg">{value}</span>
      {sub && <span className="text-xs text-fg-muted">{sub}</span>}
    </div>
  );
}

// ── Component ─────────────────────────────────────────────────────────────────

export function StatsPage() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [groupPage, setGroupPage] = useState(0);

  useEffect(() => {
    fetch('/admin/stats', { credentials: 'include' })
      .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
      .then((d: AdminStats) => setStats(d))
      .catch(err => setError(String(err)))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return (
    <div className="flex items-center justify-center py-20">
      <Spinner size="lg" label="Loading statistics" />
    </div>
  );
  if (error)   return <p className="text-sm p-4" style={{ color: 'var(--loss)' }}>Failed to load: {error}</p>;
  if (!stats)  return null;

  const { participation, tournamentPredictions, groupPredictions, knockoutPredictions, scores } = stats;
  const labelCls = 'text-[11px] font-display font-bold uppercase tracking-wider text-fg-muted';

  // Group-stage fixture pagination
  const GROUP_PAGE_SIZE = 12;
  const groupFixtures = groupPredictions.fixtureOutcomes;
  const totalGroupPages = Math.max(1, Math.ceil(groupFixtures.length / GROUP_PAGE_SIZE));
  const pagedFixtures = groupFixtures.slice(groupPage * GROUP_PAGE_SIZE, (groupPage + 1) * GROUP_PAGE_SIZE);

  // Knockout slots grouped by round
  const slotsByRound = knockoutPredictions.slotDistributions.reduce<Record<string, SlotDistribution[]>>((acc, s) => {
    (acc[s.round] ??= []).push(s);
    return acc;
  }, {});
  const roundOrder = ['R32', 'R16', 'QF', 'SF', 'ThirdPlace', 'Final'];

  // Score histogram max count for bar scaling
  const maxHistogramCount = Math.max(1, ...scores.histogram.map(b => b.count));

  return (
    <div className="space-y-6">

      {/* ── Participation ──────────────────────────────────────────────────── */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <h2 className="font-display font-bold text-lg tracking-tight">Participation</h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
          <StatCard label="Registered users"   value={participation.totalUsers} />
          <StatCard
            label="Tournament predictions"
            value={participation.usersWithTournamentPrediction}
            sub={`${pct(participation.usersWithTournamentPrediction, participation.totalUsers)}% of users`}
          />
          <StatCard
            label="Users with ≥1 group pred."
            value={participation.usersWithAnyGroupPrediction}
            sub={`${pct(participation.usersWithAnyGroupPrediction, participation.totalUsers)}% of users`}
          />
          <StatCard label="Group fixtures" value={participation.totalGroupFixtures} />
          <StatCard
            label="Group preds. submitted"
            value={participation.totalGroupPredictionsSubmitted}
            sub={`of ${participation.totalPossibleGroupPredictions} possible`}
          />
          <StatCard
            label="Group coverage"
            value={`${pct(participation.totalGroupPredictionsSubmitted, participation.totalPossibleGroupPredictions)}%`}
          />
        </div>

        {participation.submissionTimeline.length > 0 && (
          <div>
            <p className={`${labelCls} mb-2`}>Tournament prediction submission timeline</p>
            <div className="flex items-end gap-1 h-16">
              {(() => {
                const max = Math.max(1, ...participation.submissionTimeline.map(t => t.count));
                return participation.submissionTimeline.map(t => (
                  <div key={t.date} className="flex flex-col items-center gap-0.5 flex-1 min-w-0" title={`${t.date}: ${t.count}`}>
                    <div className="w-full rounded-t-sm" style={{ height: `${(t.count / max) * 52}px`, background: 'var(--secondary-fill)' }} />
                    <span className="text-[9px] text-fg-muted truncate w-full text-center">{t.date.slice(5)}</span>
                  </div>
                ));
              })()}
            </div>
          </div>
        )}
      </section>

      {/* ── Tournament Predictions ─────────────────────────────────────────── */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-5">
        <h2 className="font-display font-bold text-lg tracking-tight">Tournament Predictions</h2>

        {/* Champion picks */}
        <div>
          <p className={`${labelCls} mb-3`}>Champion picks</p>
          {tournamentPredictions.championPicks.length === 0
            ? <p className="text-sm text-fg-muted">No champion picks yet.</p>
            : (
              <div className="space-y-2">
                {tournamentPredictions.championPicks.map(pick => {
                  const max = tournamentPredictions.championPicks[0].count;
                  return (
                    <div key={pick.teamId} className="flex items-center gap-2">
                      <Flag teamId={pick.teamId} size={20} />
                      <span className="text-sm font-medium w-28 shrink-0 truncate">{pick.teamName}</span>
                      <div className="flex-1 h-5 rounded-sm overflow-hidden" style={{ background: 'var(--surface-2)' }}>
                        <div className="h-full rounded-sm transition-all"
                          style={{ width: `${(pick.count / max) * 100}%`, background: 'var(--secondary-fill)' }} />
                      </div>
                      <span className="text-sm font-semibold w-6 text-right shrink-0">{pick.count}</span>
                    </div>
                  );
                })}
              </div>
            )}
        </div>

        {/* Golden Six top picks */}
        <div>
          <p className={`${labelCls} mb-3`}>Most-picked Golden Six players (top 20)</p>
          {tournamentPredictions.topGoldenSixPicks.length === 0
            ? <p className="text-sm text-fg-muted">No Golden Six picks yet.</p>
            : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-border text-[10px] font-display font-bold uppercase tracking-wider text-fg-muted">
                      <th className="pb-2 text-left">#</th>
                      <th className="pb-2 text-left">Player</th>
                      <th className="pb-2 text-left">Team</th>
                      <th className="pb-2 text-left">Pos</th>
                      <th className="pb-2 text-right">Goals</th>
                      <th className="pb-2 text-right">Picks</th>
                      <th className="pb-2 w-24"></th>
                    </tr>
                  </thead>
                  <tbody>
                    {tournamentPredictions.topGoldenSixPicks.map((pick, i) => {
                      const max = tournamentPredictions.topGoldenSixPicks[0].pickCount;
                      return (
                        <tr key={pick.playerId.toString()} className="border-b border-border">
                          <td className="py-1.5 pr-2 text-fg-muted text-xs">{i + 1}</td>
                          <td className="py-1.5 pr-3 font-medium text-fg">{pick.playerName}</td>
                          <td className="py-1.5 pr-3">
                            <div className="flex items-center gap-1.5">
                              <Flag teamId={pick.teamId} size={16} />
                              <span className="text-fg-secondary text-xs">{pick.teamName}</span>
                            </div>
                          </td>
                          <td className="py-1.5 pr-3">
                            <span className="text-[11px] font-bold px-1.5 py-0.5 rounded"
                              style={{ background: POSITION_COLORS[pick.position] + '33', color: POSITION_COLORS[pick.position] }}>
                              {pick.position}
                            </span>
                          </td>
                          <td className="py-1.5 pr-3 text-right font-semibold">{pick.goals}</td>
                          <td className="py-1.5 text-right font-semibold">{pick.pickCount}</td>
                          <td className="py-1.5 pl-2">
                            <div className="h-2 rounded-sm overflow-hidden" style={{ background: 'var(--surface-2)' }}>
                              <div className="h-full rounded-sm"
                                style={{ width: `${(pick.pickCount / max) * 100}%`, background: POSITION_COLORS[pick.position] ?? 'var(--secondary-fill)' }} />
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
        </div>

        {/* Position breakdown + unique combos */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <p className={`${labelCls} mb-2`}>Golden Six pick breakdown by position</p>
            {(() => {
              const bp = tournamentPredictions.goldenSixByPosition;
              const total = bp.fwd + bp.mid + bp.def + bp.gk;
              return (
                <div className="space-y-2">
                  {([['FWD', bp.fwd], ['MID', bp.mid], ['DEF', bp.def], ['GK', bp.gk]] as [string, number][]).map(([pos, cnt]) => (
                    <div key={pos} className="flex items-center gap-2">
                      <span className="text-[11px] font-bold w-8 shrink-0" style={{ color: POSITION_COLORS[pos] }}>{pos}</span>
                      <div className="flex-1 h-4 rounded-sm overflow-hidden" style={{ background: 'var(--surface-2)' }}>
                        <div className="h-full rounded-sm" style={{ width: `${pct(cnt, Math.max(1, total))}%`, background: POSITION_COLORS[pos] }} />
                      </div>
                      <span className="text-xs text-fg-secondary w-16 text-right shrink-0">
                        {cnt} ({pct(cnt, Math.max(1, total))}%)
                      </span>
                    </div>
                  ))}
                </div>
              );
            })()}
          </div>
          <div className="flex flex-col justify-center">
            <StatCard
              label="Unique Golden Six combos"
              value={tournamentPredictions.uniqueGoldenSixCombos}
              sub="out of all tournament predictions"
            />
          </div>
        </div>
      </section>

      {/* ── Match Predictions ─────────────────────────────────────────────── */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-5">
        <div className="flex items-center justify-between">
          <h2 className="font-display font-bold text-lg tracking-tight">Match Predictions</h2>
          <span className="text-xs text-fg-muted">{groupFixtures.length} group fixtures</span>
        </div>

        {/* Outcome distribution per fixture */}
        <div>
          <p className={`${labelCls} mb-3`}>Outcome distribution (H = home win · D = draw · A = away win)</p>
          <div className="space-y-2">
            {pagedFixtures.map(f => {
              const total = f.totalPredictions;
              const hwPct  = pct(f.homeWinCount, total);
              const drPct  = pct(f.drawCount,    total);
              const awPct  = pct(f.awayWinCount, total);
              const hasResult = f.actualHomeScore != null && f.actualAwayScore != null;
              const actualOutcome = hasResult
                ? f.actualHomeScore! > f.actualAwayScore! ? 'H' : f.actualHomeScore! < f.actualAwayScore! ? 'A' : 'D'
                : null;
              const correctCount = actualOutcome === 'H' ? f.homeWinCount : actualOutcome === 'D' ? f.drawCount : actualOutcome === 'A' ? f.awayWinCount : null;
              const accuracyPct  = correctCount != null && total > 0 ? pct(correctCount, total) : null;
              return (
                <div key={f.fixtureId} className="rounded-input border border-border p-3 space-y-2"
                  style={{ background: 'var(--surface-2)' }}>
                  {/* Row 1: identity */}
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-[11px] font-mono font-bold px-1.5 py-0.5 rounded shrink-0"
                      style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }}>
                      M{f.matchNumber}
                    </span>
                    <span className="text-[11px] font-bold px-1.5 py-0.5 rounded shrink-0"
                      style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }}>
                      Grp {f.groupLetter}
                    </span>
                    <div className="flex items-center gap-1.5 text-sm font-medium">
                      <Flag teamId={f.homeTeamId} size={16} />
                      <span>{f.homeTeamName}</span>
                      <span className="text-fg-muted">vs</span>
                      <Flag teamId={f.awayTeamId} size={16} />
                      <span>{f.awayTeamName}</span>
                    </div>
                    <span className="text-xs text-fg-muted ml-auto shrink-0">{shortDate(f.kickoffUtc)}</span>
                  </div>
                  {/* Row 2: bar + labels */}
                  {total === 0
                    ? <p className="text-xs text-fg-muted">No predictions yet.</p>
                    : (
                      <div className="space-y-1">
                        <div className="flex h-5 rounded-sm overflow-hidden" style={{ background: 'var(--surface-3)' }}>
                          {hwPct > 0 && <div style={{ width: `${hwPct}%`, background: 'var(--win)' }} title={`Home win ${hwPct}%`} />}
                          {drPct > 0 && <div style={{ width: `${drPct}%`, background: 'var(--fg-muted)' }} title={`Draw ${drPct}%`} />}
                          {awPct > 0 && <div style={{ width: `${awPct}%`, background: 'var(--loss)' }} title={`Away win ${awPct}%`} />}
                        </div>
                        <div className="flex gap-3 text-[11px] flex-wrap">
                          <span style={{ color: 'var(--win)' }}>H {hwPct}% ({f.homeWinCount})</span>
                          <span className="text-fg-muted">D {drPct}% ({f.drawCount})</span>
                          <span style={{ color: 'var(--loss)' }}>A {awPct}% ({f.awayWinCount})</span>
                          {f.avgHomeScore != null && f.avgAwayScore != null && (
                            <span className="text-fg-secondary">avg {f.avgHomeScore}–{f.avgAwayScore}</span>
                          )}
                          <span className="text-fg-muted">{total} pred.</span>
                          {hasResult && (
                            <span className="ml-auto flex items-center gap-1.5">
                              <span className="font-mono font-bold" style={{ color: 'var(--fg)' }}>
                                {f.actualHomeScore}–{f.actualAwayScore}
                              </span>
                              {accuracyPct != null && (
                                <span style={{ color: accuracyPct >= 50 ? 'var(--win)' : 'var(--fg-muted)' }}>
                                  {accuracyPct}% correct
                                </span>
                              )}
                            </span>
                          )}
                        </div>
                      </div>
                    )}
                </div>
              );
            })}
          </div>

          {/* Pagination */}
          {totalGroupPages > 1 && (
            <div className="flex items-center justify-center gap-3 pt-3">
              <button
                onClick={() => setGroupPage(p => Math.max(0, p - 1))} disabled={groupPage === 0}
                className="px-3 py-1 text-sm rounded-input disabled:opacity-30"
                style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }}>
                ← Prev
              </button>
              <span className="text-sm text-fg-muted">{groupPage + 1} / {totalGroupPages}</span>
              <button
                onClick={() => setGroupPage(p => Math.min(totalGroupPages - 1, p + 1))} disabled={groupPage === totalGroupPages - 1}
                className="px-3 py-1 text-sm rounded-input disabled:opacity-30"
                style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }}>
                Next →
              </button>
            </div>
          )}
        </div>

        {/* Top scorelines */}
        <div>
          <p className={`${labelCls} mb-3`}>Most-predicted scorelines — group stage (90 min)</p>
          {groupPredictions.topScorelinesOverall.length === 0
            ? <p className="text-sm text-fg-muted">No predictions yet.</p>
            : (
              <div className="flex flex-wrap gap-2">
                {groupPredictions.topScorelinesOverall.map((s, i) => (
                  <div key={i} className="rounded-input px-3 py-1.5 flex items-center gap-2"
                    style={{ background: 'var(--surface-2)' }}>
                    <span className="font-mono font-bold text-sm text-fg">{s.homeScore}–{s.awayScore}</span>
                    <span className="text-xs text-fg-muted">{s.count}×</span>
                  </div>
                ))}
              </div>
            )}
        </div>

        {/* Knockout scorelines */}
        {groupPredictions.topKnockoutScorelinesOverall.length > 0 && (
          <div>
            <p className={`${labelCls} mb-3`}>Most-predicted scorelines — knockout stage (90 min)</p>
            <div className="flex flex-wrap gap-2">
              {groupPredictions.topKnockoutScorelinesOverall.map((s, i) => (
                <div key={i} className="rounded-input px-3 py-1.5 flex items-center gap-2"
                  style={{ background: 'var(--surface-2)' }}>
                  <span className="font-mono font-bold text-sm text-fg">{s.homeScore}–{s.awayScore}</span>
                  <span className="text-xs text-fg-muted">{s.count}×</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </section>

      {/* ── Knockout Predictions ───────────────────────────────────────────── */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-5">
        <h2 className="font-display font-bold text-lg tracking-tight">Knockout Predictions</h2>

        {roundOrder.map(round => {
          const slots = slotsByRound[round];
          if (!slots?.length) return null;
          return (
            <div key={round}>
              <p className={`${labelCls} mb-3`}>{ROUND_LABELS[round] ?? round}</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {slots.map(slot => {
                  const maxCount = Math.max(1, ...slot.teamCounts.map(t => t.count));
                  const unresolved = !slot.homeTeamId && !slot.awayTeamId;
                  return (
                    <div key={slot.slotKey} className="rounded-input border border-border p-3 space-y-2"
                      style={{ background: 'var(--surface-2)' }}>
                      {/* Slot header */}
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2 text-sm">
                          {slot.homeTeamId
                            ? <><Flag teamId={slot.homeTeamId} size={16} /><span className="font-medium">{slot.homeTeamName}</span></>
                            : <span className="text-fg-muted italic text-xs">TBD</span>}
                          <span className="text-fg-muted">vs</span>
                          {slot.awayTeamId
                            ? <><Flag teamId={slot.awayTeamId} size={16} /><span className="font-medium">{slot.awayTeamName}</span></>
                            : <span className="text-fg-muted italic text-xs">TBD</span>}
                        </div>
                        <span className="text-[10px] text-fg-muted font-mono">{slot.slotKey}</span>
                      </div>
                      {/* Picks */}
                      {slot.totalPredictions === 0 || unresolved
                        ? <p className="text-xs text-fg-muted">{unresolved ? 'Teams not yet resolved.' : 'No predictions yet.'}</p>
                        : (
                          <div className="space-y-1.5">
                            {slot.teamCounts.map(tc => (
                              <div key={tc.teamId} className="flex items-center gap-2">
                                <Flag teamId={tc.teamId} size={14} />
                                <span className="text-xs font-medium w-20 truncate shrink-0">{tc.teamName}</span>
                                <div className="flex-1 h-3 rounded-sm overflow-hidden" style={{ background: 'var(--surface-3)' }}>
                                  <div className="h-full rounded-sm"
                                    style={{ width: `${(tc.count / maxCount) * 100}%`, background: tc.teamId === slot.actualWinnerTeamId ? 'var(--win)' : 'var(--secondary-fill)' }} />
                                </div>
                                <span className="text-xs text-fg-secondary w-6 text-right shrink-0">{tc.count}</span>
                                <span className="text-[10px] text-fg-muted w-8 text-right shrink-0">
                                  {pct(tc.count, slot.totalPredictions)}%
                                </span>
                              </div>
                            ))}
                            <div className="flex items-center justify-between flex-wrap gap-1">
                              <p className="text-[10px] text-fg-muted">{slot.totalPredictions} prediction{slot.totalPredictions !== 1 ? 's' : ''}</p>
                              <div className="flex items-center gap-2">
                                {slot.avgPredictedHomeScore != null && slot.avgPredictedAwayScore != null && (
                                  <p className="text-[10px] text-fg-secondary">avg {slot.avgPredictedHomeScore}–{slot.avgPredictedAwayScore}</p>
                                )}
                                {slot.actualWinnerTeamId && slot.actualHomeScore != null && slot.actualAwayScore != null && (
                                  <p className="text-[10px] font-mono font-bold" style={{ color: 'var(--win)' }}>
                                    {slot.actualHomeScore}–{slot.actualAwayScore}
                                  </p>
                                )}
                              </div>
                            </div>
                          </div>
                        )}
                    </div>
                  );
                })}
              </div>
            </div>
          );
        })}

        {/* Predicted finalist pairs */}
        {knockoutPredictions.finalistPairs.length > 0 && (
          <div>
            <p className={`${labelCls} mb-3`}>Most-predicted finalist pairs (from SF picks)</p>
            <div className="space-y-2">
              {knockoutPredictions.finalistPairs.map((pair, i) => {
                const max = knockoutPredictions.finalistPairs[0].count;
                return (
                  <div key={i} className="flex items-center gap-2">
                    <div className="flex items-center gap-1.5 w-52 shrink-0 text-sm">
                      <Flag teamId={pair.team1Id} size={16} />
                      <span className="font-medium truncate">{pair.team1Name}</span>
                      <span className="text-fg-muted text-xs">vs</span>
                      <Flag teamId={pair.team2Id} size={16} />
                      <span className="font-medium truncate">{pair.team2Name}</span>
                    </div>
                    <div className="flex-1 h-4 rounded-sm overflow-hidden" style={{ background: 'var(--surface-2)' }}>
                      <div className="h-full rounded-sm" style={{ width: `${(pair.count / max) * 100}%`, background: 'var(--secondary-fill)' }} />
                    </div>
                    <span className="text-sm font-semibold w-6 text-right shrink-0">{pair.count}</span>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {knockoutPredictions.slotDistributions.length === 0 && (
          <p className="text-sm text-fg-muted">No knockout predictions yet.</p>
        )}
      </section>

      {/* ── Score Distribution ─────────────────────────────────────────────── */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-5">
        <h2 className="font-display font-bold text-lg tracking-tight">Score Distribution</h2>

        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <StatCard label="Avg total points"     value={scores.avgTotal} />
          <StatCard label="Avg group points"     value={scores.avgGroupPoints} />
          <StatCard label="Avg knockout points"  value={scores.avgKnockoutPoints} />
          <StatCard label="Avg champion points"  value={scores.avgChampionPoints} />
          <StatCard label="Avg Golden Six pts"   value={scores.avgGoldenSixPoints} />
          <StatCard label="Avg exact scorelines" value={scores.avgExactScorelinesCount} />
          <StatCard label="Avg correct outcomes" value={scores.avgCorrectOutcomeCount} />
          <StatCard label="Top score"            value={scores.maxTotalPoints} />
          <StatCard label="Lowest score"         value={scores.minTotalPoints} />
        </div>

        {scores.histogram.some(b => b.count > 0) && (
          <div>
            <p className={`${labelCls} mb-3`}>Points histogram</p>
            <div className="flex items-end gap-1.5 h-28">
              {scores.histogram.map(bucket => (
                <div key={bucket.label} className="flex flex-col items-center gap-1 flex-1 min-w-0" title={`${bucket.label}: ${bucket.count} user(s)`}>
                  <span className="text-[9px] text-fg-secondary">{bucket.count > 0 ? bucket.count : ''}</span>
                  <div className="w-full rounded-t-sm transition-all"
                    style={{ height: `${(bucket.count / maxHistogramCount) * 72}px`, background: 'var(--secondary-fill)', minHeight: bucket.count > 0 ? 4 : 0 }} />
                  <span className="text-[9px] text-fg-muted truncate w-full text-center">{bucket.label}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </section>

    </div>
  );
}
