import { useEffect, useState } from 'react';
import { flagUrl } from '../utils/flagUrl.ts';
import { Spinner } from '../components/ui/Spinner.tsx';

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
  predictedHomeScore: number | null; predictedAwayScore: number | null; submittedAt: string;
}

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
  slotKey: string,
  predictedWinnerTeamId: string,
  predictedHomeScore: number | null,
  predictedAwayScore: number | null,
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

const ROUND_ORDER = ['R32', 'R16', 'QF', 'SF', 'ThirdPlace', 'Final'];
const ROUND_LABELS: Record<string, string> = {
  R32: 'R32', R16: 'R16', QF: 'QF', SF: 'SF', ThirdPlace: '3rd', Final: 'Final',
};
const ROUND_MULTIPLIER: Record<string, string> = {
  R32: '×1', R16: '×1.5', QF: '×2', SF: '×2.5', ThirdPlace: '×2.5', Final: '×3',
};

function isLocked(kickoffUtc: string | null): boolean {
  if (!kickoffUtc) return true;
  return new Date(kickoffUtc).getTime() <= Date.now();
}
function formatCompactKickoff(kickoffUtc: string | null): string {
  if (!kickoffUtc) return 'TBD';
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday: 'short', hour: '2-digit', minute: '2-digit',
  });
}

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

  const [selectedWinner, setSelectedWinner] = useState(prediction?.predictedWinnerTeamId ?? '');
  const [homeInput, setHomeInput] = useState(
    prediction?.predictedHomeScore != null ? String(prediction.predictedHomeScore) : '',
  );
  const [awayInput, setAwayInput] = useState(
    prediction?.predictedAwayScore != null ? String(prediction.predictedAwayScore) : '',
  );
  const [saving, setSaving] = useState(false);
  const [error, setError]   = useState<string | null>(null);
  const [saved, setSaved]   = useState(false);

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

  // Right side of header
  let headerRight = '';
  let headerRightColor = 'var(--fg-muted)';
  if (isLive) {
    headerRight      = isLivePen ? 'PEN' : isLiveET ? 'AET' : 'LIVE';
    headerRightColor = 'var(--live)';
  } else if (hasResult) {
    headerRight      = ROUND_MULTIPLIER[slot.round] ?? '';
    headerRightColor = 'var(--secondary)';
  } else {
    headerRight = formatCompactKickoff(slot.kickoffUtc);
  }

  const canPick = !locked && teamsKnown && !hasResult;

  const teams = teamsKnown ? [
    { id: slot.homeTeamId!, score: slot.homeScore, penScore: slot.penaltyHomeScore, isWinner: slot.winnerTeamId === slot.homeTeamId },
    { id: slot.awayTeamId!, score: slot.awayScore, penScore: slot.penaltyAwayScore, isWinner: slot.winnerTeamId === slot.awayTeamId },
  ] : null;

  const borderColor = !teamsKnown
    ? 'var(--border)'
    : isLive
    ? 'var(--live)'
    : !locked && !prediction && !hasResult
    ? 'var(--secondary)'
    : 'var(--border)';

  return (
    <div className="rounded-card bg-surface border overflow-hidden" style={{ borderColor }}>

      {/* Header */}
      <div className="flex items-center justify-between px-4 py-2.5">
        <span className="text-[12px] font-mono font-medium text-fg-muted">{slot.slotKey}</span>
        <span className="text-[12px] font-semibold" style={{ color: headerRightColor }}>
          {headerRight}
        </span>
      </div>

      {/* TBD state */}
      {!teamsKnown && (
        <div className="px-4 pb-3 pt-1 text-[13px] text-fg-muted italic border-t border-border">
          Bracket not yet set
        </div>
      )}

      {/* Teams + footer */}
      {teamsKnown && (
        <>
          {/* Team rows */}
          <div className="border-t border-border">
            {teams!.map(({ id, score, penScore, isWinner }) => {
              const isSelected = selectedWinner === id;
              const dimmed = hasResult
                ? !isWinner
                : selectedWinner !== '' && !isSelected;

              return (
                <div
                  key={id}
                  onClick={() => canPick && setSelectedWinner(id)}
                  className={[
                    'flex items-center gap-3 px-4 py-2.5 border-b border-border last:border-b-0 transition-colors',
                    canPick ? 'cursor-pointer active:bg-surface-2' : '',
                    dimmed ? 'opacity-40' : '',
                  ].join(' ')}
                >
                  {flagUrl(id) && (
                    <img src={flagUrl(id)} alt="" width={22} height={15} className="flag shrink-0" />
                  )}
                  <span className={`flex-1 min-w-0 truncate text-[15px] font-semibold ${isWinner || isSelected ? 'text-fg' : 'text-fg-secondary'}`}>
                    {name(id)}
                  </span>
                  {isWinner && (
                    <span className="text-[12px] font-bold mr-1 shrink-0" style={{ color: 'var(--win)' }}>✓</span>
                  )}
                  {canPick && isSelected && (
                    <span className="w-2 h-2 rounded-full mr-1 shrink-0" style={{ background: 'var(--secondary)' }} />
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

          {/* AET/penalties note */}
          {(wentToAet || wonOnPens) && (
            <div className="px-4 py-1.5 border-t border-border">
              <span className="text-[11px] text-fg-muted">
                {wonOnPens ? 'Won on penalties' : 'After extra time'}
              </span>
            </div>
          )}

          {/* Footer: pick status / action */}
          <div className="flex items-center justify-between gap-3 px-4 py-2.5 border-t border-border min-h-[40px]">
            <span className="text-[12px] text-fg-muted shrink-0">Your pick</span>

            {/* Locked — show existing prediction or dash */}
            {locked && (
              prediction ? (
                <span className="text-[13px] font-semibold text-right" style={{ color: 'var(--secondary)' }}>
                  {name(prediction.predictedWinnerTeamId)}
                  {prediction.predictedHomeScore != null && (
                    <span className="text-fg-muted font-normal tnum">
                      {' '}({prediction.predictedHomeScore}–{prediction.predictedAwayScore})
                    </span>
                  )}
                </span>
              ) : (
                <span className="text-[13px] text-fg-muted">—</span>
              )
            )}

            {/* Unlocked + winner selected: inline score inputs + save */}
            {!locked && selectedWinner && (
              <div className="flex items-center gap-1.5">
                <input
                  type="number" min={0} max={99} placeholder="0" value={homeInput}
                  onChange={e => setHomeInput(e.target.value)}
                  aria-label="Home score"
                  className="w-10 text-center text-[13px] font-bold tnum bg-surface-2 border border-border rounded-input py-0.5 focus:outline-none focus:border-secondary"
                />
                <span className="text-[12px] text-fg-muted">–</span>
                <input
                  type="number" min={0} max={99} placeholder="0" value={awayInput}
                  onChange={e => setAwayInput(e.target.value)}
                  aria-label="Away score"
                  className="w-10 text-center text-[13px] font-bold tnum bg-surface-2 border border-border rounded-input py-0.5 focus:outline-none focus:border-secondary"
                />
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className="ml-1 px-3 py-1 rounded-input text-[12px] font-semibold disabled:opacity-40 transition-colors shrink-0"
                  style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
                >
                  {saving ? '…' : saved ? '✓' : 'Save'}
                </button>
              </div>
            )}

            {/* Unlocked + no winner selected yet: hint */}
            {!locked && !selectedWinner && (
              <span className="text-[13px] font-semibold" style={{ color: 'var(--secondary)' }}>
                Tap to pick →
              </span>
            )}
          </div>

          {error && (
            <div className="px-4 pb-3 text-[12px]" style={{ color: 'var(--loss)' }}>
              {error}
            </div>
          )}
        </>
      )}
    </div>
  );
}

export function KnockoutBracketPage() {
  const [slots, setSlots]           = useState<KnockoutSlotDto[]>([]);
  const [predictions, setPredictions] = useState<Map<string, KnockoutPredictionDto>>(new Map());
  const [teamNames, setTeamNames]   = useState<Map<string, string>>(new Map());
  const [loading, setLoading]       = useState(true);
  const [loadError, setLoadError]   = useState<string | null>(null);
  const [activeRound, setActiveRound] = useState<string>('R32');

  useEffect(() => {
    Promise.all([fetchSlots(), fetchPredictions(), fetchTeamNames()])
      .then(([slotList, predictionList, names]) => {
        setSlots(slotList);
        setTeamNames(names);
        const map = new Map<string, KnockoutPredictionDto>();
        for (const p of predictionList) map.set(p.slotKey, p);
        setPredictions(map);
        const firstRound = ROUND_ORDER.find(r => slotList.some(s => s.round === r));
        if (firstRound) setActiveRound(firstRound);
      })
      .catch((err: unknown) =>
        setLoadError(err instanceof Error ? err.message : 'Failed to load bracket data.'),
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

      {/* Round tab bar — underline style */}
      <div className="flex border-b border-border overflow-x-auto appscroll mb-4">
        {presentRounds.map(round => {
          const active = activeRound === round;
          return (
            <button
              key={round}
              role="tab"
              aria-selected={active}
              onClick={() => setActiveRound(round)}
              className={[
                'px-4 py-2.5 text-[13px] font-semibold whitespace-nowrap transition-colors border-b-2 -mb-px shrink-0',
                active ? 'border-secondary text-secondary' : 'border-transparent text-fg-muted hover:text-fg',
              ].join(' ')}
              style={active ? { borderColor: 'var(--secondary)', color: 'var(--secondary)' } : undefined}
            >
              {ROUND_LABELS[round] ?? round}
            </button>
          );
        })}
      </div>

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
