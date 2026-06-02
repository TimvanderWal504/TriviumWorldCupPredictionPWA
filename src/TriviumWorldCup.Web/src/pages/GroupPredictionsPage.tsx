import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react';

// ── Types ─────────────────────────────────────────────────────────────────────

interface FixtureDto {
  id: string;
  matchNumber: number;
  groupLetter: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  kickoffUtc: string;
  venue: string;
  city: string;
  status: string;
  homeScore: number | null;
  awayScore: number | null;
}

interface GroupPredictionDto {
  fixtureId: string;
  homeScore: number;
  awayScore: number;
  submittedAt: string;
}

// ── API helpers ───────────────────────────────────────────────────────────────

async function fetchFixtures(): Promise<FixtureDto[]> {
  const res = await fetch('/fixtures', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load fixtures (${res.status})`);
  return res.json() as Promise<FixtureDto[]>;
}

async function fetchPredictions(): Promise<GroupPredictionDto[]> {
  const res = await fetch('/predictions/group', { credentials: 'include' });
  if (!res.ok) throw new Error(`Failed to load predictions (${res.status})`);
  return res.json() as Promise<GroupPredictionDto[]>;
}

async function savePrediction(
  fixtureId: string,
  homeScore: number,
  awayScore: number,
  method: 'POST' | 'PUT',
): Promise<{ ok: boolean; error?: string }> {
  const res = await fetch(`/predictions/group/${fixtureId}`, {
    method,
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ homeScore, awayScore }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  if (res.status === 403) return { ok: false, error: 'This match is locked. Predictions closed at kickoff.' };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function isLocked(kickoffUtc: string): boolean {
  return new Date(kickoffUtc).getTime() <= Date.now();
}

function formatKickoff(kickoffUtc: string): string {
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday: 'short',
    month:   'short',
    day:     'numeric',
    hour:    '2-digit',
    minute:  '2-digit',
  });
}

// ── Fixture card ──────────────────────────────────────────────────────────────

interface FixtureCardProps {
  fixture: FixtureDto;
  prediction: GroupPredictionDto | undefined;
  onSaved: (prediction: GroupPredictionDto) => void;
}

function FixtureCard({ fixture, prediction, onSaved }: FixtureCardProps) {
  const locked = isLocked(fixture.kickoffUtc);

  const [homeInput, setHomeInput] = useState<string>(
    prediction !== undefined ? String(prediction.homeScore) : '',
  );
  const [awayInput, setAwayInput] = useState<string>(
    prediction !== undefined ? String(prediction.awayScore) : '',
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  // Keep inputs in sync if prediction changes externally (e.g. initial load)
  useEffect(() => {
    if (prediction !== undefined) {
      setHomeInput(String(prediction.homeScore));
      setAwayInput(String(prediction.awayScore));
    }
  }, [prediction]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSaved(false);

    const home = parseInt(homeInput, 10);
    const away = parseInt(awayInput, 10);

    if (isNaN(home) || isNaN(away)) {
      setError('Enter valid scores.');
      return;
    }
    if (home < 0 || away < 0) {
      setError('Scores must be non-negative.');
      return;
    }

    setSaving(true);
    const method = prediction !== undefined ? 'PUT' : 'POST';
    const result = await savePrediction(fixture.id, home, away, method);
    setSaving(false);

    if (result.ok) {
      onSaved({ fixtureId: fixture.id, homeScore: home, awayScore: away, submittedAt: new Date().toISOString() });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } else {
      setError(result.error ?? 'Save failed.');
    }
  };

  const unpredicted = prediction === undefined && !locked;

  return (
    <div
      className={`rounded-xl border p-4 flex flex-col gap-3 ${
        locked
          ? 'bg-slate-800 border-slate-700 opacity-75'
          : unpredicted
          ? 'bg-slate-800 border-blue-500/50'
          : 'bg-slate-800 border-slate-600'
      }`}
    >
      {/* Header: match info */}
      <div className="flex items-center justify-between text-xs text-slate-400">
        <span>Match {fixture.matchNumber} &middot; {fixture.venue}, {fixture.city}</span>
        <div className="flex items-center gap-2">
          {locked && (
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

      {/* Kickoff time in local timezone */}
      <div className="text-xs text-slate-400">{formatKickoff(fixture.kickoffUtc)}</div>

      {/* Teams and score inputs */}
      <form onSubmit={handleSubmit} className="flex items-center gap-3">
        {/* Home team */}
        <span className="flex-1 text-right text-white font-semibold truncate">
          {fixture.homeTeamName}
        </span>

        {/* Home score */}
        <input
          type="number"
          min={0}
          max={99}
          value={homeInput}
          onChange={(e: ChangeEvent<HTMLInputElement>) => setHomeInput(e.target.value)}
          disabled={locked}
          aria-label={`${fixture.homeTeamName} predicted score`}
          className="w-12 text-center bg-slate-700 text-white rounded-lg px-2 py-1.5 border border-slate-600
                     focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-slate-700/50
                     disabled:text-slate-500 disabled:cursor-not-allowed"
        />

        <span className="text-slate-400 font-bold">–</span>

        {/* Away score */}
        <input
          type="number"
          min={0}
          max={99}
          value={awayInput}
          onChange={(e: ChangeEvent<HTMLInputElement>) => setAwayInput(e.target.value)}
          disabled={locked}
          aria-label={`${fixture.awayTeamName} predicted score`}
          className="w-12 text-center bg-slate-700 text-white rounded-lg px-2 py-1.5 border border-slate-600
                     focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-slate-700/50
                     disabled:text-slate-500 disabled:cursor-not-allowed"
        />

        {/* Away team */}
        <span className="flex-1 text-left text-white font-semibold truncate">
          {fixture.awayTeamName}
        </span>

        {/* Save button — hidden when locked */}
        {!locked && (
          <button
            type="submit"
            disabled={saving}
            className="bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 disabled:cursor-not-allowed
                       text-white text-xs font-semibold rounded-lg px-3 py-1.5 transition-colors shrink-0"
          >
            {saving ? 'Saving…' : prediction !== undefined ? 'Update' : 'Save'}
          </button>
        )}
      </form>

      {/* Inline feedback */}
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
 * Group predictions page — lets the authenticated user predict all 72 group-stage matches.
 * Fixtures are organised by group (A–L). Each match locks at kickoff (server-side enforced).
 * Times are shown in the browser's local timezone.
 */
export function GroupPredictionsPage() {
  const [fixtures, setFixtures] = useState<FixtureDto[]>([]);
  const [predictions, setPredictions] = useState<Map<string, GroupPredictionDto>>(new Map());
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeGroup, setActiveGroup] = useState<string>('A');

  useEffect(() => {
    Promise.all([fetchFixtures(), fetchPredictions()])
      .then(([fixtureList, predictionList]) => {
        setFixtures(fixtureList);
        const map = new Map<string, GroupPredictionDto>();
        for (const p of predictionList) map.set(p.fixtureId, p);
        setPredictions(map);
        // Default to first group that has fixtures
        if (fixtureList.length > 0) setActiveGroup(fixtureList[0].groupLetter);
      })
      .catch((err: unknown) => {
        setLoadError(err instanceof Error ? err.message : 'Failed to load data.');
      })
      .finally(() => setLoading(false));
  }, []);

  const handleSaved = (updated: GroupPredictionDto) => {
    setPredictions(prev => new Map(prev).set(updated.fixtureId, updated));
  };

  // Derive sorted group letters from loaded fixtures
  const groupLetters = [...new Set(fixtures.map(f => f.groupLetter))].sort();
  const activeFixtures = fixtures.filter(f => f.groupLetter === activeGroup);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20 text-slate-400">
        Loading fixtures…
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
      <h1 className="text-2xl font-bold text-white mb-1">Group Stage Predictions</h1>
      <p className="text-slate-400 text-sm mb-6">
        Predict the score for each match. Predictions lock at kickoff.
        Times shown in your local timezone.
      </p>

      {/* Group tabs */}
      <div className="flex flex-wrap gap-1 mb-6" role="tablist">
        {groupLetters.map(letter => (
          <button
            key={letter}
            role="tab"
            aria-selected={activeGroup === letter}
            onClick={() => setActiveGroup(letter)}
            className={`px-3 py-1.5 rounded-lg text-sm font-semibold transition-colors ${
              activeGroup === letter
                ? 'bg-blue-600 text-white'
                : 'bg-slate-700 text-slate-300 hover:bg-slate-600 hover:text-white'
            }`}
          >
            Group {letter}
          </button>
        ))}
      </div>

      {/* Fixture cards for the selected group */}
      <div className="flex flex-col gap-3">
        {activeFixtures.map(fixture => (
          <FixtureCard
            key={fixture.id}
            fixture={fixture}
            prediction={predictions.get(fixture.id)}
            onSaved={handleSaved}
          />
        ))}
      </div>
    </div>
  );
}
