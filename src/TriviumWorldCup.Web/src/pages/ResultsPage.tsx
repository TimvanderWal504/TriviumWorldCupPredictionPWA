import { useEffect, useRef, useState } from 'react';
import { ChevronLeft, ChevronRight, Clock, ChevronDown, ChevronUp } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';
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
      {/* Header */}
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">Group {fixture.groupLetter} · {fixture.venue}</span>
        {isCancelled
          ? <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md"
                  style={{ background: 'var(--live-soft)', color: 'var(--loss)' }}>Cancelled</span>
          : <span className="font-display font-bold text-[11px] px-2 py-0.5 rounded-md bg-surface-3 text-fg-secondary">FT</span>}
      </div>

      {/* Kickoff time */}
      <div className="flex items-center gap-1.5 text-[11px] text-fg-muted">
        <Clock size={12} />{formatKickoff(fixture.kickoffUtc)}
      </div>

      {/* Team rows + scores */}
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

      {/* User prediction + points */}
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

interface ResultsPageProps {
  viewMode: 'group' | 'date';
}

export function ResultsPage({ viewMode }: ResultsPageProps) {
  const [data, setData] = useState<ResultsResponse | null>(null);
  const [allFixtures, setAllFixtures] = useState<FixtureDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeGroup, setActiveGroup] = useState<string>('A');

  const tabsRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    Promise.all([fetchResults(), fetchAllFixtures()])
      .then(([res, fixtureList]) => {
        setData(res);
        setAllFixtures(fixtureList);
        if (fixtureList.length > 0) {
          const letters = [...new Set(fixtureList.map(f => f.groupLetter))].sort();
          setActiveGroup(letters[0]);
        }
      })
      .catch((err: unknown) => setLoadError(err instanceof Error ? err.message : 'Failed to load results.'))
      .finally(() => setLoading(false));
  }, []);

  // Scroll the active tab into view whenever activeGroup changes
  useEffect(() => {
    if (!tabsRef.current) return;
    const btn = tabsRef.current.querySelector('[aria-selected="true"]') as HTMLElement | null;
    btn?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
  }, [activeGroup]);

  if (loading) return <div className="flex items-center justify-center py-20 text-fg-muted">Loading results…</div>;
  if (loadError) return (
    <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>
      {loadError}
    </div>
  );

  const playedFixtures = data?.fixtures ?? [];
  const predMap = new Map((data?.myPredictions ?? []).map(p => [p.fixtureId, p]));

  if (viewMode === 'date') {
    if (playedFixtures.length === 0) {
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
        {playedFixtures.map(f => (
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
    );
  }

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
      {/* Group tab bar */}
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

      {/* Group navigator: prev/next buttons + 5-dot window */}
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
