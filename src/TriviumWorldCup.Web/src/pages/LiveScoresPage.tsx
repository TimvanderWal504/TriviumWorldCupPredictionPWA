import { useEffect, useRef, useState } from 'react';
import { Clock } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';

interface LiveFixtureDto {
  id: string;
  matchNumber: number;
  groupLetter: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  kickoffUtc: string;
  status: string;
  homeScore: number | null;
  awayScore: number | null;
  venue: string;
  city: string;
  elapsedMinute: number | null;
  elapsedExtra: number | null;
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

interface VarEventDto {
  fixtureId: string;
  playerName: string;
  teamId: string;
  type: string;
  minute: number;
  extraMinute: number | null;
}

interface LiveFixturesResponse {
  fixtures: LiveFixtureDto[];
  goals: GoalEventDto[];
  cards: CardEventDto[];
  substitutions: SubstitutionEventDto[];
  varEvents: VarEventDto[];
  liveWindowActive: boolean;
}

async function fetchLiveFixtures(): Promise<LiveFixturesResponse> {
  const res = await fetch('/fixtures/live', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load live fixtures (${res.status})`);
  return res.json() as Promise<LiveFixturesResponse>;
}

function formatKickoff(kickoffUtc: string): string {
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday: 'short', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

function StatusBadge({ status, elapsedMinute, elapsedExtra }: { status: string; elapsedMinute: number | null; elapsedExtra: number | null }) {
  if (status === 'InProgress') {
    const clock = elapsedMinute != null ? ` ${formatMinute(elapsedMinute, elapsedExtra)}` : '';
    return (
      <span className="inline-flex items-center gap-1.5 font-display font-bold text-[11px] px-2 py-0.5 rounded-md text-white"
            style={{ background: 'var(--live)' }}>
        <span className="live-dot w-1.5 h-1.5 rounded-full bg-white" />
        LIVE{clock}
      </span>
    );
  }
  if (status === 'Completed') {
    return <span className="font-display font-bold text-[11px] px-2 py-0.5 rounded-md bg-surface-3 text-fg-secondary">FT</span>;
  }
  return (
    <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md"
          style={{ background: 'var(--warning-soft)', color: 'var(--warning)' }}>Soon</span>
  );
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

type RenderItem = EventItem | { kind: 'marker'; label: string };

// beforeExtra=false (HT): marker comes AFTER period stoppage — fires when minute crosses into next period (>45 means 2nd half started)
// beforeExtra=true  (90', ET HT): marker comes BEFORE period stoppage — fires when reaching minute+extra of threshold (90+1 means regulation ended)
const PERIOD_THRESHOLDS: { minute: number; label: string; beforeExtra: boolean }[] = [
  { minute: 45,  label: 'HT',    beforeExtra: false },
  { minute: 90,  label: "90'",   beforeExtra: true  },
  { minute: 105, label: 'ET HT', beforeExtra: true  },
];

function buildRenderList(events: EventItem[], status: string): RenderItem[] {
  const sorted = [...events].sort((a, b) =>
    a.minute !== b.minute ? a.minute - b.minute : (a.extraMinute ?? 0) - (b.extraMinute ?? 0)
  );
  const hasExtraTime = sorted.some(e => e.minute > 90);
  const thresholds = PERIOD_THRESHOLDS.filter(t => t.label !== "90'" || hasExtraTime);
  const result: RenderItem[] = [];
  if (sorted.length > 0) result.push({ kind: 'marker', label: 'Kick-off' });
  let tIdx = 0;
  for (const evt of sorted) {
    while (tIdx < thresholds.length) {
      const t = thresholds[tIdx];
      const crosses = t.beforeExtra
        ? evt.minute > t.minute || (evt.minute === t.minute && evt.extraMinute != null)
        : evt.minute > t.minute;
      if (!crosses) break;
      result.push({ kind: 'marker', label: t.label });
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

function eventKey(ev: EventItem): string {
  if (ev.kind === 'sub')
    return `sub-${ev.minute}-${ev.extraMinute ?? ''}-${ev.playerInName}-${ev.teamId}`;
  return `${ev.kind}-${ev.minute}-${ev.extraMinute ?? ''}-${ev.playerName}-${ev.teamId}-${ev.type}`;
}

function varLabel(type: string): string {
  if (type === 'GoalCancelled') return 'Goal ruled out';
  if (type === 'CardUpgradeRed') return 'Red card upgrade';
  return '2nd yellow upgrade';
}

interface FixtureCardProps { fixture: LiveFixtureDto; goals: GoalEventDto[]; cards: CardEventDto[]; substitutions: SubstitutionEventDto[]; varEvents: VarEventDto[]; }

function LiveFixtureCard({ fixture, goals, cards, substitutions, varEvents }: FixtureCardProps) {
  const isLive = fixture.status === 'InProgress';
  const hasScore = (fixture.status === 'InProgress' || fixture.status === 'Completed')
    && fixture.homeScore !== null && fixture.awayScore !== null;
  const homeLead = hasScore && (fixture.homeScore ?? 0) > (fixture.awayScore ?? 0);
  const awayLead = hasScore && (fixture.awayScore ?? 0) > (fixture.homeScore ?? 0);

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
  const renderItems = buildRenderList(events, fixture.status);

  const seenKeysRef = useRef<Set<string> | null>(null);
  const currentKeys = new Set(events.map(eventKey));
  const newKeys: Set<string> = seenKeysRef.current === null
    ? new Set()
    : new Set([...currentKeys].filter(k => !seenKeysRef.current!.has(k)));
  useEffect(() => { seenKeysRef.current = currentKeys; });

  const cardStyle = isLive
    ? { borderColor: 'transparent', boxShadow: 'var(--shadow-glow-live)' }
    : { borderColor: 'var(--border)' };

  return (
    <div className="rounded-card bg-surface p-4 flex flex-col gap-2.5 border" style={cardStyle}>
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">Group {fixture.groupLetter} · {fixture.venue}</span>
        <StatusBadge status={fixture.status} elapsedMinute={fixture.elapsedMinute} elapsedExtra={fixture.elapsedExtra} />
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
            {hasScore
              ? <span className={`font-display font-black text-[22px] tnum ${lead ? 'text-fg' : 'text-fg-muted'}`}>{score}</span>
              : null}
          </div>
        ))}
      </div>

      {fixture.status === 'Scheduled' && (
        <div className="flex items-center gap-1.5 text-[11px] text-fg-muted">
          <Clock size={12} />{formatKickoff(fixture.kickoffUtc)}
        </div>
      )}

      {renderItems.length > 0 && (
        <ul className="flex flex-col gap-1.5 text-[12px] text-fg-secondary">
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
                <li key={i} className={`flex items-center gap-2${newKeys.has(eventKey(item)) ? ' event-new' : ''}`}>
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
                <li key={i} className={`flex items-center gap-2${newKeys.has(eventKey(item)) ? ' event-new' : ''}`}>
                  <span className="font-mono text-fg-muted w-10 text-right tnum">{formatMinute(item.minute, item.extraMinute)}</span>
                  <span className="text-[9px] font-extrabold px-1.5 py-px rounded shrink-0"
                        style={{ background: 'rgba(147,51,234,.15)', color: '#a855f7' }}>VAR</span>
                  <span className="font-medium text-fg">{item.playerName}</span>
                  <span className="text-[11px] text-fg-muted">{varLabel(item.type)}</span>
                </li>
              );
            }
            return (
              <li key={i} className={`flex items-center gap-2${newKeys.has(eventKey(item)) ? ' event-new' : ''}`}>
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
  );
}

const POLL_INTERVAL_MS = 20_000;

export function LiveScoresPage() {
  const [data, setData] = useState<LiveFixturesResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  function stopPolling() {
    if (intervalRef.current !== null) { clearInterval(intervalRef.current); intervalRef.current = null; }
  }

  async function load() {
    try {
      const response = await fetchLiveFixtures();
      setData(response);
      setLoadError(null);
      if (!response.liveWindowActive) {
        stopPolling();
      } else if (intervalRef.current === null) {
        // Live window became active after polling was paused — restart.
        intervalRef.current = setInterval(() => void load(), POLL_INTERVAL_MS);
      }
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Failed to load live scores.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
    intervalRef.current = setInterval(() => void load(), POLL_INTERVAL_MS);
    return () => stopPolling();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (loading) return <div className="flex items-center justify-center py-20 text-fg-muted">Loading live scores…</div>;
  if (loadError) return <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>{loadError}</div>;

  const liveActive = data?.liveWindowActive === true;
  const liveMatches = data?.fixtures.filter(f => f.status === 'InProgress') ?? [];
  const otherMatches = data?.fixtures.filter(f => f.status !== 'InProgress') ?? [];

  return (
    <div className="max-w-3xl mx-auto px-4 py-4 flex flex-col gap-3">
      {liveActive && (
        <div className="flex items-center gap-1.5 text-[12px] font-medium" style={{ color: 'var(--live)' }}>
          <span className="live-dot w-2 h-2 rounded-full inline-block" style={{ background: 'var(--live)' }} />
          Live updates every 20s
        </div>
      )}

      {liveMatches.length === 0 && !liveActive && (
        <div className="rounded-card bg-surface border border-border px-4 py-16 text-center">
          <p className="text-fg-muted text-sm">No matches currently live. Live scores will appear here when a match is in progress.</p>
        </div>
      )}

      {liveMatches.map(f => (
        <LiveFixtureCard key={f.id} fixture={f} goals={data?.goals ?? []} cards={data?.cards ?? []} substitutions={data?.substitutions ?? []} varEvents={data?.varEvents ?? []} />
      ))}

      {otherMatches.length > 0 && (
        <>
          <p className="text-[11px] font-display font-bold uppercase tracking-[0.08em] text-fg-muted mt-1">
            Earlier &amp; upcoming
          </p>
          <div className="flex flex-col gap-2">
            {otherMatches.map(f => (
              <LiveFixtureCard key={f.id} fixture={f} goals={data?.goals ?? []} cards={data?.cards ?? []} substitutions={data?.substitutions ?? []} varEvents={data?.varEvents ?? []} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}
