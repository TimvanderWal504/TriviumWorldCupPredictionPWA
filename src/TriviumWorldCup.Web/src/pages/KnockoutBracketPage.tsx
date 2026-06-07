import { useEffect, useState, type FormEvent } from 'react';
import { Clock } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';

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
async function savePrediction(
  slotKey: string, predictedWinnerTeamId: string,
  predictedHomeScore: number | null, predictedAwayScore: number | null, method: 'POST' | 'PUT',
): Promise<{ ok: boolean; error?: string }> {
  const res = await fetch(`/predictions/knockout/${slotKey}`, {
    method, headers: { 'Content-Type': 'application/json' }, credentials: 'include',
    body: JSON.stringify({ predictedWinnerTeamId, predictedHomeScore, predictedAwayScore }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  if (res.status === 403) return { ok: false, error: 'This match is locked. Predictions closed at kickoff.' };
  if (res.status === 422) return { ok: false, error: body.error ?? 'Bracket not yet resolved.' };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

const ROUND_ORDER = ['R32', 'R16', 'QF', 'SF', 'ThirdPlace', 'Final'];
const ROUND_LABELS: Record<string, string> = {
  R32: 'Round of 32', R16: 'Round of 16', QF: 'Quarter-finals',
  SF: 'Semi-finals', ThirdPlace: 'Third-place Play-off', Final: 'Final',
};

function isLocked(kickoffUtc: string | null): boolean {
  if (!kickoffUtc) return true;
  return new Date(kickoffUtc).getTime() <= Date.now();
}
function formatKickoff(kickoffUtc: string | null): string {
  if (!kickoffUtc) return 'TBD';
  return new Date(kickoffUtc).toLocaleString(undefined, { weekday: 'short', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

interface SlotCardProps { slot: KnockoutSlotDto; prediction: KnockoutPredictionDto | undefined; onSaved: (u: KnockoutPredictionDto) => void; }

function SlotCard({ slot, prediction, onSaved }: SlotCardProps) {
  const locked = isLocked(slot.kickoffUtc);
  const teamsKnown = slot.homeTeamId !== null && slot.awayTeamId !== null;
  const [selectedWinner, setSelectedWinner] = useState(prediction?.predictedWinnerTeamId ?? '');
  const [homeInput, setHomeInput] = useState(prediction?.predictedHomeScore != null ? String(prediction.predictedHomeScore) : '');
  const [awayInput, setAwayInput] = useState(prediction?.predictedAwayScore != null ? String(prediction.predictedAwayScore) : '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const hasResult = slot.homeScore !== null && slot.awayScore !== null;
  const unpredicted = !prediction && !locked && teamsKnown;

  useEffect(() => {
    if (prediction) {
      setSelectedWinner(prediction.predictedWinnerTeamId);
      setHomeInput(prediction.predictedHomeScore != null ? String(prediction.predictedHomeScore) : '');
      setAwayInput(prediction.predictedAwayScore != null ? String(prediction.predictedAwayScore) : '');
    }
  }, [prediction]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault(); setError(null); setSaved(false);
    if (!selectedWinner) { setError('Select the team you predict will advance.'); return; }
    const homeScore = homeInput !== '' ? parseInt(homeInput, 10) : null;
    const awayScore = awayInput !== '' ? parseInt(awayInput, 10) : null;
    if (homeInput !== '' && (isNaN(homeScore!) || homeScore! < 0)) { setError('Home score must be a non-negative number.'); return; }
    if (awayInput !== '' && (isNaN(awayScore!) || awayScore! < 0)) { setError('Away score must be a non-negative number.'); return; }
    setSaving(true);
    const result = await savePrediction(slot.slotKey, selectedWinner, homeScore, awayScore, prediction !== undefined ? 'PUT' : 'POST');
    setSaving(false);
    if (result.ok) {
      onSaved({ slotKey: slot.slotKey, predictedWinnerTeamId: selectedWinner, predictedHomeScore: homeScore, predictedAwayScore: awayScore, submittedAt: new Date().toISOString() });
      setSaved(true); setTimeout(() => setSaved(false), 3000);
    } else { setError(result.error ?? 'Save failed.'); }
  };

  const isLiveET  = slot.status === 'ExtraTime';
  const isLivePen = slot.status === 'PenaltyShootout';
  const isLive    = slot.status === 'InProgress' || isLiveET || isLivePen;
  const wonOnPens = slot.penaltyHomeScore !== null && slot.penaltyAwayScore !== null;
  // Match went to AET when: 90-min draw, winner decided, no penalty scores stored
  const wentToAet = hasResult && slot.homeScore === slot.awayScore
                 && slot.winnerTeamId !== null && !wonOnPens;

  const cardBorderColor = unpredicted ? 'var(--secondary)' : isLive ? 'var(--live)' : 'var(--border)';
  const cardOpacity = locked && !teamsKnown ? 0.6 : locked && !isLive ? 0.8 : 1;

  return (
    <div className="rounded-card bg-surface p-4 flex flex-col gap-2.5 border" style={{ borderColor: cardBorderColor, opacity: cardOpacity }}>
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">{slot.slotKey}{slot.venue ? ` · ${slot.venue}` : ''}</span>
        <div className="flex items-center gap-1.5">
          {isLivePen && <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md" style={{ background: 'var(--live-soft)', color: 'var(--live)' }}>PEN</span>}
          {isLiveET  && <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md" style={{ background: 'var(--live-soft)', color: 'var(--live)' }}>ET</span>}
          {isLive && !isLiveET && !isLivePen && <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md" style={{ background: 'var(--live-soft)', color: 'var(--live)' }}>LIVE</span>}
          {locked && !isLive && teamsKnown && <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md bg-surface-3 text-fg-muted">Locked</span>}
          {unpredicted && <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md" style={{ background: 'var(--win-soft)', color: 'var(--win)' }}>Unpredicted</span>}
        </div>
      </div>

      <div className="flex items-center gap-1.5 text-[11px] text-fg-muted">
        <Clock size={12} />{formatKickoff(slot.kickoffUtc)}
      </div>

      {!teamsKnown && <p className="text-fg-muted text-sm italic">TBD vs TBD — bracket not yet set</p>}

      {teamsKnown && (
        <>
          {hasResult && (locked || isLive) && (
            <div className="flex flex-col gap-2 py-1">
              {[
                { id: slot.homeTeamId!, score: slot.homeScore, penScore: slot.penaltyHomeScore, win: slot.winnerTeamId === slot.homeTeamId },
                { id: slot.awayTeamId!, score: slot.awayScore, penScore: slot.penaltyAwayScore, win: slot.winnerTeamId === slot.awayTeamId },
              ].map(({ id, score, penScore, win }) => (
                <div key={id} className={`flex items-center gap-2.5 ${win ? '' : 'opacity-55'}`}>
                  {flagUrl(id) && <img src={flagUrl(id)} alt="" width={22} height={15} className="flag shrink-0" />}
                  <span className={`flex-1 font-mono font-semibold text-sm ${win ? '' : 'text-fg-muted'}`} style={win ? { color: 'var(--win)' } : {}}>{id}</span>
                  <span className={`font-display font-bold tnum ${win ? 'text-fg' : 'text-fg-muted'}`}>
                    {score}
                    {wonOnPens && penScore !== null && (
                      <span className="text-[11px] font-normal text-fg-muted ml-1">({penScore})</span>
                    )}
                  </span>
                </div>
              ))}
              {wentToAet && (
                <p className="text-[11px] text-fg-muted">After extra time</p>
              )}
              {wonOnPens && (
                <p className="text-[11px] text-fg-muted">Won on penalties</p>
              )}
            </div>
          )}

          {!hasResult && locked && (
            <div className="flex flex-col gap-2">
              {[slot.homeTeamId!, slot.awayTeamId!].map(id => (
                <div key={id} className="flex items-center gap-2.5">
                  {flagUrl(id) && <img src={flagUrl(id)} alt="" width={22} height={15} className="flag shrink-0" />}
                  <span className="font-mono font-semibold text-sm text-fg-secondary">{id}</span>
                </div>
              ))}
            </div>
          )}

          {!locked && (
            <form onSubmit={handleSubmit} className="flex flex-col gap-3">
              <div className="flex flex-col gap-2">
                {[
                  { id: slot.homeTeamId!, side: 'home' as const },
                  { id: slot.awayTeamId!, side: 'away' as const },
                ].map(({ id }) => (
                  <label key={id} className="flex items-center gap-2.5 cursor-pointer">
                    <input type="radio" name={`winner-${slot.slotKey}`} value={id}
                      checked={selectedWinner === id} onChange={() => setSelectedWinner(id)}
                      className="accent-pitch-500 shrink-0" />
                    {flagUrl(id) && <img src={flagUrl(id)} alt="" width={22} height={15} className="flag shrink-0" />}
                    <span className="font-mono font-semibold text-fg">{id}</span>
                  </label>
                ))}
              </div>

              <div className="flex items-center gap-2 text-[11px] text-fg-muted">
                <span className="shrink-0">90-min score (optional):</span>
                {[
                  { v: homeInput, set: setHomeInput, label: `${slot.homeTeamId} predicted score` },
                  { v: awayInput, set: setAwayInput, label: `${slot.awayTeamId} predicted score` },
                ].map(({ v, set, label }, i) => (
                  <>
                    {i === 1 && <span className="text-fg-muted font-bold">–</span>}
                    <input key={label} type="number" min={0} max={99} placeholder="—" value={v}
                      onChange={e => set(e.target.value)} aria-label={label}
                      className="w-12 text-center font-display font-bold tnum bg-surface-2 rounded-input py-1 border border-border" />
                  </>
                ))}
              </div>

              <div className="flex justify-end">
                <button type="submit" disabled={saving}
                  className="px-4 py-1.5 rounded-input text-[13px] font-semibold transition-colors disabled:opacity-40"
                  style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}>
                  {saving ? 'Saving…' : prediction !== undefined ? 'Update' : 'Save'}
                </button>
              </div>
            </form>
          )}

          {locked && prediction && (
            <div className="text-[11px] text-fg-muted">
              Your pick:{' '}
              <span className="font-mono font-semibold text-secondary">{prediction.predictedWinnerTeamId}</span>
              {prediction.predictedHomeScore != null && prediction.predictedAwayScore != null && (
                <span className="tnum"> ({prediction.predictedHomeScore}–{prediction.predictedAwayScore})</span>
              )}
            </div>
          )}
        </>
      )}

      {error && <p className="text-[13px] px-3 py-1.5 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>{error}</p>}
      {saved && <p className="text-[13px] px-3 py-1.5 rounded-input" style={{ color: 'var(--win)', background: 'var(--win-soft)' }}>Prediction saved.</p>}
    </div>
  );
}

export function KnockoutBracketPage() {
  const [slots, setSlots] = useState<KnockoutSlotDto[]>([]);
  const [predictions, setPredictions] = useState<Map<string, KnockoutPredictionDto>>(new Map());
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeRound, setActiveRound] = useState<string>('R32');

  useEffect(() => {
    Promise.all([fetchSlots(), fetchPredictions()])
      .then(([slotList, predictionList]) => {
        setSlots(slotList);
        const map = new Map<string, KnockoutPredictionDto>();
        for (const p of predictionList) map.set(p.slotKey, p);
        setPredictions(map);
        const firstRound = ROUND_ORDER.find(r => slotList.some(s => s.round === r));
        if (firstRound) setActiveRound(firstRound);
      })
      .catch((err: unknown) => setLoadError(err instanceof Error ? err.message : 'Failed to load bracket data.'))
      .finally(() => setLoading(false));
  }, []);

  const presentRounds = ROUND_ORDER.filter(r => slots.some(s => s.round === r));
  const activeSlots = slots.filter(s => s.round === activeRound).sort((a, b) => a.slotNumber - b.slotNumber);

  if (loading) return <div className="flex items-center justify-center py-20 text-fg-muted">Loading bracket…</div>;
  if (loadError) return <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>{loadError}</div>;

  return (
    <div className="max-w-3xl mx-auto px-4 py-4">
      <p className="text-[13px] text-fg-secondary mb-3 leading-relaxed">
        Pick the advancing team for each match. Optional 90-min score earns a bonus.
        Predictions lock at kickoff. Slots open once both teams are determined.
      </p>

      <div className="flex gap-1.5 overflow-x-auto appscroll pb-3" role="tablist">
        {presentRounds.map(round => (
          <button key={round} role="tab" aria-selected={activeRound === round}
            onClick={() => setActiveRound(round)}
            className={`px-3.5 py-1.5 rounded-input text-[13px] font-semibold whitespace-nowrap transition-colors ${
              activeRound !== round ? 'bg-surface-3 text-fg-secondary' : ''
            }`}
            style={activeRound === round ? { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' } : undefined}>
            {ROUND_LABELS[round] ?? round}
          </button>
        ))}
      </div>

      <div className="flex flex-col gap-2.5">
        {activeSlots.map(slot => (
          <SlotCard key={slot.slotKey} slot={slot}
            prediction={predictions.get(slot.slotKey)}
            onSaved={updated => setPredictions(prev => new Map(prev).set(updated.slotKey, updated))} />
        ))}
      </div>
    </div>
  );
}
