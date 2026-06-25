import { useEffect, useState, type CSSProperties } from 'react';
import { Clock } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';
import { Spinner } from '../components/ui/Spinner.tsx';

/* ───────────────────────────────────────────────────────────────────────────
   Types
─────────────────────────────────────────────────────────────────────────── */
interface KnockoutSlotDto {
  slotKey: string; round: string; slotNumber: number;
  homeTeamId: string | null; awayTeamId: string | null;
  kickoffUtc: string | null; venue: string | null; city: string | null;
  status: string;
  homeScore: number | null; awayScore: number | null;
  penaltyHomeScore: number | null; penaltyAwayScore: number | null;
  winnerTeamId: string | null;
}
interface KnockoutPredictionDto {
  slotKey: string; predictedWinnerTeamId: string;
  predictedHomeScore: number | null; predictedAwayScore: number | null;
  submittedAt: string;
}

/* ───────────────────────────────────────────────────────────────────────────
   API
─────────────────────────────────────────────────────────────────────────── */
async function fetchSlots(): Promise<KnockoutSlotDto[]> {
  const res = await fetch('/knockout/slots', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load bracket slots (${res.status})`);
  return res.json() as Promise<KnockoutSlotDto[]>;
}
async function fetchPredictions(): Promise<KnockoutPredictionDto[]> {
  const res = await fetch('/predictions/knockout', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load predictions (${res.status})`);
  return res.json() as Promise<KnockoutPredictionDto[]>;
}
async function fetchTeamNames(): Promise<Map<string, string>> {
  const res = await fetch('/teams', { credentials: 'include' });
  if (!res.ok) return new Map();
  const teams = await res.json() as { id: string; name: string; fifaCode: string }[];
  const map = new Map<string, string>();
  for (const t of teams) {
    map.set(t.id, t.name);
    if (t.fifaCode) map.set(t.fifaCode, t.name);
  }
  return map;
}
async function savePrediction(
  slotKey: string, predictedWinnerTeamId: string,
  predictedHomeScore: number | null, predictedAwayScore: number | null,
  method: 'POST' | 'PUT',
): Promise<{ ok: boolean; error?: string }> {
  const res = await fetch(`/predictions/knockout/${slotKey}`, {
    method, headers: { 'Content-Type': 'application/json' }, credentials: 'include',
    body: JSON.stringify({ predictedWinnerTeamId, predictedHomeScore, predictedAwayScore }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  if (res.status === 403) return { ok: false, error: 'Locked — predictions closed at kickoff.' };
  if (res.status === 422) return { ok: false, error: body.error ?? 'Bracket not yet resolved.' };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

/* ───────────────────────────────────────────────────────────────────────────
   Constants
─────────────────────────────────────────────────────────────────────────── */
const ROUND_ORDER   = ['R32', 'R16', 'QF', 'SF', 'ThirdPlace', 'Final'];
const ROUND_LABELS: Record<string, string> = {
  R32:'R32', R16:'R16', QF:'QF', SF:'SF', ThirdPlace:'3rd', Final:'Final',
};
const ROUND_FULL: Record<string, string> = {
  R32:'Round of 32', R16:'Round of 16', QF:'Quarter-finals',
  SF:'Semi-finals', ThirdPlace:'Third-place play-off', Final:'Final',
};
const ROUND_MULTIPLIER: Record<string, string> = {
  R32:'×1', R16:'×1.5', QF:'×2', SF:'×2.5', ThirdPlace:'×2.5', Final:'×3',
};

function isLocked(kickoffUtc: string | null): boolean {
  if (!kickoffUtc) return true;
  return new Date(kickoffUtc).getTime() <= Date.now();
}
function formatKickoff(kickoffUtc: string | null): string {
  if (!kickoffUtc) return 'TBD';
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday:'short', month:'short', day:'numeric', hour:'2-digit', minute:'2-digit',
  });
}

/* ───────────────────────────────────────────────────────────────────────────
   SlotCard — styled to match FixtureCard from GroupPredictionsPage
─────────────────────────────────────────────────────────────────────────── */
interface SlotCardProps {
  slot: KnockoutSlotDto;
  prediction: KnockoutPredictionDto | undefined;
  teamNames: Map<string, string>;
  onSaved: (u: KnockoutPredictionDto) => void;
}

function SlotCard({ slot, prediction, teamNames, onSaved }: SlotCardProps) {
  const locked     = isLocked(slot.kickoffUtc);
  const teamsKnown = slot.homeTeamId !== null && slot.awayTeamId !== null;
  const hasResult  = slot.homeScore !== null && slot.awayScore !== null;
  const isLiveET   = slot.status === 'ExtraTime';
  const isLivePen  = slot.status === 'PenaltyShootout';
  const isLive     = slot.status === 'InProgress' || isLiveET || isLivePen;
  const wonOnPens  = slot.penaltyHomeScore !== null && slot.penaltyAwayScore !== null;
  const wentToAet  = hasResult && slot.homeScore === slot.awayScore
                  && slot.winnerTeamId !== null && !wonOnPens;
  const canPick    = !locked && teamsKnown && !hasResult;

  const [selectedWinner, setSelectedWinner] = useState(prediction?.predictedWinnerTeamId ?? '');
  const [homeInput, setHomeInput] = useState(
    prediction?.predictedHomeScore != null ? String(prediction.predictedHomeScore) : '',
  );
  const [awayInput, setAwayInput] = useState(
    prediction?.predictedAwayScore != null ? String(prediction.predictedAwayScore) : '',
  );
  const [saving, setSaving] = useState(false);
  const [error,  setError]  = useState<string | null>(null);
  const [saved,  setSaved]  = useState(false);

  useEffect(() => {
    if (prediction) {
      setSelectedWinner(prediction.predictedWinnerTeamId);
      setHomeInput(prediction.predictedHomeScore != null ? String(prediction.predictedHomeScore) : '');
      setAwayInput(prediction.predictedAwayScore != null ? String(prediction.predictedAwayScore) : '');
    }
  }, [prediction]);

  const handleSave = async () => {
    if (!selectedWinner) return;
    setError(null); setSaved(false); setSaving(true);
    const homeScore = homeInput !== '' ? parseInt(homeInput, 10) : null;
    const awayScore = awayInput !== '' ? parseInt(awayInput, 10) : null;
    const result = await savePrediction(
      slot.slotKey, selectedWinner, homeScore, awayScore, prediction ? 'PUT' : 'POST',
    );
    setSaving(false);
    if (result.ok) {
      onSaved({
        slotKey: slot.slotKey, predictedWinnerTeamId: selectedWinner,
        predictedHomeScore: homeScore, predictedAwayScore: awayScore,
        submittedAt: new Date().toISOString(),
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } else {
      setError(result.error ?? 'Save failed.');
    }
  };

  const name = (id: string) => teamNames.get(id) ?? id;

  // Badge — same logic as FixtureCard
  let badgeText = '';
  let badgeStyle: CSSProperties = {};
  if (saving) {
    badgeText = 'Saving…';
    badgeStyle = { background: 'var(--warning-soft)', color: 'var(--warning)' };
  } else if (saved) {
    badgeText = 'Saved';
    badgeStyle = { background: 'var(--win-soft)', color: 'var(--win)' };
  } else if (isLive) {
    badgeText = isLivePen ? 'PEN' : isLiveET ? 'AET' : 'LIVE';
    badgeStyle = { background: 'var(--live)', color: 'white' };
  } else if (hasResult) {
    badgeText = 'Played';
    badgeStyle = { background: 'var(--surface-3)', color: 'var(--fg-muted)' };
  } else if (locked) {
    badgeText = 'Locked';
    badgeStyle = { background: 'var(--surface-3)', color: 'var(--fg-muted)' };
  } else if (prediction) {
    badgeText = 'Predicted';
    badgeStyle = { background: 'var(--surface-3)', color: 'var(--fg-muted)' };
  } else if (!teamsKnown) {
    badgeText = 'TBD';
    badgeStyle = { background: 'var(--surface-3)', color: 'var(--fg-muted)' };
  } else {
    badgeText = 'Unpredicted';
    badgeStyle = { background: 'var(--win-soft)', color: 'var(--win)' };
  }

  // Border — same logic as FixtureCard
  const borderColor = isLive
    ? 'var(--live)'
    : !locked && teamsKnown && !prediction && !hasResult
    ? 'var(--secondary)'
    : 'var(--border)';

  const kickoffLine = hasResult
    ? (ROUND_MULTIPLIER[slot.round] ?? '')
    : formatKickoff(slot.kickoffUtc);

  const teams = teamsKnown ? [
    { id: slot.homeTeamId!, score: slot.homeScore, penScore: slot.penaltyHomeScore, isWinner: slot.winnerTeamId === slot.homeTeamId },
    { id: slot.awayTeamId!, score: slot.awayScore, penScore: slot.penaltyAwayScore, isWinner: slot.winnerTeamId === slot.awayTeamId },
  ] : null;

  return (
    <div className="rounded-card bg-surface p-4 flex flex-col gap-2.5 border" style={{ borderColor }}>

      {/* Header — matches FixtureCard */}
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">
          {slot.slotKey}{slot.venue ? ` · ${slot.venue}` : ''}
        </span>
        <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md" style={badgeStyle}>
          {badgeText}
        </span>
      </div>

      {/* Kickoff row — matches FixtureCard */}
      <div className="flex items-center gap-1.5 text-[11px] text-fg-muted">
        <Clock size={12} />
        <span>{kickoffLine}</span>
      </div>

      {/* TBD placeholder */}
      {!teamsKnown && (
        <p className="text-[13px] text-fg-muted italic">Bracket not yet set</p>
      )}

      {/* Team rows — same structure as FixtureCard; rows are tappable to pick */}
      {teamsKnown && (
        <div className="flex flex-col gap-2">
          {teams!.map(({ id, score, penScore, isWinner }) => {
            const isSelected = selectedWinner === id;
            const dimmed = hasResult
              ? !isWinner
              : selectedWinner !== '' && !isSelected;
            const url = flagUrl(id);
            return (
              <div
                key={id}
                onClick={() => canPick && setSelectedWinner(id)}
                className={[
                  'flex items-center gap-2.5 transition-opacity',
                  canPick ? 'cursor-pointer active:opacity-60' : '',
                  dimmed ? 'opacity-40' : '',
                ].join(' ')}
              >
                {url && (
                  <img src={url} alt="" width={28} height={20} className="flag shrink-0" />
                )}
                <span className={`flex-1 min-w-0 truncate font-semibold ${isWinner || isSelected ? 'text-fg' : 'text-fg-secondary'}`}>
                  {name(id)}
                </span>
                {isWinner && !canPick && (
                  <span className="text-[12px] font-bold shrink-0 mr-1" style={{ color: 'var(--win)' }}>✓</span>
                )}
                {canPick && isSelected && (
                  <span className="w-2 h-2 rounded-full shrink-0 mr-1" style={{ background: 'var(--secondary)' }} />
                )}
                {hasResult && (
                  <span className={`font-display font-black tnum text-[22px] w-7 text-right shrink-0 ${isWinner ? 'text-fg' : 'text-fg-muted'}`}>
                    {score}
                  </span>
                )}
                {hasResult && wonOnPens && penScore != null && (
                  <span className="text-[11px] text-fg-muted tnum shrink-0">({penScore})</span>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* AET / penalties note */}
      {(wentToAet || wonOnPens) && (
        <p className="text-[11px] text-fg-muted">
          {wonOnPens ? 'Won on penalties' : 'After extra time'}
        </p>
      )}

      {/* Footer: pick action (unlocked) */}
      {canPick && (
        <div className="flex items-center justify-between gap-3 text-[12px]">
          <span className="text-fg-muted shrink-0">Your pick</span>
          {selectedWinner ? (
            <div className="flex items-center gap-1.5">
              <input
                type="number" min={0} max={99} placeholder="0" value={homeInput}
                onChange={e => setHomeInput(e.target.value)} aria-label="Home score"
                className="w-10 text-center text-[13px] font-bold tnum bg-surface-2 border border-border rounded-input py-0.5 focus:outline-none focus:border-secondary"
              />
              <span className="text-fg-muted">–</span>
              <input
                type="number" min={0} max={99} placeholder="0" value={awayInput}
                onChange={e => setAwayInput(e.target.value)} aria-label="Away score"
                className="w-10 text-center text-[13px] font-bold tnum bg-surface-2 border border-border rounded-input py-0.5 focus:outline-none focus:border-secondary"
              />
              <button
                onClick={handleSave} disabled={saving}
                className="ml-1 px-3 py-1 rounded-input text-[12px] font-semibold disabled:opacity-40 transition-colors shrink-0"
                style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
              >
                {saving ? '…' : saved ? '✓' : 'Save'}
              </button>
            </div>
          ) : (
            <span className="font-semibold" style={{ color: 'var(--secondary)' }}>
              Tap to pick →
            </span>
          )}
        </div>
      )}

      {/* Footer: locked prediction */}
      {locked && prediction && (
        <div className="flex items-center justify-between text-[12px]">
          <span className="text-fg-muted shrink-0">Your pick</span>
          <span className="font-semibold text-right" style={{ color: 'var(--secondary)' }}>
            {name(prediction.predictedWinnerTeamId)}
            {prediction.predictedHomeScore != null && (
              <span className="text-fg-muted font-normal tnum">
                {' '}({prediction.predictedHomeScore}–{prediction.predictedAwayScore})
              </span>
            )}
          </span>
        </div>
      )}

      {error && (
        <p className="text-[12px] px-3 py-1.5 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>
          {error}
        </p>
      )}
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────────────────
   Page
─────────────────────────────────────────────────────────────────────────── */
export function KnockoutBracketPage() {
  const [slots, setSlots]             = useState<KnockoutSlotDto[]>([]);
  const [predictions, setPredictions] = useState<Map<string, KnockoutPredictionDto>>(new Map());
  const [teamNames, setTeamNames]     = useState<Map<string, string>>(new Map());
  const [loading, setLoading]         = useState(true);
  const [loadError, setLoadError]     = useState<string | null>(null);
  const [activeRound, setActiveRound] = useState<string>('R32');

  useEffect(() => {
    Promise.all([fetchSlots(), fetchPredictions(), fetchTeamNames()])
      .then(([slotList, predList, names]) => {
        setSlots(slotList);
        setTeamNames(names);
        const map = new Map<string, KnockoutPredictionDto>();
        for (const p of predList) map.set(p.slotKey, p);
        setPredictions(map);
        const first = ROUND_ORDER.find(r => slotList.some(s => s.round === r));
        if (first) setActiveRound(first);
      })
      .catch((err: unknown) =>
        setLoadError(err instanceof Error ? err.message : 'Failed to load bracket.'),
      )
      .finally(() => setLoading(false));
  }, []);

  const presentRounds = ROUND_ORDER.filter(r => slots.some(s => s.round === r));
  const activeSlots   = slots
    .filter(s => s.round === activeRound)
    .sort((a, b) => a.slotNumber - b.slotNumber);

  if (loading) return (
    <div className="flex items-center justify-center py-20">
      <Spinner size="lg" label="Loading bracket" />
    </div>
  );
  if (loadError) return (
    <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>
      {loadError}
    </div>
  );

  return (
    <div className="max-w-3xl mx-auto px-4 py-4">

      {/* Bracket progression stepper */}
      <div className="overflow-x-auto appscroll pb-4 -mx-4 px-4">
        <div className="flex items-center gap-1 min-w-max">
          {presentRounds.map((round, i) => {
            const active     = activeRound === round;
            const roundSlots = slots.filter(s => s.round === round);
            const total      = roundSlots.length;
            const predicted  = roundSlots.filter(s =>
              s.winnerTeamId !== null || predictions.has(s.slotKey),
            ).length;
            const allDone    = total > 0 && roundSlots.every(s => s.status === 'Completed');

            return (
              <div key={round} className="flex items-center gap-1">
                <button
                  role="tab"
                  aria-selected={active}
                  onClick={() => setActiveRound(round)}
                  className="flex flex-col items-center gap-0.5 px-4 py-2.5 rounded-card transition-colors shrink-0"
                  style={active
                    ? { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }
                    : { background: 'var(--surface-3)',      color: 'var(--fg-secondary)' }}
                >
                  <span className="text-[13px] font-bold whitespace-nowrap">
                    {ROUND_LABELS[round] ?? round}
                  </span>
                  <span className={`text-[10px] font-mono whitespace-nowrap ${active ? 'opacity-75' : 'text-fg-muted'}`}>
                    {allDone ? 'Done ✓' : `${predicted} / ${total}`}
                  </span>
                </button>
                {i < presentRounds.length - 1 && (
                  <svg className="shrink-0" width="16" height="16" viewBox="0 0 16 16" fill="none"
                    style={{ color: 'var(--fg-muted)' }}>
                    <path d="M3.5 8h9M9.5 5l3 3-3 3" stroke="currentColor" strokeWidth="1.5"
                      strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Round label */}
      <h2 className="font-display font-bold text-[18px] tracking-tight mb-3">
        {ROUND_FULL[activeRound] ?? activeRound}
      </h2>

      {/* Slot cards */}
      <div className="flex flex-col gap-2.5">
        {activeSlots.map(slot => (
          <SlotCard
            key={slot.slotKey}
            slot={slot}
            prediction={predictions.get(slot.slotKey)}
            teamNames={teamNames}
            onSaved={updated =>
              setPredictions(prev => new Map(prev).set(updated.slotKey, updated))
            }
          />
        ))}
      </div>
    </div>
  );
}
