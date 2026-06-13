import { useEffect, useState } from 'react';
import { Clock, ChevronDown, ChevronUp } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';

interface ResultFixtureDto {
  id: string;
  matchNumber: number;
  groupLetter: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  kickoffUtc: string;
  homeScore: number;
  awayScore: number;
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
  extraMinute: number | null;
}

interface CardEventDto {
  fixtureId: string;
  playerId: string;
  playerName: string;
  teamId: string;
  type: string;
  minute: number;
  extraMinute: number | null;
}

interface SubstitutionEventDto {
  fixtureId: string;
  playerInName: string;
  playerOutName: string;
  teamId: string;
  minute: number;
  extraMinute: number | null;
}

interface MyPredictionDto {
  fixtureId: string;
  predictedHome: number;
  predictedAway: number;
  points: number;
}

interface VarEventDto {
  fixtureId: string;
  playerName: string;
  teamId: string;
  type: string;
  minute: number;
  extraMinute: number | null;
}

interface ResultsResponse {
  fixtures: ResultFixtureDto[];
  goals: GoalEventDto[];
  cards: CardEventDto[];
  substitutions: SubstitutionEventDto[];
  varEvents: VarEventDto[];
  myPredictions: MyPredictionDto[];
}

function formatMinute(minute: number, extra: number | null): string {
  return extra ? `${minute}+${extra}'` : `${minute}'`;
}

function GoalIcon() {
  return <span className="text-[11px] leading-none inline-flex w-2 justify-center shrink-0">⚽</span>;
}

function CardIcon({ type }: { type: string }) {
  if (type === 'SecondYellow') {
    return (
      <span className="inline-flex items-center gap-0.5">
        <span className="inline-block w-2 h-3 rounded-[2px]" style={{ background: '#f5c518' }} />
        <span className="inline-block w-2 h-3 rounded-[2px]" style={{ background: '#ef4444' }} />
      </span>
    );
  }
  if (type === 'Red') {
    return <span className="inline-block w-2 h-3 rounded-[2px]" style={{ background: '#ef4444' }} />;
  }
  return <span className="inline-block w-2 h-3 rounded-[2px]" style={{ background: '#f5c518' }} />;
}

type EventItem =
  | { kind: 'goal'; minute: number; extraMinute: number | null; playerName: string; teamId: string; type: string }
  | { kind: 'card'; minute: number; extraMinute: number | null; playerName: string; teamId: string; type: string }
  | { kind: 'sub';  minute: number; extraMinute: number | null; playerInName: string; playerOutName: string; teamId: string }
  | { kind: 'var';  minute: number; extraMinute: number | null; playerName: string; teamId: string; type: string };

function varLabel(type: string): string {
  if (type === 'GoalCancelled') return 'Goal ruled out';
  if (type === 'CardUpgradeRed') return 'Red card upgrade';
  return '2nd yellow upgrade';
}

type RenderItem = EventItem | { kind: 'marker'; label: string };

const PERIOD_THRESHOLDS: { minute: number; label: string }[] = [
  { minute: 45, label: 'HT' },
  { minute: 90, label: "90'" },
  { minute: 105, label: 'ET HT' },
];

function buildRenderList(events: EventItem[], status: string): RenderItem[] {
  const sorted = [...events].sort((a, b) =>
    a.minute !== b.minute ? a.minute - b.minute : (a.extraMinute ?? 0) - (b.extraMinute ?? 0)
  );
  const result: RenderItem[] = [];
  if (sorted.length > 0) result.push({ kind: 'marker', label: 'Kick-off' });
  let tIdx = 0;
  for (const evt of sorted) {
    while (tIdx < PERIOD_THRESHOLDS.length && evt.minute > PERIOD_THRESHOLDS[tIdx].minute) {
      result.push({ kind: 'marker', label: PERIOD_THRESHOLDS[tIdx].label });
      tIdx++;
    }
    result.push(evt);
  }
  if (status === 'Completed' && sorted.length > 0) {
    const maxMinute = Math.max(...sorted.map(e => e.minute));
    result.push({ kind: 'marker', label: maxMinute > 90 ? 'AET' : 'FT' });
  }
  return result;
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
  const homeLead = fixture.homeScore > fixture.awayScore;
  const awayLead = fixture.awayScore > fixture.homeScore;
  const [eventsOpen, setEventsOpen] = useState(false);

  const events: EventItem[] = [
    ...goals.filter(g => g.fixtureId === fixture.id).map(g => ({
      kind: 'goal' as const, minute: g.minute, extraMinute: g.extraMinute, playerName: g.playerName, teamId: g.teamId, type: g.type,
    })),
    ...cards.filter(c => c.fixtureId === fixture.id).map(c => ({
      kind: 'card' as const, minute: c.minute, extraMinute: c.extraMinute, playerName: c.playerName, teamId: c.teamId, type: c.type,
    })),
    ...substitutions.filter(s => s.fixtureId === fixture.id).map(s => ({
      kind: 'sub' as const, minute: s.minute, extraMinute: s.extraMinute, playerInName: s.playerInName, playerOutName: s.playerOutName, teamId: s.teamId,
    })),
    ...varEvents.filter(v => v.fixtureId === fixture.id).map(v => ({
      kind: 'var' as const, minute: v.minute, extraMinute: v.extraMinute, playerName: v.playerName, teamId: v.teamId, type: v.type,
    })),
  ];
  const renderItems = buildRenderList(events, 'Completed');

  return (
    <div className="rounded-card bg-surface p-4 flex flex-col gap-2.5 border" style={{ borderColor: 'var(--border)' }}>
      {/* Header */}
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">Group {fixture.groupLetter} · {fixture.venue}</span>
        <span className="font-display font-bold text-[11px] px-2 py-0.5 rounded-md bg-surface-3 text-fg-secondary">FT</span>
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
            <span className={`font-display font-black text-[22px] tnum ${lead ? 'text-fg' : 'text-fg-muted'}`}>
              {score}
            </span>
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
            <ul className="flex flex-col gap-1.5 mt-2 text-[12px] text-fg-secondary">
              {renderItems.map((item, i) => {
                if (item.kind === 'marker') {
                  return (
                    <li key={`m-${i}`} className="flex items-center gap-2 text-[10px] font-bold text-fg-muted uppercase tracking-wider">
                      <span className="w-4s" />
                      <span className="flex-1 border-t border-border" />
                      <span>{item.label}</span>
                      <span className="flex-1 border-t border-border" />
                      <span className="w-4s" />
                    </li>
                  );
                }
                if (item.kind === 'sub') {
                  return (
                    <li key={i} className="flex items-center gap-2">
                      <span className="font-mono text-fg-muted w-10 text-right tnum">{formatMinute(item.minute, item.extraMinute)}</span>
                      <span className="inline-flex flex-col items-center shrink-0 text-[10px] leading-none gap-px w-2">
                        <span style={{ color: 'var(--win)' }}>▲</span>
                        <span style={{ color: 'var(--loss)' }}>▼</span>
                      </span>
                      <span className="flex flex-col text-[11px] leading-snug">
                        <span className="font-medium text-fg">{item.playerInName}</span>
                        <span className="text-fg-muted line-through">{item.playerOutName}</span>
                      </span>
                      <span className="font-mono text-fg-muted text-[11px]">{item.teamId.toUpperCase()}</span>
                    </li>
                  );
                }
                if (item.kind === 'var') {
                  return (
                    <li key={i} className="flex items-center gap-2">
                      <span className="font-mono text-fg-muted w-10 text-right tnum">{formatMinute(item.minute, item.extraMinute)}</span>
                      <span className="text-[9px] font-extrabold px-1.5 py-px rounded shrink-0"
                            style={{ background: 'rgba(147,51,234,.15)', color: '#a855f7' }}>VAR</span>
                      <span className="font-medium text-fg">{item.playerName}</span>
                      <span className="text-[11px] text-fg-muted">{varLabel(item.type)}</span>
                    </li>
                  );
                }
                return (
                  <li key={i} className="flex items-center gap-2">
                    <span className="font-mono text-fg-muted w-10 text-right tnum">{formatMinute(item.minute, item.extraMinute)}</span>
                    {item.kind === 'goal' ? <GoalIcon /> : <CardIcon type={item.type} />}
                    <span className="font-medium text-fg">{item.playerName}</span>
                    <span className="font-mono text-fg-muted text-[11px]">{item.teamId}</span>
                    {item.kind === 'goal' && item.type === 'OwnGoal' && (
                      <span className="text-[9px] font-extrabold px-1.5 py-px rounded"
                            style={{ background: 'rgba(255,107,107,.16)', color: 'var(--loss)' }}>OG</span>
                    )}
                    {item.kind === 'goal' && item.type === 'PenaltyInMatch' && (
                      <span className="text-[9px] font-extrabold px-1.5 py-px rounded"
                            style={{ background: 'rgba(242,193,78,.18)', color: 'var(--accent)' }}>PEN</span>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}

      {/* User prediction + points */}
      <div className="pt-2.5 border-t border-border">
        {prediction !== undefined ? (
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
