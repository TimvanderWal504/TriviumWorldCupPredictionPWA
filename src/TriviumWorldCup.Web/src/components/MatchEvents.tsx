export interface GoalEventDto {
  fixtureId: string;
  playerId: string;
  playerName: string;
  teamId: string;
  type: string;
  minute: number;
  extraMinute: number | null;
}

export interface CardEventDto {
  fixtureId: string;
  playerId: string;
  playerName: string;
  teamId: string;
  type: string;
  minute: number;
  extraMinute: number | null;
}

export interface SubstitutionEventDto {
  fixtureId: string;
  playerInName: string;
  playerOutName: string;
  teamId: string;
  minute: number;
  extraMinute: number | null;
}

export interface VarEventDto {
  fixtureId: string;
  playerName: string;
  teamId: string;
  type: string;
  minute: number;
  extraMinute: number | null;
}

export type EventItem =
  | { kind: 'goal'; minute: number; extraMinute: number | null; playerName: string; teamId: string; type: string }
  | { kind: 'card'; minute: number; extraMinute: number | null; playerName: string; teamId: string; type: string }
  | { kind: 'sub';  minute: number; extraMinute: number | null; playerInName: string; playerOutName: string; teamId: string }
  | { kind: 'var';  minute: number; extraMinute: number | null; playerName: string; teamId: string; type: string };

export type RenderItem = EventItem | { kind: 'marker'; label: string };

export function formatMinute(minute: number, extra: number | null): string {
  return extra ? `${minute}+${extra}'` : `${minute}'`;
}

export function varLabel(type: string): string {
  if (type === 'GoalCancelled') return 'Goal ruled out';
  if (type === 'CardUpgradeRed') return 'Red card upgrade';
  return '2nd yellow upgrade';
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

// beforeExtra=false (HT): marker comes AFTER period stoppage — fires when minute crosses into next period (>45 means 2nd half started)
// beforeExtra=true  (90', ET HT): marker comes BEFORE period stoppage — fires when reaching minute+extra of threshold (90+1 means regulation ended)
const PERIOD_THRESHOLDS: { minute: number; label: string; beforeExtra: boolean }[] = [
  { minute: 45,  label: 'HT',    beforeExtra: false },
  { minute: 90,  label: "90'",   beforeExtra: true  },
  { minute: 105, label: 'ET HT', beforeExtra: true  },
];

export function buildEventItems(
  fixtureId: string,
  goals: GoalEventDto[],
  cards: CardEventDto[],
  substitutions: SubstitutionEventDto[],
  varEvents: VarEventDto[],
): EventItem[] {
  return [
    ...goals.filter(g => g.fixtureId === fixtureId).map(g => ({
      kind: 'goal' as const, minute: g.minute, extraMinute: g.extraMinute, playerName: g.playerName, teamId: g.teamId, type: g.type,
    })),
    ...cards.filter(c => c.fixtureId === fixtureId).map(c => ({
      kind: 'card' as const, minute: c.minute, extraMinute: c.extraMinute, playerName: c.playerName, teamId: c.teamId, type: c.type,
    })),
    ...substitutions.filter(s => s.fixtureId === fixtureId).map(s => ({
      kind: 'sub' as const, minute: s.minute, extraMinute: s.extraMinute, playerInName: s.playerInName, playerOutName: s.playerOutName, teamId: s.teamId,
    })),
    ...varEvents.filter(v => v.fixtureId === fixtureId).map(v => ({
      kind: 'var' as const, minute: v.minute, extraMinute: v.extraMinute, playerName: v.playerName, teamId: v.teamId, type: v.type,
    })),
  ];
}

export function buildRenderList(events: EventItem[], status: string): RenderItem[] {
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

export function eventKey(ev: EventItem): string {
  if (ev.kind === 'sub')
    return `sub-${ev.minute}-${ev.extraMinute ?? ''}-${ev.playerInName}-${ev.teamId}`;
  return `${ev.kind}-${ev.minute}-${ev.extraMinute ?? ''}-${ev.playerName}-${ev.teamId}-${ev.type}`;
}

interface MatchEventsListProps {
  renderItems: RenderItem[];
  newKeys?: Set<string>;
}

export function MatchEventsList({ renderItems, newKeys }: MatchEventsListProps) {
  return (
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
        const isNew = newKeys?.has(eventKey(item)) ?? false;
        if (item.kind === 'sub') {
          return (
            <li key={i} className={`flex items-center gap-2${isNew ? ' event-new' : ''}`}>
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
            <li key={i} className={`flex items-center gap-2${isNew ? ' event-new' : ''}`}>
              <span className="font-mono text-fg-muted w-10 text-right tnum">{formatMinute(item.minute, item.extraMinute)}</span>
              <span className="text-[9px] font-extrabold px-1.5 py-px rounded shrink-0"
                    style={{ background: 'rgba(147,51,234,.15)', color: '#a855f7' }}>VAR</span>
              <span className="font-medium text-fg">{item.playerName}</span>
              <span className="text-[11px] text-fg-muted">{varLabel(item.type)}</span>
            </li>
          );
        }
        return (
          <li key={i} className={`flex items-center gap-2${isNew ? ' event-new' : ''}`}>
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
  );
}
