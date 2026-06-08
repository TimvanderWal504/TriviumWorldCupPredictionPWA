import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';

interface Team { id: string; name: string; fifaCode: string; countryCode: string; }
interface Player { id: string; name: string; teamId: string; teamName: string; position: string; shirtNumber: number | null; }
interface TournamentPrediction { championTeamId: string | null; goldenSixPlayerIds: string[]; submittedAt: string; }

const FIRST_KICKOFF = new Date('2026-06-11T19:00:00Z');

// Position display order: FWD → MID → DEF → GK
const POS_RANK: Record<string, number> = {
  FWD: 0, Forward: 0, Striker: 0,
  MID: 1, Midfielder: 1,
  DEF: 2, Defender: 2,
  GK: 3, Goalkeeper: 3,
};
function posRank(pos: string): number { return POS_RANK[pos] ?? 99; }

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
  method: 'POST' | 'PUT', championTeamId: string, goldenSixPlayerIds: string[],
): Promise<{ ok: boolean; error?: string }> {
  const res = await fetch('/predictions/tournament', {
    method,
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ championTeamId, goldenSixPlayerIds }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  if (res.status === 403) return { ok: false, error: 'Predictions are locked. The tournament has started.' };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return <h2 className="font-display font-bold text-lg tracking-tight mb-1">{children}</h2>;
}

// ── Team grid + expandable player panel ─────────────────────────────────────

interface TeamGridProps {
  teams: Team[];
  players: Player[];
  selectedPlayers: Player[];
  onAdd: (p: Player) => void;
  onRemove: (id: string) => void;
  disabled: boolean;
}

function TeamGrid({ teams, players, selectedPlayers, onAdd, onRemove, disabled }: TeamGridProps) {
  const [expandedTeamId, setExpandedTeamId] = useState<string | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!expandedTeamId || !panelRef.current) return;
    const el = panelRef.current;
    requestAnimationFrame(() => {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }, [expandedTeamId]);

  const selectedIds = new Set(selectedPlayers.map(p => p.id));
  const full = selectedPlayers.length >= 6;

  function toggleTeam(id: string) {
    setExpandedTeamId(prev => prev === id ? null : id);
  }

  function teamPlayerCount(teamId: string): number {
    return selectedPlayers.filter(p => p.teamId === teamId).length;
  }

  const sortedTeams = [...teams].sort((a, b) => a.fifaCode.localeCompare(b.fifaCode));

  return (
    <div className="space-y-2">
      {/* Team grid */}
      <div className="grid grid-cols-4 gap-1.5">
        {sortedTeams.map(team => {
          const count = teamPlayerCount(team.id);
          const isOpen = expandedTeamId === team.id;
          const url = flagUrl(team.fifaCode);

          return (
            <button
              key={team.id}
              type="button"
              disabled={disabled && count === 0}
              onClick={() => toggleTeam(team.id)}
              className={`relative flex flex-col items-center gap-1 py-2.5 px-1 rounded-card border transition-colors text-center ${
                isOpen
                  ? 'border-secondary bg-blue-500/10'
                  : count > 0
                  ? 'border-pitch-500/60 bg-pitch-500/8'
                  : 'border-border bg-surface hover:bg-surface-2'
              } disabled:opacity-40 disabled:cursor-not-allowed`}
            >
              {url
                ? <img src={url} alt={team.fifaCode} width={32} height={22} className="flag" />
                : <span className="w-8 h-5 bg-surface-3 rounded inline-block" />}
              <span className="font-mono text-[11px] font-semibold text-fg leading-none">{team.fifaCode}</span>
              {count > 0 && (
                <span
                  className="absolute top-1 right-1 font-display font-bold text-[10px] w-4 h-4 rounded-chip grid place-items-center"
                  style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
                >
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Expanded player panel */}
      {expandedTeamId && (() => {
        const team = teams.find(t => t.id === expandedTeamId);
        if (!team) return null;
        const teamPlayers = players
          .filter(p => p.teamId === expandedTeamId)
          .sort((a, b) => posRank(a.position) - posRank(b.position) || a.name.localeCompare(b.name));

        return (
          <div ref={panelRef} className="rounded-card border border-secondary/50 bg-surface overflow-hidden scroll-mt-20">
            {/* Panel header */}
            <div className="flex items-center justify-between px-4 py-2.5 border-b border-border bg-surface-2">
              <div className="flex items-center gap-2">
                {flagUrl(team.fifaCode) && (
                  <img src={flagUrl(team.fifaCode)} alt="" width={24} height={16} className="flag" />
                )}
                <span className="font-display font-bold text-[15px] tracking-tight">{team.name}</span>
                <span className="font-mono text-[11px] text-fg-muted">{team.fifaCode}</span>
              </div>
              <button
                type="button"
                onClick={() => setExpandedTeamId(null)}
                className="text-fg-muted hover:text-fg transition-colors p-1"
                aria-label="Close"
              >
                <ChevronDown size={16} />
              </button>
            </div>

            {/* Player rows */}
            {teamPlayers.length === 0
              ? <p className="text-fg-muted text-sm px-4 py-3">No players found for this team.</p>
              : teamPlayers.map(player => {
                  const alreadySelected = selectedIds.has(player.id);
                  const canAdd = !alreadySelected && !full;
                  return (
                    <div
                      key={player.id}
                      className={`flex items-center gap-3 px-4 py-2.5 border-b border-border last:border-0 transition-colors ${
                        alreadySelected ? 'bg-pitch-500/8' : 'hover:bg-surface-2'
                      }`}
                    >
                      <span className="font-medium text-[13px] text-fg flex-1 min-w-0 truncate">{player.name}</span>
                      <span
                        className="font-mono text-[11px] font-semibold px-1.5 py-0.5 rounded-chip shrink-0"
                        style={{
                          background: posRank(player.position) === 0 ? 'var(--live-soft)'
                            : posRank(player.position) === 1 ? 'var(--win-soft)'
                            : posRank(player.position) === 2 ? 'var(--warning-soft)'
                            : 'var(--surface-3)',
                          color: posRank(player.position) === 0 ? 'var(--live)'
                            : posRank(player.position) === 1 ? 'var(--win)'
                            : posRank(player.position) === 2 ? 'var(--warning)'
                            : 'var(--fg-muted)',
                        }}
                      >
                        {player.position}
                      </span>
                      {!disabled && (
                        alreadySelected
                          ? <button
                              type="button"
                              onClick={() => onRemove(player.id)}
                              className="text-[11px] font-semibold transition-colors shrink-0"
                              style={{ color: 'var(--win)' }}
                            >
                              ✓ Remove
                            </button>
                          : <button
                              type="button"
                              disabled={!canAdd}
                              onClick={() => onAdd(player)}
                              className="text-[11px] font-semibold transition-colors shrink-0 disabled:opacity-30"
                              style={{ color: canAdd ? 'var(--secondary)' : undefined }}
                            >
                              + Add
                            </button>
                      )}
                    </div>
                  );
                })}
          </div>
        );
      })()}
    </div>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

export function TournamentPredictionPage() {
  const [teams, setTeams] = useState<Team[]>([]);
  const [players, setPlayers] = useState<Player[]>([]);
  const [loading, setLoading] = useState(true);
  const [existingPrediction, setExistingPrediction] = useState<TournamentPrediction | null>(null);
  const [championTeamId, setChampionTeamId] = useState('');
  const [selectedPlayers, setSelectedPlayers] = useState<Player[]>([]);
  const [teamSearch, setTeamSearch] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const isLocked = new Date() >= FIRST_KICKOFF;

  useEffect(() => {
    Promise.all([fetchTeams(), fetchPlayers(), fetchPrediction()]).then(([t, p, pred]) => {
      setTeams(t);
      setPlayers(p);
      if (pred) {
        setExistingPrediction(pred);
        setChampionTeamId(pred.championTeamId ?? '');
        setSelectedPlayers(pred.goldenSixPlayerIds.map(id => p.find(pl => pl.id === id)).filter((pl): pl is Player => pl !== undefined));
      }
    }).finally(() => setLoading(false));
  }, []);

  const filteredTeams = teams.filter(t =>
    t.name.toLowerCase().includes(teamSearch.toLowerCase()) ||
    t.fifaCode.toLowerCase().includes(teamSearch.toLowerCase())
  );

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSaveError(null);
    setSaveSuccess(false);
    if (!championTeamId) { setSaveError('Please select a champion team.'); return; }
    if (selectedPlayers.length !== 6) { setSaveError('Please select exactly 6 players for the Golden Six.'); return; }
    setSaving(true);
    const result = await savePrediction(existingPrediction ? 'PUT' : 'POST', championTeamId, selectedPlayers.map(p => p.id));
    setSaving(false);
    if (result.ok) {
      setExistingPrediction({ championTeamId, goldenSixPlayerIds: selectedPlayers.map(p => p.id), submittedAt: new Date().toISOString() });
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 4000);
    } else {
      setSaveError(result.error ?? 'Save failed.');
    }
  }

  if (loading) return <div className="p-8 text-fg-muted">Loading tournament data…</div>;

  const championTeam = teams.find(t => t.id === championTeamId);

  return (
    <div className="max-w-2xl mx-auto px-4 py-4 space-y-6">
      <div className="rounded-card bg-surface border border-border px-4 py-3 text-[13px] text-fg-secondary leading-relaxed">
        Select the team you predict will win the World Cup, and a Golden Six of top scorers.
        Predictions lock at first kickoff.
      </div>

      {isLocked && (
        <div className="rounded-input px-4 py-3 text-sm font-medium border"
             style={{ background: 'var(--warning-soft)', borderColor: 'transparent', color: 'var(--warning)' }}>
          Locked. Predictions closed. The tournament has started.
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">

        {/* ── Champion ─────────────────────────────────────────────────────── */}
        <section className="rounded-card bg-surface border border-border p-5 space-y-3">
          <SectionLabel>Champion</SectionLabel>
          <p className="text-fg-secondary text-[13px] mb-3">Select the team you predict will win the World Cup.</p>

          <input type="text" value={teamSearch} onChange={e => setTeamSearch(e.target.value)}
            placeholder="Search teams…" disabled={isLocked}
            className="w-full bg-surface-2 text-fg rounded-input px-4 py-2.5 border border-border placeholder:text-fg-muted mb-2 disabled:opacity-50" />

          {championTeam && (
            <div className="mb-2 flex items-center gap-2 text-sm">
              <span className="text-fg-muted">Selected:</span>
              {flagUrl(championTeam.fifaCode) && (
                <img src={flagUrl(championTeam.fifaCode)} alt="" width={22} height={15} className="flag" />
              )}
              <span className="font-mono font-semibold text-secondary">{championTeam.fifaCode}</span>
              <span className="font-medium text-fg">{championTeam.name}</span>
              {!isLocked && (
                <button type="button" onClick={() => setChampionTeamId('')}
                  className="text-fg-muted hover:text-fg-secondary transition-colors text-xs ml-1">
                  Clear
                </button>
              )}
            </div>
          )}

          <div className="max-h-56 overflow-y-auto appscroll rounded-card border border-border bg-surface">
            {filteredTeams.length === 0
              ? <p className="text-fg-muted text-sm px-4 py-3">No teams match.</p>
              : filteredTeams.map(team => (
                  <button key={team.id} type="button" disabled={isLocked}
                    onClick={() => setChampionTeamId(team.id)}
                    className={`w-full text-left px-4 py-2.5 text-sm flex items-center gap-3 transition-colors disabled:opacity-50 ${
                      team.id === championTeamId ? 'bg-blue-500/15' : 'hover:bg-surface-2'
                    }`}>
                    {flagUrl(team.fifaCode) && (
                      <img src={flagUrl(team.fifaCode)} alt="" width={22} height={15} className="flag shrink-0" />
                    )}
                    <span className="font-mono text-xs text-fg-muted w-8">{team.fifaCode}</span>
                    <span className={`font-medium ${team.id === championTeamId ? 'text-secondary' : 'text-fg'}`}>{team.name}</span>
                  </button>
                ))}
          </div>
        </section>

        {/* ── Golden Six ───────────────────────────────────────────────────── */}
        <section className="rounded-card bg-surface border border-border p-5 space-y-3">
          <div className="flex items-center justify-between mb-1">
            <SectionLabel>Golden Six</SectionLabel>
            <span className="font-display font-bold text-[13px] tnum"
                  style={{ color: selectedPlayers.length === 6 ? 'var(--win)' : 'var(--warning)' }}>
              {selectedPlayers.length}/6
            </span>
          </div>
          <p className="text-fg-secondary text-[13px] mb-3">
            Select exactly 6 players you predict will be the top scorers. Pick a national team below to see its squad.
          </p>

          {/* Selected players */}
          {selectedPlayers.length > 0 && (
            <div className="mb-4 space-y-1.5">
              {selectedPlayers.map((player, idx) => (
                <div key={player.id} className="flex items-center gap-3 rounded-input px-3 py-2 text-sm border"
                     style={{ background: 'var(--win-soft)', borderColor: 'transparent' }}>
                  <span className="text-fg-muted w-4 text-xs font-mono tnum">{idx + 1}.</span>
                  <span className="font-medium text-fg flex-1 min-w-0 truncate">{player.name}</span>
                  <span className="text-fg-muted text-xs shrink-0">{player.teamName}</span>
                  <span
                    className="font-mono text-[11px] font-semibold px-1.5 py-0.5 rounded-chip shrink-0"
                    style={{
                      background: posRank(player.position) === 0 ? 'var(--live-soft)'
                        : posRank(player.position) === 1 ? 'var(--win-soft)'
                        : posRank(player.position) === 2 ? 'var(--warning-soft)'
                        : 'var(--surface-3)',
                      color: posRank(player.position) === 0 ? 'var(--live)'
                        : posRank(player.position) === 1 ? 'var(--win)'
                        : posRank(player.position) === 2 ? 'var(--warning)'
                        : 'var(--fg-muted)',
                    }}
                  >
                    {player.position}
                  </span>
                  {!isLocked && (
                    <button type="button"
                      onClick={() => setSelectedPlayers(prev => prev.filter(p => p.id !== player.id))}
                      className="text-fg-muted hover:text-fg transition-colors text-xs shrink-0"
                      aria-label={`Remove ${player.name}`}>
                      ✕
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Team grid + expanded player panel */}
          {!isLocked || selectedPlayers.length > 0 ? (
            <TeamGrid
              teams={teams}
              players={players}
              selectedPlayers={selectedPlayers}
              onAdd={p => setSelectedPlayers(prev => prev.length < 6 ? [...prev, p] : prev)}
              onRemove={id => setSelectedPlayers(prev => prev.filter(p => p.id !== id))}
              disabled={isLocked}
            />
          ) : null}
        </section>

        {saveError && (
          <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>{saveError}</p>
        )}
        {saveSuccess && (
          <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--win)', background: 'var(--win-soft)' }}>
            Prediction saved.
          </p>
        )}

        {!isLocked && (
          <button type="submit" disabled={saving}
            className="font-semibold rounded-input px-6 py-2.5 transition-colors disabled:opacity-50"
            style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}>
            {saving ? 'Saving…' : existingPrediction ? 'Update prediction' : 'Save prediction'}
          </button>
        )}
      </form>
    </div>
  );
}
