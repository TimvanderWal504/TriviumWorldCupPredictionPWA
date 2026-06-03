import { useEffect, useState, type FormEvent } from 'react';

// ── Types ─────────────────────────────────────────────────────────────────────

interface KnockoutSlotDto {
  slotKey: string;
  round: string;
  slotNumber: number;
  homeTeamId: string | null;
  awayTeamId: string | null;
  kickoffUtc: string | null;
  venue: string | null;
  city: string | null;
  status: string;
  homeScore: number | null;
  awayScore: number | null;
  winnerTeamId: string | null;
}

interface KnockoutPredictionDto {
  slotKey: string;
  predictedWinnerTeamId: string;
  predictedHomeScore: number | null;
  predictedAwayScore: number | null;
  submittedAt: string;
}

// ── API helpers ───────────────────────────────────────────────────────────────

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
  slotKey: string,
  predictedWinnerTeamId: string,
  predictedHomeScore: number | null,
  predictedAwayScore: number | null,
  method: 'POST' | 'PUT',
): Promise<{ ok: boolean; error?: string }> {
  const res = await fetch(`/predictions/knockout/${slotKey}`, {
    method,
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ predictedWinnerTeamId, predictedHomeScore, predictedAwayScore }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  if (res.status === 403) return { ok: false, error: 'This match is locked. Predictions closed at kickoff.' };
  if (res.status === 422) return { ok: false, error: body.error ?? 'Bracket not yet resolved.' };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const ROUND_ORDER = ['R32', 'R16', 'QF', 'SF', 'ThirdPlace', 'Final'];

const ROUND_LABELS: Record<string, string> = {
  R32: 'Round of 32',
  R16: 'Round of 16',
  QF: 'Quarter-finals',
  SF: 'Semi-finals',
  ThirdPlace: 'Third-place Play-off',
  Final: 'Final',
};

function isLocked(kickoffUtc: string | null): boolean {
  if (!kickoffUtc) return true;
  return new Date(kickoffUtc).getTime() <= Date.now();
}

function formatKickoff(kickoffUtc: string | null): string {
  if (!kickoffUtc) return 'TBD';
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

// ── Slot card ─────────────────────────────────────────────────────────────────

interface SlotCardProps {
  slot: KnockoutSlotDto;
  prediction: KnockoutPredictionDto | undefined;
  onSaved: (updated: KnockoutPredictionDto) => void;
}

function SlotCard({ slot, prediction, onSaved }: SlotCardProps) {
  const locked = isLocked(slot.kickoffUtc);
  const teamsKnown = slot.homeTeamId !== null && slot.awayTeamId !== null;

  const [selectedWinner, setSelectedWinner] = useState<string>(
    prediction?.predictedWinnerTeamId ?? '',
  );
  const [homeInput, setHomeInput] = useState<string>(
    prediction?.predictedHomeScore != null ? String(prediction.predictedHomeScore) : '',
  );
  const [awayInput, setAwayInput] = useState<string>(
    prediction?.predictedAwayScore != null ? String(prediction.predictedAwayScore) : '',
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  // Sync if prediction changes (e.g. on initial load)
  useEffect(() => {
    if (prediction) {
      setSelectedWinner(prediction.predictedWinnerTeamId);
      setHomeInput(prediction.predictedHomeScore != null ? String(prediction.predictedHomeScore) : '');
      setAwayInput(prediction.predictedAwayScore != null ? String(prediction.predictedAwayScore) : '');
    }
  }, [prediction]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSaved(false);

    if (!selectedWinner) {
      setError('Select the team you predict will advance.');
      return;
    }

    const homeScore = homeInput !== '' ? parseInt(homeInput, 10) : null;
    const awayScore = awayInput !== '' ? parseInt(awayInput, 10) : null;

    if (homeInput !== '' && (isNaN(homeScore!) || homeScore! < 0)) {
      setError('Home score must be a non-negative number.');
      return;
    }
    if (awayInput !== '' && (isNaN(awayScore!) || awayScore! < 0)) {
      setError('Away score must be a non-negative number.');
      return;
    }

    setSaving(true);
    const method = prediction !== undefined ? 'PUT' : 'POST';
    const result = await savePrediction(slot.slotKey, selectedWinner, homeScore, awayScore, method);
    setSaving(false);

    if (result.ok) {
      onSaved({
        slotKey: slot.slotKey,
        predictedWinnerTeamId: selectedWinner,
        predictedHomeScore: homeScore,
        predictedAwayScore: awayScore,
        submittedAt: new Date().toISOString(),
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } else {
      setError(result.error ?? 'Save failed.');
    }
  };

  const hasResult = slot.homeScore !== null && slot.awayScore !== null;
  const unpredicted = !prediction && !locked && teamsKnown;

  return (
    <div
      className={`rounded-xl border p-4 flex flex-col gap-3 ${
        locked
          ? 'bg-slate-800 border-slate-700 opacity-80'
          : unpredicted
          ? 'bg-slate-800 border-blue-500/50'
          : 'bg-slate-800 border-slate-600'
      }`}
    >
      {/* Header */}
      <div className="flex items-center justify-between text-xs text-slate-400">
        <span>
          {slot.slotKey}
          {slot.venue ? ` · ${slot.venue}, ${slot.city}` : ''}
        </span>
        <div className="flex items-center gap-2">
          {locked && teamsKnown && (
            <span className="bg-slate-600 text-slate-300 text-xs font-semibold px-2 py-0.5 rounded">
              Locked
            </span>
          )}
          {unpredicted && (
            <span className="bg-blue-900/60 text-blue-300 text-xs font-semibold px-2 py-0.5 rounded">
              Unpredicted
            </span>
          )}
        </div>
      </div>

      {/* Kickoff */}
      <div className="text-xs text-slate-400">{formatKickoff(slot.kickoffUtc)}</div>

      {/* Teams not yet determined */}
      {!teamsKnown && (
        <p className="text-slate-500 text-sm italic">TBD vs TBD — bracket not yet set</p>
      )}

      {/* Teams are known */}
      {teamsKnown && (
        <>
          {/* Result (if available) */}
          {hasResult && locked && (
            <div className="flex items-center justify-center gap-2 text-sm">
              <span className={`font-semibold ${slot.winnerTeamId === slot.homeTeamId ? 'text-green-400' : 'text-white'}`}>
                {slot.homeTeamId}
              </span>
              <span className="text-slate-300 font-bold">
                {slot.homeScore} – {slot.awayScore}
              </span>
              <span className={`font-semibold ${slot.winnerTeamId === slot.awayTeamId ? 'text-green-400' : 'text-white'}`}>
                {slot.awayTeamId}
              </span>
            </div>
          )}

          {/* Score (no result yet but locked) */}
          {!hasResult && locked && (
            <div className="flex items-center justify-center gap-2 text-sm text-slate-400">
              <span>{slot.homeTeamId}</span>
              <span className="font-bold">vs</span>
              <span>{slot.awayTeamId}</span>
            </div>
          )}

          {/* Prediction form — editable when not locked */}
          {!locked && (
            <form onSubmit={handleSubmit} className="flex flex-col gap-3">
              {/* Winner selection */}
              <div className="flex items-center gap-3">
                <label className="flex items-center gap-2 flex-1 cursor-pointer">
                  <input
                    type="radio"
                    name={`winner-${slot.slotKey}`}
                    value={slot.homeTeamId ?? ''}
                    checked={selectedWinner === slot.homeTeamId}
                    onChange={() => setSelectedWinner(slot.homeTeamId!)}
                    className="accent-blue-500"
                  />
                  <span className="text-white font-semibold">{slot.homeTeamId}</span>
                </label>

                <span className="text-slate-400 font-bold text-sm">vs</span>

                <label className="flex items-center gap-2 flex-1 cursor-pointer justify-end">
                  <span className="text-white font-semibold">{slot.awayTeamId}</span>
                  <input
                    type="radio"
                    name={`winner-${slot.slotKey}`}
                    value={slot.awayTeamId ?? ''}
                    checked={selectedWinner === slot.awayTeamId}
                    onChange={() => setSelectedWinner(slot.awayTeamId!)}
                    className="accent-blue-500"
                  />
                </label>
              </div>

              {/* Optional score prediction */}
              <div className="flex items-center gap-2 text-xs text-slate-400">
                <span className="shrink-0">90-min score (optional):</span>
                <input
                  type="number"
                  min={0}
                  max={99}
                  placeholder="—"
                  value={homeInput}
                  onChange={e => setHomeInput(e.target.value)}
                  aria-label={`${slot.homeTeamId} predicted score`}
                  className="w-12 text-center bg-slate-700 text-white rounded-lg px-2 py-1
                             border border-slate-600 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <span className="text-slate-400 font-bold">–</span>
                <input
                  type="number"
                  min={0}
                  max={99}
                  placeholder="—"
                  value={awayInput}
                  onChange={e => setAwayInput(e.target.value)}
                  aria-label={`${slot.awayTeamId} predicted score`}
                  className="w-12 text-center bg-slate-700 text-white rounded-lg px-2 py-1
                             border border-slate-600 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>

              <button
                type="submit"
                disabled={saving}
                className="self-end bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800
                           disabled:cursor-not-allowed text-white text-xs font-semibold
                           rounded-lg px-3 py-1.5 transition-colors"
              >
                {saving ? 'Saving…' : prediction !== undefined ? 'Update' : 'Save'}
              </button>
            </form>
          )}

          {/* Read-only prediction when locked */}
          {locked && prediction && (
            <div className="text-xs text-slate-400 mt-1">
              Your pick:{' '}
              <span className="text-white font-semibold">{prediction.predictedWinnerTeamId}</span>
              {prediction.predictedHomeScore != null && prediction.predictedAwayScore != null && (
                <span>
                  {' '}({prediction.predictedHomeScore}–{prediction.predictedAwayScore})
                </span>
              )}
            </div>
          )}
        </>
      )}

      {/* Feedback */}
      {error && (
        <p className="text-red-400 text-xs bg-red-950/40 rounded px-3 py-1.5">{error}</p>
      )}
      {saved && (
        <p className="text-green-400 text-xs bg-green-950/40 rounded px-3 py-1.5">Prediction saved.</p>
      )}
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

/**
 * Knockout bracket prediction page.
 * Shows all rounds R32 → R16 → QF → SF → 3rd place → Final.
 * Each slot is predictable once both teams are known and before kickoff.
 * Predictions lock at kickoff (server-side enforced).
 */
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
        // Default to the first round that has slots
        if (slotList.length > 0) {
          const firstRound = ROUND_ORDER.find(r => slotList.some(s => s.round === r));
          if (firstRound) setActiveRound(firstRound);
        }
      })
      .catch((err: unknown) => {
        setLoadError(err instanceof Error ? err.message : 'Failed to load bracket data.');
      })
      .finally(() => setLoading(false));
  }, []);

  const handleSaved = (updated: KnockoutPredictionDto) => {
    setPredictions(prev => new Map(prev).set(updated.slotKey, updated));
  };

  // Rounds present in the slot data, in canonical order
  const presentRounds = ROUND_ORDER.filter(r => slots.some(s => s.round === r));
  const activeSlots = slots
    .filter(s => s.round === activeRound)
    .sort((a, b) => a.slotNumber - b.slotNumber);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20 text-slate-400">
        Loading bracket…
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="flex items-center justify-center py-20 text-red-400">
        {loadError}
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto px-4 py-6">
      <h1 className="text-2xl font-bold text-white mb-1">Knockout Bracket</h1>
      <p className="text-slate-400 text-sm mb-6">
        Pick the advancing team for each match. An optional 90-minute score can also be predicted.
        Predictions lock at kickoff. Slots open once both teams are determined.
      </p>

      {/* Round tabs */}
      <div className="flex flex-wrap gap-1 mb-6" role="tablist">
        {presentRounds.map(round => (
          <button
            key={round}
            role="tab"
            aria-selected={activeRound === round}
            onClick={() => setActiveRound(round)}
            className={`px-3 py-1.5 rounded-lg text-sm font-semibold transition-colors ${
              activeRound === round
                ? 'bg-blue-600 text-white'
                : 'bg-slate-700 text-slate-300 hover:bg-slate-600 hover:text-white'
            }`}
          >
            {ROUND_LABELS[round] ?? round}
          </button>
        ))}
      </div>

      {/* Slot cards for selected round */}
      <div className="flex flex-col gap-3">
        {activeSlots.map(slot => (
          <SlotCard
            key={slot.slotKey}
            slot={slot}
            prediction={predictions.get(slot.slotKey)}
            onSaved={handleSaved}
          />
        ))}
      </div>
    </div>
  );
}
