import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import { Clock } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';

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
  fixtureId: string, homeScore: number, awayScore: number, method: 'POST' | 'PUT',
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

function isLocked(kickoffUtc: string): boolean {
  return new Date(kickoffUtc).getTime() <= Date.now();
}

function formatKickoff(kickoffUtc: string): string {
  return new Date(kickoffUtc).toLocaleString(undefined, {
    weekday: 'short', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

interface FixtureCardProps {
  fixture: FixtureDto;
  prediction: GroupPredictionDto | undefined;
  onSaved: (p: GroupPredictionDto) => void;
}

function FixtureCard({ fixture, prediction, onSaved }: FixtureCardProps) {
  const played = fixture.status === 'Completed';
  const locked = isLocked(fixture.kickoffUtc) || played;
  const [homeInput, setHomeInput] = useState(prediction !== undefined ? String(prediction.homeScore) : '');
  const [awayInput, setAwayInput] = useState(prediction !== undefined ? String(prediction.awayScore) : '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const unpredicted = prediction === undefined && !locked;

  // Sync inputs when prediction loads from the server
  useEffect(() => {
    if (prediction !== undefined) {
      setHomeInput(String(prediction.homeScore));
      setAwayInput(String(prediction.awayScore));
    }
  }, [prediction]);

  // Auto-save 700ms after both inputs are valid and differ from the stored prediction
  useEffect(() => {
    if (locked) return;
    const home = parseInt(homeInput, 10);
    const away = parseInt(awayInput, 10);
    if (homeInput === '' || awayInput === '') return;
    if (isNaN(home) || isNaN(away) || home < 0 || away < 0) return;
    if (prediction && prediction.homeScore === home && prediction.awayScore === away) return;

    const timer = setTimeout(async () => {
      setError(null);
      setSaved(false);
      setSaving(true);
      const method = prediction !== undefined ? 'PUT' : 'POST';
      const result = await savePrediction(fixture.id, home, away, method);
      setSaving(false);
      if (result.ok) {
        onSaved({ fixtureId: fixture.id, homeScore: home, awayScore: away, submittedAt: new Date().toISOString() });
        setSaved(true);
        setTimeout(() => setSaved(false), 2000);
      } else {
        setError(result.error ?? 'Save failed.');
      }
    }, 700);

    return () => clearTimeout(timer);
    // prediction, fixture.id, onSaved omitted: including them would re-trigger after every save
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [homeInput, awayInput, locked]);

  const cardBorderColor = unpredicted ? 'var(--secondary)' : 'var(--border)';
  const cardOpacity = locked ? 0.72 : 1;

  return (
    <div
      className="rounded-card bg-surface p-4 flex flex-col gap-2.5 border"
      style={{ borderColor: cardBorderColor, opacity: cardOpacity }}
    >
      {/* Header */}
      <div className="flex items-center justify-between text-[11px] text-fg-muted">
        <span className="font-mono">Match {fixture.matchNumber} · {fixture.venue}</span>
        <span
          className={`text-[11px] font-semibold px-2 py-0.5 rounded-md ${
            !saving && !saved && (locked || !unpredicted) ? 'bg-surface-3 text-fg-muted' : ''
          }`}
          style={
            saving      ? { background: 'var(--warning-soft)', color: 'var(--warning)' }
            : saved     ? { background: 'var(--win-soft)',     color: 'var(--win)' }
            : unpredicted ? { background: 'var(--win-soft)',   color: 'var(--win)' }
            : undefined
          }
        >
          {saving ? 'Saving…' : saved ? 'Saved' : played ? 'Played' : locked ? 'Locked' : unpredicted ? 'Unpredicted' : 'Predicted'}
        </span>
      </div>

      {/* Kickoff */}
      <div className="flex items-center gap-1.5 text-[11px] text-fg-muted">
        <Clock size={12} />{formatKickoff(fixture.kickoffUtc)}
      </div>

      {/* Team rows */}
      <div className="flex flex-col gap-2">
        {[
          { id: fixture.homeTeamId, name: fixture.homeTeamName, value: homeInput, set: setHomeInput, label: `${fixture.homeTeamName} predicted score` },
          { id: fixture.awayTeamId, name: fixture.awayTeamName, value: awayInput, set: setAwayInput, label: `${fixture.awayTeamName} predicted score` },
        ].map(({ id, name, value, set, label }) => (
          <div key={name} className="flex items-center gap-2.5">
            {flagUrl(id) && <img src={flagUrl(id)} alt="" width={28} height={20} className="flag shrink-0" />}
            <span className="flex-1 min-w-0 truncate font-semibold text-fg">{name}</span>
            {locked
              ? <span className="font-display font-bold text-lg tnum text-fg-muted w-12 text-center">
                  {prediction ? value : '–'}
                </span>
              : <input
                  type="text" min={0} max={99} value={value}
                  onChange={(e: ChangeEvent<HTMLInputElement>) => set(e.target.value)}
                  inputMode="numeric" aria-label={label}
                  className="w-12 text-center font-display font-bold text-lg tnum bg-surface-2 rounded-input py-1.5 border border-border"
                />}
          </div>
        ))}
      </div>

      {error && (
        <p className="text-[13px] px-3 py-1.5 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>
          {error}
        </p>
      )}
    </div>
  );
}

interface GroupPredictionsPageProps {
  onAllGroupsComplete?: () => void;
}

export function GroupPredictionsPage({ onAllGroupsComplete }: GroupPredictionsPageProps) {
  const [fixtures, setFixtures] = useState<FixtureDto[]>([]);
  const [predictions, setPredictions] = useState<Map<string, GroupPredictionDto>>(new Map());
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeGroup, setActiveGroup] = useState<string>('A');

  const tabsRef = useRef<HTMLDivElement>(null);

  // Refs to avoid stale-closure issues in effects
  const loadedRef = useRef(false);
  const onCompleteRef = useRef(onAllGroupsComplete);
  useEffect(() => { onCompleteRef.current = onAllGroupsComplete; }, [onAllGroupsComplete]);

  // Groups that have already triggered auto-advance (pre-seeded on load for already-complete groups)
  const triggeredGroups = useRef(new Set<string>());

  useEffect(() => {
    Promise.all([fetchFixtures(), fetchPredictions()])
      .then(([fixtureList, predictionList]) => {
        setFixtures(fixtureList);
        const map = new Map<string, GroupPredictionDto>();
        for (const p of predictionList) map.set(p.fixtureId, p);
        setPredictions(map);

        if (fixtureList.length > 0) {
          const letters = [...new Set(fixtureList.map(f => f.groupLetter))].sort();

          // Mark already-complete groups so they don't fire auto-advance on mount
          for (const letter of letters) {
            const unlocked = fixtureList.filter(f => f.groupLetter === letter && !isLocked(f.kickoffUtc));
            if (unlocked.length > 0 && unlocked.every(f => map.has(f.id))) {
              triggeredGroups.current.add(letter);
            }
          }

          // Start on the first group that still has unpredicted fixtures
          const firstIncomplete =
            letters.find(l => !triggeredGroups.current.has(l)) ?? letters[letters.length - 1];
          setActiveGroup(firstIncomplete);
        }

        loadedRef.current = true;
      })
      .catch((err: unknown) => setLoadError(err instanceof Error ? err.message : 'Failed to load data.'))
      .finally(() => setLoading(false));
  }, []);

  // Scroll the active tab into view whenever activeGroup changes
  useEffect(() => {
    if (!tabsRef.current) return;
    const btn = tabsRef.current.querySelector('[aria-selected="true"]') as HTMLElement | null;
    btn?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
  }, [activeGroup]);


  // Auto-advance when every unlocked fixture in the active group has a prediction
  useEffect(() => {
    if (!loadedRef.current || fixtures.length === 0) return;

    const unlockedInGroup = fixtures.filter(
      f => f.groupLetter === activeGroup && !isLocked(f.kickoffUtc),
    );
    if (unlockedInGroup.length === 0) return;
    if (!unlockedInGroup.every(f => predictions.has(f.id))) return;
    if (triggeredGroups.current.has(activeGroup)) return;

    triggeredGroups.current.add(activeGroup);

    const groupLetters = [...new Set(fixtures.map(f => f.groupLetter))].sort();
    const currentIdx = groupLetters.indexOf(activeGroup);

    // Small delay so the last "Saved" flash is visible before transitioning
    const tid = setTimeout(() => {
      if (currentIdx < groupLetters.length - 1) {
        setActiveGroup(groupLetters[currentIdx + 1]);
      } else {
        onCompleteRef.current?.();
      }
    }, 600);

    return () => clearTimeout(tid);
    // onCompleteRef is a ref — intentionally excluded from deps
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [predictions, activeGroup, fixtures]);

  const groupLetters = [...new Set(fixtures.map(f => f.groupLetter))].sort();
  const activeFixtures = fixtures.filter(f => f.groupLetter === activeGroup);

  if (loading) return <div className="flex items-center justify-center py-20 text-fg-muted">Loading fixtures…</div>;
  if (loadError) return <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>{loadError}</div>;

  return (
    <div className="max-w-3xl mx-auto px-4 py-4">
      <div className="rounded-card bg-surface border border-border px-4 py-3 mb-3 text-[13px] text-fg-secondary leading-relaxed">
        Predict the score for each match. Predictions lock at kickoff. Times are in your local timezone.
      </div>

      {/* Group tab bar */}
      <div
        ref={tabsRef}
        className="flex gap-1.5 overflow-x-auto appscroll pb-1.5"
        role="tablist"
      >
        {groupLetters.map(letter => (
          <button
            key={letter} role="tab" aria-selected={activeGroup === letter}
            onClick={() => setActiveGroup(letter)}
            className={`px-3.5 py-1.5 rounded-input text-[13px] font-semibold whitespace-nowrap transition-colors ${
              activeGroup !== letter ? 'bg-surface-3 text-fg-secondary' : ''
            }`}
            style={activeGroup === letter
              ? { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }
              : undefined}
          >
            Group {letter}
          </button>
        ))}
      </div>

      {/* Active-group position indicator */}
      {groupLetters.length > 1 && (() => {
        const n = groupLetters.length;
        const i = groupLetters.indexOf(activeGroup);
        const tw = 133.333 / n;
        const tl = i * tw;
        return (
          <div className="relative h-[3px] bg-surface-3 rounded-full mb-3 mt-1.5 overflow-hidden">
            <div
              className="absolute inset-y-0 rounded-full"
              style={{
                background: 'var(--secondary)',
                width: `${tw}%`,
                left: `${tl}%`,
                transition: 'left 120ms ease',
              }}
            />
          </div>
        );
      })()}

      <div className="flex flex-col gap-2.5">
        {activeFixtures.map(fixture => (
          <FixtureCard
            key={fixture.id}
            fixture={fixture}
            prediction={predictions.get(fixture.id)}
            onSaved={updated => setPredictions(prev => new Map(prev).set(updated.fixtureId, updated))}
          />
        ))}
      </div>
    </div>
  );
}
