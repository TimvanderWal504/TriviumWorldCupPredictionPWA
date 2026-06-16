import { useEffect, useState } from 'react';
import { Clock, ChevronDown, ChevronUp } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';
import {
  buildEventItems, buildRenderList, MatchEventsList,
  type GoalEventDto, type CardEventDto, type SubstitutionEventDto, type VarEventDto,
} from '../components/MatchEvents.tsx';

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

export function ResultsPage() {
  const [data, setData] = useState<ResultsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    fetchResults()
      .then(setData)
      .catch((err: unknown) => setLoadError(err instanceof Error ? err.message : 'Failed to load results.'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex items-center justify-center py-20 text-fg-muted">Loading results…</div>;
  if (loadError) return (
    <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>
      {loadError}
    </div>
  );

  const fixtures = data?.fixtures ?? [];
  const predMap = new Map((data?.myPredictions ?? []).map(p => [p.fixtureId, p]));

  if (fixtures.length === 0) {
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
      {fixtures.map(f => (
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
