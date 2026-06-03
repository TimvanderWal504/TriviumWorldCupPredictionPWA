import { useEffect, useState } from 'react';
import { useAuth } from '../auth/useAuth.ts';

interface IngestionStatus {
  lastSuccessfulPoll: string | null;
  lastAttemptedPoll: string | null;
  lastError: string | null;
  totalPollCount: number;
  errorCount: number;
  pendingFixtureCount: number;
}

interface OverrideRecord {
  id: string;
  adminDisplayName: string;
  overriddenAt: string;
  targetType: string;
  targetId: string;
  description: string;
}

export function AdminPage() {
  const { user } = useAuth();

  const isAdmin = user?.roles?.includes('admin') ?? false;

  const [ingestion, setIngestion] = useState<IngestionStatus | null>(null);
  const [ingestionError, setIngestionError] = useState<string | null>(null);

  const [overrides, setOverrides] = useState<OverrideRecord[]>([]);
  const [overridesError, setOverridesError] = useState<string | null>(null);

  const [fixtureId, setFixtureId] = useState('');
  const [homeScore, setHomeScore] = useState('');
  const [awayScore, setAwayScore] = useState('');
  const [resultMsg, setResultMsg] = useState<string | null>(null);
  const [resultError, setResultError] = useState<string | null>(null);

  const [recomputeMsg, setRecomputeMsg] = useState<string | null>(null);

  // All hooks must be declared before any conditional return.
  useEffect(() => {
    if (!isAdmin) return;
    fetchIngestion();
    fetchOverrides();
  // fetchIngestion and fetchOverrides are stable (defined below, not re-created on render)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin]);

  // Guard: should never reach this page as non-admin, but belt-and-suspenders.
  if (!isAdmin) {
    return (
      <div className="p-8 text-center">
        <p className="text-red-400 font-semibold text-lg">Access denied.</p>
        <p className="text-slate-400 text-sm mt-1">You must be an admin to view this page.</p>
      </div>
    );
  }

  async function fetchIngestion() {
    try {
      const res = await fetch('/admin/ingestion');
      if (!res.ok) {
        setIngestionError(`HTTP ${res.status}`);
        return;
      }
      const data: IngestionStatus = await res.json();
      setIngestion(data);
      setIngestionError(null);
    } catch (err) {
      setIngestionError(String(err));
    }
  }

  async function fetchOverrides() {
    try {
      const res = await fetch('/admin/overrides');
      if (!res.ok) {
        setOverridesError(`HTTP ${res.status}`);
        return;
      }
      const data: OverrideRecord[] = await res.json();
      setOverrides(data);
      setOverridesError(null);
    } catch (err) {
      setOverridesError(String(err));
    }
  }

  async function handleSetResult(e: React.FormEvent) {
    e.preventDefault();
    setResultMsg(null);
    setResultError(null);

    const home = parseInt(homeScore, 10);
    const away = parseInt(awayScore, 10);
    if (!fixtureId.trim()) {
      setResultError('Fixture ID is required.');
      return;
    }
    if (isNaN(home) || isNaN(away) || home < 0 || away < 0) {
      setResultError('Scores must be non-negative integers.');
      return;
    }

    try {
      const res = await fetch(`/admin/fixtures/${encodeURIComponent(fixtureId.trim())}/result`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ homeScore: home, awayScore: away }),
      });

      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        setResultError((body as { error?: string })?.error ?? `HTTP ${res.status}`);
        return;
      }

      setResultMsg(`Result set: ${home}-${away} for fixture ${fixtureId.trim()}`);
      setFixtureId('');
      setHomeScore('');
      setAwayScore('');
      // Refresh overrides and ingestion status
      await fetchOverrides();
      await fetchIngestion();
    } catch (err) {
      setResultError(String(err));
    }
  }

  async function handleForceRecompute() {
    setRecomputeMsg(null);
    try {
      const res = await fetch('/admin/recompute', { method: 'POST' });
      if (!res.ok) {
        setRecomputeMsg(`Error: HTTP ${res.status}`);
        return;
      }
      const body = await res.json();
      setRecomputeMsg((body as { message?: string }).message ?? 'Recompute triggered.');
    } catch (err) {
      setRecomputeMsg(`Error: ${String(err)}`);
    }
  }

  function formatDate(iso: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleString();
  }

  return (
    <div className="max-w-4xl mx-auto p-6 space-y-8">
      <h1 className="text-2xl font-bold text-white">Admin Panel</h1>

      {/* ── Ingestion health ────────────────────────────────────────────── */}
      <section className="bg-slate-800 rounded-xl p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-white">Ingestion Health</h2>
          <button
            onClick={fetchIngestion}
            className="text-sm text-blue-400 hover:text-blue-300 transition-colors"
          >
            Refresh
          </button>
        </div>

        {ingestionError && (
          <p className="text-red-400 text-sm">Failed to load: {ingestionError}</p>
        )}

        {ingestion && (
          <dl className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <dt className="text-slate-400">Last successful poll</dt>
              <dd className="text-white">{formatDate(ingestion.lastSuccessfulPoll)}</dd>
            </div>
            <div>
              <dt className="text-slate-400">Last attempted poll</dt>
              <dd className="text-white">{formatDate(ingestion.lastAttemptedPoll)}</dd>
            </div>
            <div>
              <dt className="text-slate-400">Total polls</dt>
              <dd className="text-white">{ingestion.totalPollCount}</dd>
            </div>
            <div>
              <dt className="text-slate-400">Error count</dt>
              <dd className={ingestion.errorCount > 0 ? 'text-red-400' : 'text-white'}>
                {ingestion.errorCount}
              </dd>
            </div>
            <div>
              <dt className="text-slate-400">Pending fixtures (past kickoff, not completed)</dt>
              <dd className={ingestion.pendingFixtureCount > 0 ? 'text-yellow-400' : 'text-white'}>
                {ingestion.pendingFixtureCount}
              </dd>
            </div>
            {ingestion.lastError && (
              <div className="col-span-2">
                <dt className="text-slate-400">Last error</dt>
                <dd className="text-red-400 break-all">{ingestion.lastError}</dd>
              </div>
            )}
          </dl>
        )}

        <div className="pt-2">
          <button
            onClick={handleForceRecompute}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium rounded-lg transition-colors"
          >
            Force recompute scores
          </button>
          {recomputeMsg && (
            <p className="mt-2 text-sm text-green-400">{recomputeMsg}</p>
          )}
        </div>
      </section>

      {/* ── Manual result override ──────────────────────────────────────── */}
      <section className="bg-slate-800 rounded-xl p-6 space-y-4">
        <h2 className="text-lg font-semibold text-white">Manual Result Override</h2>
        <form onSubmit={handleSetResult} className="space-y-3">
          <div>
            <label className="block text-sm text-slate-400 mb-1" htmlFor="fixtureId">
              Fixture ID
            </label>
            <input
              id="fixtureId"
              type="text"
              value={fixtureId}
              onChange={e => setFixtureId(e.target.value)}
              placeholder="e.g. 1"
              className="w-40 px-3 py-2 bg-slate-700 text-white rounded-lg text-sm border border-slate-600 focus:outline-none focus:border-blue-500"
            />
          </div>
          <div className="flex items-end gap-3">
            <div>
              <label className="block text-sm text-slate-400 mb-1" htmlFor="homeScore">
                Home score
              </label>
              <input
                id="homeScore"
                type="number"
                min="0"
                value={homeScore}
                onChange={e => setHomeScore(e.target.value)}
                placeholder="0"
                className="w-24 px-3 py-2 bg-slate-700 text-white rounded-lg text-sm border border-slate-600 focus:outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm text-slate-400 mb-1" htmlFor="awayScore">
                Away score
              </label>
              <input
                id="awayScore"
                type="number"
                min="0"
                value={awayScore}
                onChange={e => setAwayScore(e.target.value)}
                placeholder="0"
                className="w-24 px-3 py-2 bg-slate-700 text-white rounded-lg text-sm border border-slate-600 focus:outline-none focus:border-blue-500"
              />
            </div>
            <button
              type="submit"
              className="px-4 py-2 bg-orange-600 hover:bg-orange-500 text-white text-sm font-medium rounded-lg transition-colors"
            >
              Set result
            </button>
          </div>
          {resultError && <p className="text-red-400 text-sm">{resultError}</p>}
          {resultMsg && <p className="text-green-400 text-sm">{resultMsg}</p>}
        </form>
      </section>

      {/* ── Override history ────────────────────────────────────────────── */}
      <section className="bg-slate-800 rounded-xl p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-white">Override History (last 50)</h2>
          <button
            onClick={fetchOverrides}
            className="text-sm text-blue-400 hover:text-blue-300 transition-colors"
          >
            Refresh
          </button>
        </div>

        {overridesError && (
          <p className="text-red-400 text-sm">Failed to load: {overridesError}</p>
        )}

        {overrides.length === 0 && !overridesError && (
          <p className="text-slate-500 text-sm">No overrides recorded yet.</p>
        )}

        {overrides.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="text-slate-400 border-b border-slate-700">
                  <th className="py-2 pr-4">When</th>
                  <th className="py-2 pr-4">Admin</th>
                  <th className="py-2 pr-4">Type</th>
                  <th className="py-2 pr-4">Target</th>
                  <th className="py-2">Description</th>
                </tr>
              </thead>
              <tbody>
                {overrides.map(o => (
                  <tr key={o.id} className="border-b border-slate-700/50 hover:bg-slate-700/30">
                    <td className="py-2 pr-4 text-slate-300 whitespace-nowrap">
                      {formatDate(o.overriddenAt)}
                    </td>
                    <td className="py-2 pr-4 text-white">{o.adminDisplayName}</td>
                    <td className="py-2 pr-4 text-slate-300">{o.targetType}</td>
                    <td className="py-2 pr-4 text-slate-300 font-mono text-xs">{o.targetId}</td>
                    <td className="py-2 text-slate-300">{o.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
