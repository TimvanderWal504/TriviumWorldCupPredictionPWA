import { useEffect, useRef, useState } from 'react';
import { ChevronLeft, ChevronRight, Clock, ChevronDown, ChevronUp, Trophy } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';
import { Spinner } from '../components/ui/Spinner.tsx';
import {
  buildEventItems, buildRenderList, MatchEventsList,
  type GoalEventDto, type CardEventDto, type SubstitutionEventDto, type VarEventDto,
} from '../components/MatchEvents.tsx';
import { GroupStandingsTable, type StandingsMatchInput } from '../components/GroupStandingsTable.tsx';

interface ResultFixtureDto {
  id: string;
  matchNumber: number;
  groupLetter: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  kickoffUtc: string;
  homeScore: number | null;
  awayScore: number | null;
  venue: string;
  city: string;
  status: string;
}

type FixtureDto = ResultFixtureDto;

interface MyPredictionDto {
  fixtureId: string;
  predictedHome: number;
  predictedAway: number;
  points: number;
}

interface ResultsResponse {
  fixtures: ResultFixtureDto[];
  goals: GoalEventDto[];
  cards: CardEventDto[];
  substitutions: SubstitutionEventDto[];
  varEvents: VarEventDto[];
  myPredictions: MyPredictionDto[];
}

interface MyKnockoutPredictionDto {
  predictedWinnerTeamId: string;
  predictedHomeScore: number | null;
  predictedAwayScore: number | null;
  points: number;
}

interface KnockoutSlotResultDto {
  slotKey: string;
  round: string;
  slotNumber: number;
  homeTeamId: string | null;
  homeTeamName: string | null;
  awayTeamId: string | null;
  awayTeamName: string | null;
  kickoffUtc: string | null;
  venue: string | null;
  city: string | null;
  status: string;
  homeScore: number | null;
  awayScore: number | null;
  penaltyHomeScore: number | null;
  penaltyAwayScore: number | null;
  winnerTeamId: string | null;
  myPrediction: MyKnockoutPredictionDto | null;
}

async function fetchResults(): Promise<ResultsResponse> {
  const res = await fetch('/fixtures/results', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load results (${res.status})`);
  return res.json() as Promise<ResultsResponse>;
}

async function fetchAllFixtures(): Promise<FixtureDto[]> {
  const res = await fetch('/fixtures', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load fixtures (${res.status})`);
  return res.json() as Promise<FixtureDto[]>;
}

async function fetchKnockoutResults(): Promise<KnockoutSlotResultDto[]> {
  const res = await fetch('/knockout-slots/results', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load knockout results (${res.status})`);
  return res.json() as Promise<KnockoutSlotResultDto[]>;
}

function formatKickoff(kickoffUtc: string): string {
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday: 'short', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

function pointsLabel(points: number): string {
  if (points === 10) return 'Exact score';
  if (points === 7) return 'Correct diff';
  if (points === 4) return 'Right outcome +1';
  if (points === 3) return 'Right outcome';
  if (points === 1) return 'One tally';
  return 'Wrong';
}

function pointsStyle(points: number): { background: string; color: string } {
  if (points === 10) return { background: 'var(--win-soft)', color: 'var(--win)' };
  if (points === 7) return { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' };
  if (points >= 3) return { background: 'rgba(242,193,78,.18)', color: 'var(--accent)' };
  if (points === 1) return { background: 'var(--surface-3)', color: 'var(--fg-secondary)' };
  return { background: 'var(--live-soft)', color: 'var(--loss)' };
}

function roundLabel(round: string): string {
  switch (round) {
    case 'R32':        return 'Round of 32';
    case 'R16':        return 'Round of 16';
    case 'QF':         return 'Quarter-Final';
    case 'SF':         return 'Semi-Final';
    case 'ThirdPlace': return '3rd Place';
    case 'Final':      return 'Final';
    default:           return round;
  }
}

// ── Group-stage result card ───────────────────────────────────────────────────

interface ResultCardProps {
  fixture: ResultFixtureDto;
  goals: GoalEventDto[];
  cards: CardEventDto[];
  substitutions: SubstitutionEventDto[];
  varEvents: VarEventDto[];
  prediction: MyPredictionDto | undefined;
}

function ResultFixtureCard({ fixture, goals, cards, substitutions, varEvents, prediction }: ResultCardProps) {
  const isCancelled = fixture.status === 'Cancelled';
  const homeLead = !isCancelled && (fixture.homeScore ?? 0) > (fixture.awayScore ?? 0);
  const awayLead = !isCancelled && (fixture.awayScore ?? 0) > (fixture.homeScore ?? 0);
  const [eventsOpen, setEventsOpen] = useState(false);

  const events = buildEventItems(fixture.id, goals, cards, substitutions, varEvents);
  const renderItems = buildRenderList(events, 'Completed');

  return (
    <div className="rounded-card bg-surface p-4 flex flex-col gap-2.5 border" style={{ borderColor: 'var(--border)' }}>
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">Group {fixture.groupLetter} · {fixture.venue}</span>
        {isCancelled
          ? <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md"
                  style={{ background: 'var(--live-soft)', color: 'var(--loss)' }}>Cancelled</span>
          : <span className="font-display font-bold text-[11px] px-2 py-0.5 rounded-md bg-surface-3 text-fg-secondary">FT</span>}
      </div>

      <div className="flex items-center gap-1.5 text-[11px] text-fg-muted">
        <Clock size={12} />{formatKickoff(fixture.kickoffUtc)}
      </div>

      <div className="flex flex-col gap-2">
        {[
          { id: fixture.homeTeamId, name: fixture.homeTeamName, score: fixture.homeScore, lead: homeLead },
          { id: fixture.awayTeamId, name: fixture.awayTeamName, score: fixture.awayScore, lead: awayLead },
        ].map(({ id, name, score, lead }) => (
          <div key={name} className="flex items-center gap-2.5">
            {flagUrl(id) && (
              <img src={flagUrl(id)} alt="" width={22} height={15} className="flag shrink-0" />
            )}
            <span className={`flex-1 min-w-0 truncate font-semibold ${lead ? 'text-fg font-bold' : 'text-fg-secondary'}`}>
              {name}
            </span>
            {!isCancelled && (
              <span className={`font-display font-black text-[22px] tnum ${lead ? 'text-fg' : 'text-fg-muted'}`}>
                {score}
              </span>
            )}
          </div>
        ))}
      </div>

      {renderItems.length > 0 && (
        <div className="pt-2.5 border-t border-border">
          <button
            onClick={() => setEventsOpen(o => !o)}
            className="flex items-center gap-1.5 text-[11px] text-fg-muted w-full hover:text-fg transition-colors"
          >
            {eventsOpen ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
            <span>{eventsOpen ? 'Hide' : 'Show'} match events</span>
          </button>
          {eventsOpen && (
            <div className="mt-2">
              <MatchEventsList renderItems={renderItems} />
            </div>
          )}
        </div>
      )}

      <div className="pt-2.5 border-t border-border">
        {isCancelled ? (
          <p className="text-[12px] text-fg-muted italic">Match cancelled — no points awarded</p>
        ) : prediction !== undefined ? (
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-1.5 text-[12px]">
              <span className="text-fg-muted">Your pick:</span>
              <span className="font-display font-bold tnum text-fg">
                {prediction.predictedHome}–{prediction.predictedAway}
              </span>
            </div>
            <div className="flex items-center gap-2">
              <span
                className="text-[10px] font-extrabold px-2 py-0.5 rounded-md"
                style={pointsStyle(prediction.points)}
              >
                {pointsLabel(prediction.points)}
              </span>
              <span
                className="font-display font-black text-[15px] tnum"
                style={{ color: prediction.points > 0 ? 'var(--primary)' : 'var(--fg-muted)' }}
              >
                +{prediction.points}
              </span>
            </div>
          </div>
        ) : (
          <p className="text-[12px] text-fg-muted italic">No prediction submitted</p>
        )}
      </div>
    </div>
  );
}

// ── Knockout result card ──────────────────────────────────────────────────────

function KnockoutResultCard({ slot }: { slot: KnockoutSlotResultDto }) {
  const homeLead = (slot.homeScore ?? 0) > (slot.awayScore ?? 0);
  const awayLead = (slot.awayScore ?? 0) > (slot.homeScore ?? 0);
  const hasPenalties = slot.penaltyHomeScore !== null || slot.penaltyAwayScore !== null;

  const pred = slot.myPrediction;
  const correctWinner = pred !== null && pred.predictedWinnerTeamId === slot.winnerTeamId;

  return (
    <div className="rounded-card bg-surface p-4 flex flex-col gap-2.5 border" style={{ borderColor: 'var(--border)' }}>
      {/* Header */}
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">{roundLabel(slot.round)}{slot.venue ? ` · ${slot.venue}` : ''}</span>
        <span className="font-display font-bold text-[11px] px-2 py-0.5 rounded-md bg-surface-3 text-fg-secondary">FT</span>
      </div>

      {slot.kickoffUtc && (
        <div className="flex items-center gap-1.5 text-[11px] text-fg-muted">
          <Clock size={12} />{formatKickoff(slot.kickoffUtc)}
        </div>
      )}

      {/* Scores */}
      <div className="flex flex-col gap-2">
        {[
          { id: slot.homeTeamId, name: slot.homeTeamName, score: slot.homeScore, penScore: slot.penaltyHomeScore, lead: homeLead },
          { id: slot.awayTeamId, name: slot.awayTeamName, score: slot.awayScore, penScore: slot.penaltyAwayScore, lead: awayLead },
        ].map(({ id, name, score, penScore, lead }) => (
          <div key={id ?? name} className="flex items-center gap-2.5">
            {id && flagUrl(id) && (
              <img src={flagUrl(id)} alt="" width={22} height={15} className="flag shrink-0" />
            )}
            <span className={`flex-1 min-w-0 truncate font-semibold ${lead ? 'text-fg font-bold' : 'text-fg-secondary'}`}>
              {name ?? id}
            </span>
            <div className="flex items-center gap-1.5">
              <span className={`font-display font-black text-[22px] tnum ${lead ? 'text-fg' : 'text-fg-muted'}`}>
                {score ?? '–'}
              </span>
              {hasPenalties && (
                <span className="text-[11px] text-fg-muted tnum">({penScore ?? 0})</span>
              )}
            </div>
          </div>
        ))}
      </div>

      {hasPenalties && (
        <p className="text-[11px] text-fg-muted">Decided on penalties</p>
      )}

      {/* Winner badge */}
      {slot.winnerTeamId && (
        <div className="flex items-center gap-1.5 text-[11px]" style={{ color: 'var(--win)' }}>
          <Trophy size={12} />
          <span className="font-semibold">
            {slot.winnerTeamId === slot.homeTeamId ? slot.homeTeamName : slot.awayTeamName} advance
          </span>
        </div>
      )}

      {/* User prediction + points */}
      <div className="pt-2.5 border-t border-border">
        {pred !== null ? (
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-1.5 text-[12px]">
                <span className="text-fg-muted">Your pick:</span>
                {pred.predictedWinnerTeamId === slot.homeTeamId
                  ? (slot.homeTeamId && flagUrl(slot.homeTeamId) && <img src={flagUrl(slot.homeTeamId)} alt="" width={16} height={11} className="flag shrink-0" />)
                  : (slot.awayTeamId && flagUrl(slot.awayTeamId) && <img src={flagUrl(slot.awayTeamId)} alt="" width={16} height={11} className="flag shrink-0" />)
                }
                <span className="font-semibold text-fg">
                  {pred.predictedWinnerTeamId === slot.homeTeamId ? slot.homeTeamName : slot.awayTeamName}
                </span>
                {pred.predictedHomeScore !== null && pred.predictedAwayScore !== null && (
                  <span className="text-fg-muted font-display tnum">
                    ({pred.predictedHomeScore}–{pred.predictedAwayScore})
                  </span>
                )}
              </div>
              <div className="flex items-center gap-2">
                <span
                  className="text-[10px] font-extrabold px-2 py-0.5 rounded-md"
                  style={correctWinner
                    ? { background: 'var(--win-soft)', color: 'var(--win)' }
                    : { background: 'var(--live-soft)', color: 'var(--loss)' }}
                >
                  {correctWinner ? 'Correct' : 'Wrong'}
                </span>
                <span
                  className="font-display font-black text-[15px] tnum"
                  style={{ color: pred.points > 0 ? 'var(--primary)' : 'var(--fg-muted)' }}
                >
                  +{pred.points}
                </span>
              </div>
            </div>
          </div>
        ) : (
          <p className="text-[12px] text-fg-muted italic">No prediction submitted</p>
        )}
      </div>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

interface ResultsPageProps {
  tab: 'group-stage' | 'knockout' | 'by-date';
}

export function ResultsPage({ tab }: ResultsPageProps) {
  const [data, setData] = useState<ResultsResponse | null>(null);
  const [allFixtures, setAllFixtures] = useState<FixtureDto[]>([]);
  const [knockoutResults, setKnockoutResults] = useState<KnockoutSlotResultDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeGroup, setActiveGroup] = useState<string>('A');

  const tabsRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    Promise.all([fetchResults(), fetchAllFixtures(), fetchKnockoutResults()])
      .then(([res, fixtureList, knockoutList]) => {
        setData(res);
        setAllFixtures(fixtureList);
        setKnockoutResults(knockoutList);
        if (fixtureList.length > 0) {
          const letters = [...new Set(fixtureList.map(f => f.groupLetter))].sort();
          setActiveGroup(letters[0]);
        }
      })
      .catch((err: unknown) => setLoadError(err instanceof Error ? err.message : 'Failed to load results.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!tabsRef.current) return;
    const btn = tabsRef.current.querySelector('[aria-selected="true"]') as HTMLElement | null;
    btn?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
  }, [activeGroup]);

  if (loading) return (
    <div className="flex items-center justify-center py-20">
      <Spinner size="lg" label="Loading results" />
    </div>
  );
  if (loadError) return (
    <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>
      {loadError}
    </div>
  );

  const playedFixtures = data?.fixtures ?? [];
  const predMap = new Map((data?.myPredictions ?? []).map(p => [p.fixtureId, p]));

  // ── Knockouts tab ─────────────────────────────────────────────────────────
  if (tab === 'knockout') {
    if (knockoutResults.length === 0) {
      return (
        <div className="max-w-3xl mx-auto px-4 py-4">
          <div className="rounded-card bg-surface border border-border px-4 py-16 text-center">
            <p className="text-fg-muted text-sm">No knockout results yet.</p>
          </div>
        </div>
      );
    }

    const roundOrder = ['R32', 'R16', 'QF', 'SF', 'ThirdPlace', 'Final'];
    const slotsByRound = new Map<string, KnockoutSlotResultDto[]>();
    for (const slot of knockoutResults) {
      const arr = slotsByRound.get(slot.round) ?? [];
      arr.push(slot);
      slotsByRound.set(slot.round, arr);
    }
    const rounds = roundOrder.filter(r => slotsByRound.has(r));

    return (
      <div className="max-w-3xl mx-auto px-4 py-4 flex flex-col gap-6">
        {rounds.map(round => (
          <div key={round} className="flex flex-col gap-2.5">
            <h2 className="text-[13px] font-bold text-fg-muted uppercase tracking-wider px-0.5">
              {roundLabel(round)}
            </h2>
            {(slotsByRound.get(round) ?? []).map(slot => (
              <KnockoutResultCard key={slot.slotKey} slot={slot} />
            ))}
          </div>
        ))}
      </div>
    );
  }

  // ── By Date tab — all results merged ─────────────────────────────────────
  if (tab === 'by-date') {
    type DateItem =
      | { kind: 'group'; fixture: ResultFixtureDto }
      | { kind: 'knockout'; slot: KnockoutSlotResultDto };

    const allItems: DateItem[] = [
      ...playedFixtures.map(f => ({ kind: 'group' as const, fixture: f })),
      ...knockoutResults.map(s => ({ kind: 'knockout' as const, slot: s })),
    ].sort((a, b) => {
      const at = a.kind === 'group' ? new Date(a.fixture.kickoffUtc).getTime() : (a.slot.kickoffUtc ? new Date(a.slot.kickoffUtc).getTime() : 0);
      const bt = b.kind === 'group' ? new Date(b.fixture.kickoffUtc).getTime() : (b.slot.kickoffUtc ? new Date(b.slot.kickoffUtc).getTime() : 0);
      return bt - at;
    });

    if (allItems.length === 0) {
      return (
        <div className="max-w-3xl mx-auto px-4 py-4">
          <div className="rounded-card bg-surface border border-border px-4 py-16 text-center">
            <p className="text-fg-muted text-sm">No results yet. Come back after the first match.</p>
          </div>
        </div>
      );
    }

    return (
      <div className="max-w-3xl mx-auto px-4 py-4 flex flex-col gap-3">
        {allItems.map(item =>
          item.kind === 'group'
            ? <ResultFixtureCard
                key={item.fixture.id}
                fixture={item.fixture}
                goals={data?.goals ?? []}
                cards={data?.cards ?? []}
                substitutions={data?.substitutions ?? []}
                varEvents={data?.varEvents ?? []}
                prediction={predMap.get(item.fixture.id)}
              />
            : <KnockoutResultCard key={item.slot.slotKey} slot={item.slot} />
        )}
      </div>
    );
  }

  // ── Group Stage tab — by-group view ──────────────────────────────────────
  if (allFixtures.length === 0) {
    return (
      <div className="max-w-3xl mx-auto px-4 py-4">
        <div className="rounded-card bg-surface border border-border px-4 py-16 text-center">
          <p className="text-fg-muted text-sm">No results yet. Come back after the first match.</p>
        </div>
      </div>
    );
  }

  const groupLetters = [...new Set(allFixtures.map(f => f.groupLetter))].sort();
  const activeGroupFixtures = allFixtures.filter(f => f.groupLetter === activeGroup);
  const activePlayedFixtures = playedFixtures.filter(f => f.groupLetter === activeGroup);

  const activeGroupStandingsMatches: StandingsMatchInput[] = activeGroupFixtures.map(f => ({
    homeTeamId: f.homeTeamId, homeTeamName: f.homeTeamName,
    awayTeamId: f.awayTeamId, awayTeamName: f.awayTeamName,
    homeScore: f.status === 'Cancelled' ? null : f.homeScore,
    awayScore: f.status === 'Cancelled' ? null : f.awayScore,
  }));

  const btnBase = 'w-7 h-7 flex items-center justify-center rounded-input text-sm font-bold transition-opacity disabled:opacity-25';

  return (
    <div className="max-w-3xl mx-auto px-4 py-4">
      <div ref={tabsRef} className="flex gap-1.5 overflow-x-auto appscroll pb-1.5" role="tablist">
        {groupLetters.map(letter => (
          <button
            key={letter} role="tab" aria-selected={activeGroup === letter}
            onClick={() => setActiveGroup(letter)}
            className={`px-3.5 py-1.5 rounded-input text-[13px] font-semibold whitespace-nowrap transition-colors ${
              activeGroup !== letter ? 'bg-surface-3 text-fg-secondary' : ''
            }`}
            style={activeGroup === letter
              ? { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }
              : undefined}
          >
            Group {letter}
          </button>
        ))}
      </div>

      {groupLetters.length > 1 && (() => {
        const n = groupLetters.length;
        const activeIdx = groupLetters.indexOf(activeGroup);
        const windowSize = Math.min(5, n);
        const windowStart = Math.max(0, Math.min(n - windowSize, activeIdx - Math.floor(windowSize / 2)));
        const visibleLetters = groupLetters.slice(windowStart, windowStart + windowSize);
        return (
          <div className="flex items-center justify-center gap-3 mb-3 mt-1.5">
            <button onClick={() => setActiveGroup(groupLetters[activeIdx - 1])} disabled={activeIdx === 0} className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }} aria-label="Previous group">
              <ChevronLeft size={16} />
            </button>
            <div className="flex items-center gap-2.5">
              {visibleLetters.map(letter => {
                const active = activeGroup === letter;
                return (
                  <button key={letter} onClick={() => setActiveGroup(letter)} aria-label={`Group ${letter}`} className="flex flex-col items-center gap-0.5">
                    <div className="rounded-full transition-all duration-150" style={{ width: active ? 10 : 7, height: active ? 10 : 7, background: active ? 'var(--secondary)' : 'var(--surface-3)' }} />
                    <span className="text-[9px] font-mono leading-none" style={{ color: active ? 'var(--secondary)' : 'var(--fg-muted)' }}>{letter}</span>
                  </button>
                );
              })}
            </div>
            <button onClick={() => setActiveGroup(groupLetters[activeIdx + 1])} disabled={activeIdx === n - 1} className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }} aria-label="Next group">
              <ChevronRight size={16} />
            </button>
          </div>
        );
      })()}

      {activePlayedFixtures.length === 0 ? (
        <p className="text-fg-muted text-[13px] text-center py-6">No matches played yet in this group.</p>
      ) : (
        <div className="flex flex-col gap-2.5">
          {activePlayedFixtures.map(f => (
            <ResultFixtureCard
              key={f.id}
              fixture={f}
              goals={data?.goals ?? []}
              cards={data?.cards ?? []}
              substitutions={data?.substitutions ?? []}
              varEvents={data?.varEvents ?? []}
              prediction={predMap.get(f.id)}
            />
          ))}
        </div>
      )}

      <GroupStandingsTable matches={activeGroupStandingsMatches} />
    </div>
  );
}
