import { useEffect, useRef, useState } from 'react';
import { Clock } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';
import { Spinner } from '../components/ui/Spinner.tsx';
import {
  buildEventItems, buildRenderList, eventKey, formatMinute, MatchEventsList,
  type GoalEventDto, type CardEventDto, type SubstitutionEventDto, type VarEventDto,
} from '../components/MatchEvents.tsx';

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
  if (status === 'Cancelled') {
    return (
      <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md"
            style={{ background: 'var(--live-soft)', color: 'var(--loss)' }}>Cancelled</span>
    );
  }
  return (
    <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md"
          style={{ background: 'var(--warning-soft)', color: 'var(--warning)' }}>Soon</span>
  );
}

interface FixtureCardProps { fixture: LiveFixtureDto; goals: GoalEventDto[]; cards: CardEventDto[]; substitutions: SubstitutionEventDto[]; varEvents: VarEventDto[]; }

function LiveFixtureCard({ fixture, goals, cards, substitutions, varEvents }: FixtureCardProps) {
  const isLive = fixture.status === 'InProgress';
  const hasScore = (fixture.status === 'InProgress' || fixture.status === 'Completed')
    && fixture.homeScore !== null && fixture.awayScore !== null;
  const homeLead = hasScore && (fixture.homeScore ?? 0) > (fixture.awayScore ?? 0);
  const awayLead = hasScore && (fixture.awayScore ?? 0) > (fixture.homeScore ?? 0);

  const events = buildEventItems(fixture.id, goals, cards, substitutions, varEvents);
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

      {renderItems.length > 0 && <MatchEventsList renderItems={renderItems} newKeys={newKeys} />}
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

  if (loading) return (
    <div className="flex items-center justify-center py-20">
      <Spinner size="lg" label="Loading live scores" />
    </div>
  );
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
