import { useEffect, useState } from 'react';
import { useAuth } from '../auth/useAuth.ts';

interface IngestionStatus {
  lastSuccessfulPoll: string | null; lastAttemptedPoll: string | null;
  lastError: string | null; totalPollCount: number; errorCount: number; pendingFixtureCount: number;
}
interface OverrideRecord {
  id: string; adminDisplayName: string; overriddenAt: string;
  targetType: string; targetId: string; description: string;
}

interface InviteUserDto {
  id: string;
  displayName: string;
  roles: string[];
  createdAt: string;
  loginPath: string;
}

export function AdminPage() {
  const { user, isLinkAuth } = useAuth();
  const isAdmin = user?.roles?.includes('admin') ?? false;

  const [inviteUsers, setInviteUsers] = useState<InviteUserDto[]>([]);
  const [newUserName, setNewUserName] = useState('');
  const [createdLoginUrl, setCreatedLoginUrl] = useState<string | null>(null);
  const [userError, setUserError] = useState<string | null>(null);
  const [userBusy, setUserBusy] = useState(false);

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

  useEffect(() => {
    if (!isAdmin) return;
    fetchIngestion(); fetchOverrides();
    if (isLinkAuth) fetchInviteUsers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin, isLinkAuth]);

  function fetchInviteUsers() {
    fetch('/admin/users', { credentials: 'include' })
      .then(r => r.json())
      .then((data: InviteUserDto[]) => setInviteUsers(data))
      .catch(() => {});
  }

  async function handleCreateUser(e: React.FormEvent) {
    e.preventDefault();
    setUserError(null);
    setCreatedLoginUrl(null);
    if (!newUserName.trim()) { setUserError('Name is required.'); return; }
    setUserBusy(true);
    try {
      const res = await fetch('/admin/users', {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: newUserName.trim() }),
      });
      if (!res.ok) { const b = await res.json().catch(() => ({})); setUserError((b as { error?: string }).error ?? `HTTP ${res.status}`); return; }
      const created = await res.json() as InviteUserDto;
      setCreatedLoginUrl(window.location.origin + created.loginPath);
      setNewUserName('');
      fetchInviteUsers();
    } catch (err) {
      setUserError(String(err));
    } finally {
      setUserBusy(false);
    }
  }

  async function handleDeleteUser(id: string) {
    if (!confirm('Remove this user? Their predictions stay but they can no longer log in.')) return;
    await fetch(`/admin/users/${id}`, { method: 'DELETE', credentials: 'include' });
    fetchInviteUsers();
    setCreatedLoginUrl(null);
  }

  if (!isAdmin) {
    return (
      <div className="p-8 text-center">
        <p className="font-semibold text-lg" style={{ color: 'var(--loss)' }}>Access denied.</p>
        <p className="text-fg-muted text-sm mt-1">You must be an admin to view this page.</p>
      </div>
    );
  }

  async function fetchIngestion() {
    try {
      const res = await fetch('/admin/ingestion');
      if (!res.ok) { setIngestionError(`HTTP ${res.status}`); return; }
      setIngestion(await res.json() as IngestionStatus);
      setIngestionError(null);
    } catch (err) { setIngestionError(String(err)); }
  }

  async function fetchOverrides() {
    try {
      const res = await fetch('/admin/overrides');
      if (!res.ok) { setOverridesError(`HTTP ${res.status}`); return; }
      setOverrides(await res.json() as OverrideRecord[]);
      setOverridesError(null);
    } catch (err) { setOverridesError(String(err)); }
  }

  async function handleSetResult(e: React.FormEvent) {
    e.preventDefault(); setResultMsg(null); setResultError(null);
    const home = parseInt(homeScore, 10);
    const away = parseInt(awayScore, 10);
    if (!fixtureId.trim()) { setResultError('Fixture ID is required.'); return; }
    if (isNaN(home) || isNaN(away) || home < 0 || away < 0) { setResultError('Scores must be non-negative integers.'); return; }
    try {
      const res = await fetch(`/admin/fixtures/${encodeURIComponent(fixtureId.trim())}/result`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ homeScore: home, awayScore: away }),
      });
      if (!res.ok) { const body = await res.json().catch(() => ({})); setResultError((body as { error?: string })?.error ?? `HTTP ${res.status}`); return; }
      setResultMsg(`Result set: ${home}-${away} for fixture ${fixtureId.trim()}`);
      setFixtureId(''); setHomeScore(''); setAwayScore('');
      await fetchOverrides(); await fetchIngestion();
    } catch (err) { setResultError(String(err)); }
  }

  async function handleForceRecompute() {
    setRecomputeMsg(null);
    try {
      const res = await fetch('/admin/recompute', { method: 'POST' });
      if (!res.ok) { setRecomputeMsg(`Error: HTTP ${res.status}`); return; }
      const body = await res.json();
      setRecomputeMsg((body as { message?: string }).message ?? 'Recompute triggered.');
    } catch (err) { setRecomputeMsg(`Error: ${String(err)}`); }
  }

  function formatDate(iso: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleString();
  }

  const inputCls = 'bg-surface-2 text-fg rounded-input px-3 py-2 text-sm border border-border';
  const labelCls = 'block text-[11px] font-display font-bold uppercase tracking-wider text-fg-muted mb-1';

  return (
    <div className="max-w-4xl mx-auto px-4 py-4 space-y-6">



      {/* Users — link auth provider only */}
      {isLinkAuth && (
        <section className="rounded-card bg-surface border border-border p-5 space-y-4">
          <h2 className="font-display font-bold text-lg tracking-tight">Users</h2>

          {/* Create user */}
          <form onSubmit={handleCreateUser} className="flex gap-2 items-end">
            <div className="flex-1">
              <label htmlFor="newUserName" className={labelCls}>Display name</label>
              <input
                id="newUserName" type="text" value={newUserName}
                onChange={e => setNewUserName(e.target.value)}
                placeholder="e.g. Jan" className={`${inputCls} w-full`}
              />
            </div>
            <button type="submit" disabled={userBusy}
              className="px-4 py-2 rounded-input text-sm font-semibold transition-colors disabled:opacity-50"
              style={{ background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }}>
              {userBusy ? 'Creating…' : 'Create user'}
            </button>
          </form>
          {userError && <p className="text-sm" style={{ color: 'var(--loss)' }}>{userError}</p>}

          {/* Login link after creation */}
          {createdLoginUrl && (
            <div className="rounded-input p-3 space-y-1" style={{ background: 'var(--win-soft)' }}>
              <p className="text-[11px] font-display font-bold uppercase tracking-wider" style={{ color: 'var(--win)' }}>
                User created — share this login link:
              </p>
              <div className="flex gap-2 items-center">
                <code className="text-xs break-all flex-1" style={{ color: 'var(--fg)' }}>{createdLoginUrl}</code>
                <button
                  onClick={() => navigator.clipboard.writeText(createdLoginUrl)}
                  className="px-2.5 py-1 rounded-input text-[12px] font-semibold shrink-0"
                  style={{ background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }}>
                  Copy
                </button>
              </div>
            </div>
          )}

          {/* User list */}
          {inviteUsers.length > 0 && (
            <div className="divide-y divide-border">
              {inviteUsers.map(u => (
                <div key={u.id} className="flex items-center justify-between py-2.5 gap-3">
                  <div className="min-w-0">
                    <p className="font-semibold text-sm text-fg truncate">{u.displayName}</p>
                    <p className="text-[11px] text-fg-muted font-mono truncate">{window.location.origin}/auth/link/login?id={u.id}</p>
                  </div>
                  <div className="flex gap-2 shrink-0">
                    <button
                      onClick={() => navigator.clipboard.writeText(`${window.location.origin}/auth/link/login?id=${u.id}`)}
                      className="px-2.5 py-1 rounded-input text-[12px] font-semibold bg-surface-3 text-fg-secondary">
                      Copy link
                    </button>
                    <button
                      onClick={() => handleDeleteUser(u.id)}
                      className="px-2.5 py-1 rounded-input text-[12px] font-semibold"
                      style={{ background: 'var(--live-soft)', color: 'var(--loss)' }}>
                      Remove
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
          {inviteUsers.length === 0 && (
            <p className="text-sm text-fg-muted">No users yet. Create one above.</p>
          )}
        </section>
      )}

      {/* Ingestion health */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="font-display font-bold text-lg tracking-tight">Ingestion Health</h2>
          <button onClick={fetchIngestion} className="text-sm font-medium transition-colors" style={{ color: 'var(--link)' }}>
            Refresh
          </button>
        </div>

        {ingestionError && <p className="text-sm" style={{ color: 'var(--loss)' }}>Failed to load: {ingestionError}</p>}

        {ingestion && (
          <dl className="grid grid-cols-2 gap-3 text-sm">
            {[
              ['Last successful poll', formatDate(ingestion.lastSuccessfulPoll)],
              ['Last attempted poll', formatDate(ingestion.lastAttemptedPoll)],
              ['Total polls', String(ingestion.totalPollCount)],
            ].map(([label, val]) => (
              <div key={label as string}>
                <dt className="text-fg-muted">{label}</dt>
                <dd className="text-fg font-medium">{val}</dd>
              </div>
            ))}
            <div>
              <dt className="text-fg-muted">Error count</dt>
              <dd className="font-medium" style={{ color: ingestion.errorCount > 0 ? 'var(--loss)' : 'var(--fg)' }}>
                {ingestion.errorCount}
              </dd>
            </div>
            <div>
              <dt className="text-fg-muted">Pending fixtures</dt>
              <dd className="font-medium" style={{ color: ingestion.pendingFixtureCount > 0 ? 'var(--warning)' : 'var(--fg)' }}>
                {ingestion.pendingFixtureCount}
              </dd>
            </div>
            {ingestion.lastError && (
              <div className="col-span-2">
                <dt className="text-fg-muted">Last error</dt>
                <dd className="break-all" style={{ color: 'var(--loss)' }}>{ingestion.lastError}</dd>
              </div>
            )}
          </dl>
        )}

        <div className="pt-2">
          <button onClick={handleForceRecompute}
            className="px-4 py-2 rounded-input text-sm font-semibold transition-colors"
            style={{ background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }}>
            Force recompute scores
          </button>
          {recomputeMsg && <p className="mt-2 text-sm" style={{ color: 'var(--win)' }}>{recomputeMsg}</p>}
        </div>
      </section>

      {/* Manual result override */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <h2 className="font-display font-bold text-lg tracking-tight">Manual Result Override</h2>
        <form onSubmit={handleSetResult} className="space-y-3">
          <div>
            <label htmlFor="fixtureId" className={labelCls}>Fixture ID</label>
            <input id="fixtureId" type="text" value={fixtureId} onChange={e => setFixtureId(e.target.value)}
              placeholder="e.g. 1" className={`${inputCls} w-40`} />
          </div>
          <div className="flex items-end gap-3">
            {[
              { id: 'homeScore', label: 'Home score', value: homeScore, set: setHomeScore },
              { id: 'awayScore', label: 'Away score', value: awayScore, set: setAwayScore },
            ].map(({ id, label, value, set }) => (
              <div key={id}>
                <label htmlFor={id} className={labelCls}>{label}</label>
                <input id={id} type="number" min="0" value={value} onChange={e => set(e.target.value)}
                  placeholder="0" className={`${inputCls} w-24`} />
              </div>
            ))}
            <button type="submit"
              className="px-4 py-2 rounded-input text-sm font-semibold transition-colors"
              style={{ background: 'var(--warning)', color: '#fff' }}>
              Set result
            </button>
          </div>
          {resultError && <p className="text-sm" style={{ color: 'var(--loss)' }}>{resultError}</p>}
          {resultMsg && <p className="text-sm" style={{ color: 'var(--win)' }}>{resultMsg}</p>}
        </form>
      </section>

      {/* Override history */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="font-display font-bold text-lg tracking-tight">Override History (last 50)</h2>
          <button onClick={fetchOverrides} className="text-sm font-medium transition-colors" style={{ color: 'var(--link)' }}>
            Refresh
          </button>
        </div>

        {overridesError && <p className="text-sm" style={{ color: 'var(--loss)' }}>Failed to load: {overridesError}</p>}
        {overrides.length === 0 && !overridesError && <p className="text-fg-muted text-sm">No overrides recorded yet.</p>}

        {overrides.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="text-fg-muted border-b border-border text-[10px] font-display font-bold uppercase tracking-wider">
                  <th className="py-2 pr-4">When</th>
                  <th className="py-2 pr-4">Admin</th>
                  <th className="py-2 pr-4">Type</th>
                  <th className="py-2 pr-4">Target</th>
                  <th className="py-2">Description</th>
                </tr>
              </thead>
              <tbody>
                {overrides.map(o => (
                  <tr key={o.id} className="border-b border-border hover:bg-surface-2 transition-colors">
                    <td className="py-2 pr-4 text-fg-muted whitespace-nowrap text-xs">{formatDate(o.overriddenAt)}</td>
                    <td className="py-2 pr-4 text-fg font-medium">{o.adminDisplayName}</td>
                    <td className="py-2 pr-4 text-fg-secondary">{o.targetType}</td>
                    <td className="py-2 pr-4 font-mono text-xs text-fg-secondary">{o.targetId}</td>
                    <td className="py-2 text-fg-secondary">{o.description}</td>
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
