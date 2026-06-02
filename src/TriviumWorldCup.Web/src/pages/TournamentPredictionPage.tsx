import { useEffect, useState, type FormEvent } from 'react';

// ── Types ─────────────────────────────────────────────────────────────────────

interface Team {
  id: string;
  name: string;
  fifaCode: string;
  countryCode: string;
}

interface Player {
  id: string;
  name: string;
  teamId: string;
  teamName: string;
  position: string;
  shirtNumber: number | null;
}

interface TournamentPrediction {
  championTeamId: string | null;
  goldenSixPlayerIds: string[];
  submittedAt: string;
}

// ── Lock hint ─────────────────────────────────────────────────────────────────
// The server enforces the real lock. This client-side date is only used to show
// the locked banner — the server will reject any POST/PUT after first kickoff.
const FIRST_KICKOFF = new Date('2026-06-11T19:00:00Z');

// ── API helpers ───────────────────────────────────────────────────────────────

async function fetchTeams(): Promise<Team[]> {
  const res = await fetch('/teams', { credentials: 'include' });
  if (!res.ok) return [];
  return res.json() as Promise<Team[]>;
}

async function fetchPlayers(): Promise<Player[]> {
  const res = await fetch('/players', { credentials: 'include' });
  if (!res.ok) return [];
  return res.json() as Promise<Player[]>;
}

async function fetchPrediction(): Promise<TournamentPrediction | null> {
  const res = await fetch('/predictions/tournament', { credentials: 'include' });
  if (res.status === 404) return null;
  if (res.ok) return res.json() as Promise<TournamentPrediction>;
  return null;
}

async function savePrediction(
  method: 'POST' | 'PUT',
  championTeamId: string,
  goldenSixPlayerIds: string[],
): Promise<{ ok: boolean; error?: string }> {
  const res = await fetch('/predictions/tournament', {
    method,
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ championTeamId, goldenSixPlayerIds }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  if (res.status === 403) return { ok: false, error: 'Predictions are locked — the tournament has started.' };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

// ── Component ─────────────────────────────────────────────────────────────────

/**
 * Tournament prediction screen — champion team + Golden Six top scorers.
 * Editable until the first kickoff; locked server-side thereafter.
 */
export function TournamentPredictionPage() {
  const [teams, setTeams]     = useState<Team[]>([]);
  const [players, setPlayers] = useState<Player[]>([]);
  const [loading, setLoading] = useState(true);

  // Existing prediction (if any)
  const [existingPrediction, setExistingPrediction] = useState<TournamentPrediction | null>(null);

  // Form state
  const [championTeamId, setChampionTeamId]           = useState<string>('');
  const [selectedPlayers, setSelectedPlayers]         = useState<Player[]>([]);

  // Search state
  const [teamSearch, setTeamSearch]       = useState('');
  const [playerSearch, setPlayerSearch]   = useState('');

  // Submission state
  const [saving, setSaving]         = useState(false);
  const [saveError, setSaveError]   = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  // Client-side lock hint (server enforces the real lock)
  const isLocked = new Date() >= FIRST_KICKOFF;

  useEffect(() => {
    Promise.all([fetchTeams(), fetchPlayers(), fetchPrediction()]).then(
      ([teamsData, playersData, prediction]) => {
        setTeams(teamsData);
        setPlayers(playersData);
        if (prediction) {
          setExistingPrediction(prediction);
          setChampionTeamId(prediction.championTeamId ?? '');
          // Hydrate selected players from IDs
          const selectedById = prediction.goldenSixPlayerIds
            .map(id => playersData.find(p => p.id === id))
            .filter((p): p is Player => p !== undefined);
          setSelectedPlayers(selectedById);
        }
      },
    ).finally(() => setLoading(false));
  }, []);

  // ── Filtered lists ──────────────────────────────────────────────────────────

  const filteredTeams = teams.filter(t =>
    t.name.toLowerCase().includes(teamSearch.toLowerCase()) ||
    t.fifaCode.toLowerCase().includes(teamSearch.toLowerCase()),
  );

  const selectedPlayerIds = new Set(selectedPlayers.map(p => p.id));

  const filteredPlayers = players.filter(p => {
    if (selectedPlayerIds.has(p.id)) return false;
    const q = playerSearch.toLowerCase();
    return (
      p.name.toLowerCase().includes(q) ||
      p.teamName.toLowerCase().includes(q) ||
      p.position.toLowerCase().includes(q)
    );
  });

  // ── Player selection ────────────────────────────────────────────────────────

  function addPlayer(player: Player) {
    if (selectedPlayers.length >= 6) return;
    setSelectedPlayers(prev => [...prev, player]);
  }

  function removePlayer(playerId: string) {
    setSelectedPlayers(prev => prev.filter(p => p.id !== playerId));
  }

  // ── Submit ──────────────────────────────────────────────────────────────────

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaveError(null);
    setSaveSuccess(false);

    if (!championTeamId) {
      setSaveError('Please select a champion team.');
      return;
    }
    if (selectedPlayers.length !== 6) {
      setSaveError('Please select exactly 6 players for the Golden Six.');
      return;
    }

    setSaving(true);
    const method = existingPrediction ? 'PUT' : 'POST';
    const result = await savePrediction(method, championTeamId, selectedPlayers.map(p => p.id));
    setSaving(false);

    if (result.ok) {
      setExistingPrediction({
        championTeamId,
        goldenSixPlayerIds: selectedPlayers.map(p => p.id),
        submittedAt: new Date().toISOString(),
      });
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 4000);
    } else {
      setSaveError(result.error ?? 'Save failed.');
    }
  }

  // ── Render ──────────────────────────────────────────────────────────────────

  if (loading) {
    return <div className="p-8 text-slate-400">Loading tournament data…</div>;
  }

  const championTeam = teams.find(t => t.id === championTeamId);

  return (
    <div className="max-w-2xl mx-auto p-6 space-y-8">
      <h1 className="text-2xl font-bold text-white">Tournament Predictions</h1>

      {/* Locked banner */}
      {isLocked && (
        <div className="bg-amber-950/60 border border-amber-700 text-amber-300 rounded-lg px-4 py-3 text-sm font-medium">
          Locked — predictions closed. The tournament has started.
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-8">

        {/* ── Section 1: Champion ─────────────────────────────────────────── */}
        <section>
          <h2 className="text-lg font-semibold text-white mb-3">Champion</h2>
          <p className="text-slate-400 text-sm mb-3">Select the team you predict will win the World Cup.</p>

          <input
            type="text"
            value={teamSearch}
            onChange={e => setTeamSearch(e.target.value)}
            placeholder="Search teams…"
            disabled={isLocked}
            className="w-full bg-slate-700 text-white rounded-lg px-4 py-2.5 border border-slate-600
                       focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder:text-slate-500 mb-2
                       disabled:opacity-50 disabled:cursor-not-allowed"
          />

          {championTeamId && championTeam && (
            <div className="mb-2 flex items-center gap-2 text-sm">
              <span className="text-slate-400">Selected:</span>
              <span className="text-white font-medium">{championTeam.fifaCode} — {championTeam.name}</span>
              {!isLocked && (
                <button
                  type="button"
                  onClick={() => setChampionTeamId('')}
                  className="text-slate-500 hover:text-red-400 transition-colors text-xs ml-1"
                >
                  Clear
                </button>
              )}
            </div>
          )}

          <div className="max-h-56 overflow-y-auto rounded-lg border border-slate-600 bg-slate-800">
            {filteredTeams.length === 0 ? (
              <p className="text-slate-500 text-sm px-4 py-3">No teams match.</p>
            ) : (
              filteredTeams.map(team => (
                <button
                  key={team.id}
                  type="button"
                  disabled={isLocked}
                  onClick={() => setChampionTeamId(team.id)}
                  className={`w-full text-left px-4 py-2.5 text-sm flex items-center gap-3 transition-colors
                    disabled:opacity-50 disabled:cursor-not-allowed
                    ${team.id === championTeamId
                      ? 'bg-blue-700/40 text-white'
                      : 'text-slate-300 hover:bg-slate-700 hover:text-white'}`}
                >
                  <span className="font-mono text-xs text-slate-400 w-8">{team.fifaCode}</span>
                  <span>{team.name}</span>
                </button>
              ))
            )}
          </div>
        </section>

        {/* ── Section 2: Golden Six ───────────────────────────────────────── */}
        <section>
          <h2 className="text-lg font-semibold text-white mb-1">Golden Six</h2>
          <p className="text-slate-400 text-sm mb-3">
            Select exactly 6 players you predict will be the top scorers.
            <span className={`ml-2 font-medium ${selectedPlayers.length === 6 ? 'text-green-400' : 'text-amber-400'}`}>
              {selectedPlayers.length}/6 selected
            </span>
          </p>

          {/* Selected players list */}
          {selectedPlayers.length > 0 && (
            <div className="mb-3 space-y-1.5">
              {selectedPlayers.map((player, idx) => (
                <div
                  key={player.id}
                  className="flex items-center gap-3 bg-blue-900/30 border border-blue-700/40 rounded-lg px-3 py-2 text-sm"
                >
                  <span className="text-slate-500 w-4 text-xs">{idx + 1}.</span>
                  <span className="text-white font-medium flex-1">{player.name}</span>
                  <span className="text-slate-400 text-xs">{player.teamName}</span>
                  <span className="text-xs font-mono px-1.5 py-0.5 rounded bg-slate-700 text-slate-300">
                    {player.position}
                  </span>
                  {!isLocked && (
                    <button
                      type="button"
                      onClick={() => removePlayer(player.id)}
                      className="text-slate-500 hover:text-red-400 transition-colors ml-1 text-xs"
                      aria-label={`Remove ${player.name}`}
                    >
                      Remove
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Player search input */}
          {!isLocked && selectedPlayers.length < 6 && (
            <>
              <input
                type="text"
                value={playerSearch}
                onChange={e => setPlayerSearch(e.target.value)}
                placeholder="Search players by name, team, or position…"
                className="w-full bg-slate-700 text-white rounded-lg px-4 py-2.5 border border-slate-600
                           focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder:text-slate-500 mb-2"
              />

              <div className="max-h-64 overflow-y-auto rounded-lg border border-slate-600 bg-slate-800">
                {filteredPlayers.length === 0 ? (
                  <p className="text-slate-500 text-sm px-4 py-3">
                    {playerSearch ? 'No players match.' : 'Type to search players.'}
                  </p>
                ) : (
                  filteredPlayers.slice(0, 50).map(player => (
                    <button
                      key={player.id}
                      type="button"
                      onClick={() => addPlayer(player)}
                      disabled={selectedPlayers.length >= 6}
                      className="w-full text-left px-4 py-2.5 text-sm flex items-center gap-3
                                 text-slate-300 hover:bg-slate-700 hover:text-white transition-colors
                                 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <span className="flex-1 font-medium">{player.name}</span>
                      <span className="text-slate-400 text-xs">{player.teamName}</span>
                      <span className="text-xs font-mono px-1.5 py-0.5 rounded bg-slate-700 text-slate-300">
                        {player.position}
                      </span>
                    </button>
                  ))
                )}
              </div>
            </>
          )}
        </section>

        {/* ── Feedback ─────────────────────────────────────────────────────── */}
        {saveError && (
          <p className="text-red-400 text-sm bg-red-950/40 rounded-lg px-4 py-2">{saveError}</p>
        )}
        {saveSuccess && (
          <p className="text-green-400 text-sm bg-green-950/40 rounded-lg px-4 py-2">
            {existingPrediction ? 'Prediction saved.' : 'Prediction created.'}
          </p>
        )}

        {/* ── Save button ────────────────────────────────────────────────── */}
        {!isLocked && (
          <button
            type="submit"
            disabled={saving}
            className="bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 disabled:cursor-not-allowed
                       text-white font-semibold rounded-lg px-6 py-2.5 transition-colors"
          >
            {saving ? 'Saving…' : existingPrediction ? 'Update prediction' : 'Save prediction'}
          </button>
        )}
      </form>
    </div>
  );
}
