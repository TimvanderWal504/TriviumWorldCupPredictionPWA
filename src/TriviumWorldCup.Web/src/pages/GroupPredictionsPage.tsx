import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import { ChevronLeft, ChevronRight, Clock } from 'lucide-react';
import { flagUrl } from '../utils/flagUrl.ts';
import { GroupStandingsTable, type StandingsMatchInput } from '../components/GroupStandingsTable.tsx';
import { Spinner } from '../components/ui/Spinner.tsx';

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

function getLocalDateKey(kickoffUtc: string): string {
  const d = new Date(kickoffUtc);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function formatDateLabel(dateKey: string): string {
  const [y, m, day] = dateKey.split('-').map(Number);
  return new Date(y, m - 1, day).toLocaleDateString(undefined, {
    weekday: 'short', month: 'short', day: 'numeric',
  });
}

interface FixtureCardProps {
  fixture: FixtureDto;
  prediction: GroupPredictionDto | undefined;
  onSaved: (p: GroupPredictionDto) => void;
  showGroup?: boolean;
}

function FixtureCard({ fixture, prediction, onSaved, showGroup }: FixtureCardProps) {
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
        <span className="font-mono">Match {fixture.matchNumber}{showGroup ? ` · Group ${fixture.groupLetter}` : ''} · {fixture.venue} · {fixture.city}</span>
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
          {saving ? <span className="flex items-center gap-1"><Spinner size="sm" />Saving…</span> : saved ? 'Saved' : played ? 'Played' : locked ? 'Locked' : unpredicted ? 'Unpredicted' : 'Predicted'}
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
  viewMode: 'group' | 'date';
}

export function GroupPredictionsPage({ onAllGroupsComplete, viewMode }: GroupPredictionsPageProps) {
  const [fixtures, setFixtures] = useState<FixtureDto[]>([]);
  const [predictions, setPredictions] = useState<Map<string, GroupPredictionDto>>(new Map());
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeGroup, setActiveGroup] = useState<string>('A');
  const [activeDate, setActiveDate] = useState<string>('');

  const tabsRef = useRef<HTMLDivElement>(null);
  const dateTabsRef = useRef<HTMLDivElement>(null);

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

          // Next match to be played, used as the fallback once everything is predicted
          const upcoming = [...fixtureList]
            .filter(f => !isLocked(f.kickoffUtc))
            .sort((a, b) => new Date(a.kickoffUtc).getTime() - new Date(b.kickoffUtc).getTime());
          const nextFixture = upcoming[0] ?? [...fixtureList].sort(
            (a, b) => new Date(b.kickoffUtc).getTime() - new Date(a.kickoffUtc).getTime(),
          )[0];

          // Start on the first group that still has unpredicted fixtures, otherwise the next group to play
          const firstIncomplete =
            letters.find(l => !triggeredGroups.current.has(l)) ?? nextFixture?.groupLetter ?? letters[letters.length - 1];
          setActiveGroup(firstIncomplete);

          // Set initial active date to the first date with unpredicted unlocked fixtures, otherwise today
          const dateKeys = [...new Set(fixtureList.map(f => getLocalDateKey(f.kickoffUtc)))].sort();
          const todayKey = getLocalDateKey(new Date().toISOString());
          const todayFallback = dateKeys.find(dk => dk >= todayKey) ?? dateKeys[dateKeys.length - 1];
          const firstDateIncomplete = dateKeys.find(dk =>
            fixtureList.some(f => getLocalDateKey(f.kickoffUtc) === dk && !isLocked(f.kickoffUtc) && !map.has(f.id))
          ) ?? todayFallback;
          setActiveDate(firstDateIncomplete ?? '');
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

  // Scroll the active date tab into view whenever activeDate changes
  useEffect(() => {
    if (!dateTabsRef.current) return;
    const btn = dateTabsRef.current.querySelector('[aria-selected="true"]') as HTMLElement | null;
    btn?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
  }, [activeDate]);


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

  // Actual results take priority once played; otherwise fall back to the user's prediction.
  const activeGroupStandingsMatches: StandingsMatchInput[] = activeFixtures.map(f => {
    const prediction = predictions.get(f.id);
    const homeScore = f.homeScore ?? prediction?.homeScore ?? null;
    const awayScore = f.awayScore ?? prediction?.awayScore ?? null;
    return {
      homeTeamId: f.homeTeamId, homeTeamName: f.homeTeamName,
      awayTeamId: f.awayTeamId, awayTeamName: f.awayTeamName,
      homeScore, awayScore,
    };
  });

  const dates = [...new Set(fixtures.map(f => getLocalDateKey(f.kickoffUtc)))].sort();
  const activeDateFixtures = fixtures
    .filter(f => getLocalDateKey(f.kickoffUtc) === activeDate)
    .sort((a, b) => new Date(a.kickoffUtc).getTime() - new Date(b.kickoffUtc).getTime());

  if (loading) return (
    <div className="flex items-center justify-center py-20">
      <Spinner size="lg" label="Loading fixtures" />
    </div>
  );
  if (loadError) return <div className="flex items-center justify-center py-20 text-[13px]" style={{ color: 'var(--loss)' }}>{loadError}</div>;

  const btnBase = 'w-7 h-7 flex items-center justify-center rounded-input text-sm font-bold transition-opacity disabled:opacity-25';

  return (
    <div className="max-w-3xl mx-auto px-4 py-4">
      <div className="rounded-card bg-surface border border-border px-4 py-3 mb-3 text-[13px] text-fg-secondary leading-relaxed">
        Predict the score for each match. Predictions lock at kickoff. Times are in your local timezone.
      </div>

      {viewMode === 'group' ? (
        <>
          {/* Group tab bar */}
          <div ref={tabsRef} className="flex gap-1.5 overflow-x-auto appscroll pb-1.5" role="tablist">
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

          {/* Group navigator: prev/next buttons + 5-dot window */}
          {groupLetters.length > 1 && (() => {
            const n = groupLetters.length;
            const activeIdx = groupLetters.indexOf(activeGroup);
            const windowSize = Math.min(5, n);
            const windowStart = Math.max(0, Math.min(n - windowSize, activeIdx - Math.floor(windowSize / 2)));
            const visibleLetters = groupLetters.slice(windowStart, windowStart + windowSize);
            return (
              <div className="flex items-center justify-center gap-3 mb-3 mt-1.5">
                <button onClick={() => setActiveGroup(groupLetters[activeIdx - 1])} disabled={activeIdx === 0} className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }} aria-label="Previous group">
                  <ChevronLeft size={16} />
                </button>
                <div className="flex items-center gap-2.5">
                  {visibleLetters.map(letter => {
                    const active = activeGroup === letter;
                    return (
                      <button key={letter} onClick={() => setActiveGroup(letter)} aria-label={`Group ${letter}`} className="flex flex-col items-center gap-0.5">
                        <div className="rounded-full transition-all duration-150" style={{ width: active ? 10 : 7, height: active ? 10 : 7, background: active ? 'var(--secondary)' : 'var(--surface-3)' }} />
                        <span className="text-[9px] font-mono leading-none" style={{ color: active ? 'var(--secondary)' : 'var(--fg-muted)' }}>{letter}</span>
                      </button>
                    );
                  })}
                </div>
                <button onClick={() => setActiveGroup(groupLetters[activeIdx + 1])} disabled={activeIdx === n - 1} className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }} aria-label="Next group">
                  <ChevronRight size={16} />
                </button>
              </div>
            );
          })()}

          <div className="flex flex-col gap-2.5">
            {activeFixtures.map(fixture => (
              <FixtureCard key={fixture.id} fixture={fixture} prediction={predictions.get(fixture.id)} onSaved={updated => setPredictions(prev => new Map(prev).set(updated.fixtureId, updated))} />
            ))}
          </div>

          <GroupStandingsTable matches={activeGroupStandingsMatches} />
        </>
      ) : (
        <>
          {/* Date tab bar */}
          <div ref={dateTabsRef} className="flex gap-1.5 overflow-x-auto appscroll pb-1.5" role="tablist">
            {dates.map(dk => (
              <button
                key={dk} role="tab" aria-selected={activeDate === dk}
                onClick={() => setActiveDate(dk)}
                className={`px-3.5 py-1.5 rounded-input text-[13px] font-semibold whitespace-nowrap transition-colors ${
                  activeDate !== dk ? 'bg-surface-3 text-fg-secondary' : ''
                }`}
                style={activeDate === dk
                  ? { background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }
                  : undefined}
              >
                {formatDateLabel(dk)}
              </button>
            ))}
          </div>

          {/* Date navigator: prev/next + dot window */}
          {dates.length > 1 && (() => {
            const n = dates.length;
            const activeIdx = dates.indexOf(activeDate);
            const windowSize = Math.min(5, n);
            const windowStart = Math.max(0, Math.min(n - windowSize, activeIdx - Math.floor(windowSize / 2)));
            const visibleDates = dates.slice(windowStart, windowStart + windowSize);
            return (
              <div className="flex items-center justify-center gap-3 mb-3 mt-1.5">
                <button onClick={() => setActiveDate(dates[activeIdx - 1])} disabled={activeIdx === 0} className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }} aria-label="Previous date">
                  <ChevronLeft size={16} />
                </button>
                <div className="flex items-center gap-2.5">
                  {visibleDates.map(dk => {
                    const active = activeDate === dk;
                    return (
                      <button key={dk} onClick={() => setActiveDate(dk)} aria-label={formatDateLabel(dk)} className="flex flex-col items-center gap-0.5">
                        <div className="rounded-full transition-all duration-150" style={{ width: active ? 10 : 7, height: active ? 10 : 7, background: active ? 'var(--secondary)' : 'var(--surface-3)' }} />
                        <span className="text-[9px] font-mono leading-none" style={{ color: active ? 'var(--secondary)' : 'var(--fg-muted)' }}>{dk.slice(8)}</span>
                      </button>
                    );
                  })}
                </div>
                <button onClick={() => setActiveDate(dates[activeIdx + 1])} disabled={activeIdx === n - 1} className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }} aria-label="Next date">
                  <ChevronRight size={16} />
                </button>
              </div>
            );
          })()}

          <div className="flex flex-col gap-2.5">
            {activeDateFixtures.map(fixture => (
              <FixtureCard key={fixture.id} fixture={fixture} prediction={predictions.get(fixture.id)} onSaved={updated => setPredictions(prev => new Map(prev).set(updated.fixtureId, updated))} showGroup />
            ))}
          </div>
        </>
      )}
    </div>
  );
}
