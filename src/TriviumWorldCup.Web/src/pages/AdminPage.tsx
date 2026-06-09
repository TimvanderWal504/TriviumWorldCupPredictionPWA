import { useEffect, useState } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
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

interface PlayerDto {
  id: string;
  name: string;
  teamId: string;
  teamName: string;
  position: string;
  shirtNumber: number | null;
}

export function AdminPage() {
  const { user, isLinkAuth } = useAuth();
  const isAdmin = user?.roles?.includes('admin') ?? false;

  const [inviteUsers, setInviteUsers] = useState<InviteUserDto[]>([]);
  const [userPage, setUserPage] = useState(0);
  const [newUserName, setNewUserName] = useState('');
  const [createdLoginUrl, setCreatedLoginUrl] = useState<string | null>(null);
  const [userError, setUserError] = useState<string | null>(null);
  const [userBusy, setUserBusy] = useState(false);

  const [ingestion, setIngestion] = useState<IngestionStatus | null>(null);
  const [ingestionError, setIngestionError] = useState<string | null>(null);
  const [overrides, setOverrides] = useState<OverrideRecord[]>([]);
  const [overridesError, setOverridesError] = useState<string | null>(null);
  const [deletingOverride, setDeletingOverride] = useState<string | null>(null);
  const [fixtureId, setFixtureId] = useState('');
  const [homeScore, setHomeScore] = useState('');
  const [awayScore, setAwayScore] = useState('');
  const [markAsLive, setMarkAsLive] = useState(false);
  const [elapsedMinute, setElapsedMinute] = useState('');
  const [elapsedExtra, setElapsedExtra] = useState('');
  const [resultMsg, setResultMsg] = useState<string | null>(null);
  const [resultError, setResultError] = useState<string | null>(null);
  const [recomputeMsg, setRecomputeMsg] = useState<string | null>(null);

  const [pushTargetUserId, setPushTargetUserId] = useState('');
  const [pushTitle, setPushTitle] = useState('Test notification');
  const [pushBody, setPushBody] = useState('This is a test notification from the admin panel.');
  const [pushMsg, setPushMsg] = useState<string | null>(null);
  const [pushError, setPushError] = useState<string | null>(null);
  const [pushBusy, setPushBusy] = useState(false);

  const [players, setPlayers] = useState<PlayerDto[]>([]);
  const [playerSearch, setPlayerSearch] = useState('');
  const [goalFixtureId, setGoalFixtureId] = useState('');
  const [goalPlayerId, setGoalPlayerId] = useState('');
  const [goalType, setGoalType] = useState('OpenPlay');
  const [goalMinute, setGoalMinute] = useState('');
  const [goalMsg, setGoalMsg] = useState<string | null>(null);
  const [goalError, setGoalError] = useState<string | null>(null);

  const [cardFixtureId, setCardFixtureId] = useState('');
  const [cardPlayerId, setCardPlayerId] = useState('');
  const [cardPlayerSearch, setCardPlayerSearch] = useState('');
  const [cardType, setCardType] = useState('Yellow');
  const [cardMinute, setCardMinute] = useState('');
  const [cardMsg, setCardMsg] = useState<string | null>(null);
  const [cardError, setCardError] = useState<string | null>(null);

  const [subFixtureId, setSubFixtureId] = useState('');
  const [subPlayerInName, setSubPlayerInName] = useState('');
  const [subPlayerInSearch, setSubPlayerInSearch] = useState('');
  const [subPlayerOutName, setSubPlayerOutName] = useState('');
  const [subPlayerOutSearch, setSubPlayerOutSearch] = useState('');
  const [subTeamId, setSubTeamId] = useState('');
  const [subMinute, setSubMinute] = useState('');
  const [subMsg, setSubMsg] = useState<string | null>(null);
  const [subError, setSubError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAdmin) return;
    fetchIngestion(); fetchOverrides();
    if (isLinkAuth) fetchInviteUsers();
    fetch('/players').then(r => r.json()).then((data: PlayerDto[]) => setPlayers(data)).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin, isLinkAuth]);

  function fetchInviteUsers(resetPage = false) {
    fetch('/admin/users', { credentials: 'include' })
      .then(r => r.json())
      .then((data: InviteUserDto[]) => { setInviteUsers(data); if (resetPage) setUserPage(0); })
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
      fetchInviteUsers(true);
    } catch (err) {
      setUserError(String(err));
    } finally {
      setUserBusy(false);
    }
  }

  async function handleDeleteUser(id: string) {
    if (!confirm('Remove this user? Their predictions stay but they can no longer log in.')) return;
    await fetch(`/admin/users/${id}`, { method: 'DELETE', credentials: 'include' });
    fetchInviteUsers(true);
    setCreatedLoginUrl(null);
  }

  if (!isAdmin) {
    return (
      <div className="max-w-lg mx-auto px-4 py-6">
        <div className="rounded-card bg-surface border border-border p-8 text-center">
          <p className="font-semibold text-lg" style={{ color: 'var(--loss)' }}>Access denied.</p>
          <p className="text-fg-muted text-sm mt-1">You must be an admin to view this page.</p>
        </div>
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
        body: JSON.stringify({
          homeScore: home, awayScore: away, markAsLive,
          elapsedMinute: markAsLive && elapsedMinute !== '' ? parseInt(elapsedMinute, 10) : null,
          elapsedExtra:  markAsLive && elapsedExtra  !== '' ? parseInt(elapsedExtra,  10) : null,
        }),
      });
      if (!res.ok) { const body = await res.json().catch(() => ({})); setResultError((body as { error?: string })?.error ?? `HTTP ${res.status}`); return; }
      setResultMsg(markAsLive
        ? `Fixture ${fixtureId.trim()} set to InProgress: ${home}-${away}`
        : `Result set: ${home}-${away} for fixture ${fixtureId.trim()}`);
      setFixtureId(''); setHomeScore(''); setAwayScore(''); setMarkAsLive(false); setElapsedMinute(''); setElapsedExtra('');
      await fetchOverrides(); await fetchIngestion();
    } catch (err) { setResultError(String(err)); }
  }

  async function handleDeleteOverride(id: string) {
    if (!confirm('Remove this override and revert the underlying result/event? Scores will be recomputed.')) return;
    setDeletingOverride(id);
    try {
      const res = await fetch(`/admin/overrides/${id}`, { method: 'DELETE', credentials: 'include' });
      if (!res.ok) {
        const b = await res.json().catch(() => ({}));
        alert((b as { error?: string }).error ?? `HTTP ${res.status}`);
        return;
      }
      await fetchOverrides();
      await fetchIngestion();
    } finally {
      setDeletingOverride(null);
    }
  }

  async function handleAddGoal(e: React.FormEvent) {
    e.preventDefault(); setGoalMsg(null); setGoalError(null);
    if (!goalFixtureId.trim()) { setGoalError('Fixture ID is required.'); return; }
    if (!goalPlayerId.trim()) { setGoalError('Player is required.'); return; }
    const minute = parseInt(goalMinute, 10);
    if (isNaN(minute) || minute < 1) { setGoalError('Minute must be a positive integer.'); return; }
    try {
      const res = await fetch(`/admin/fixtures/${encodeURIComponent(goalFixtureId.trim())}/goals`, {
        method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerId: goalPlayerId.trim(), type: goalType, minute }),
      });
      if (!res.ok) { const b = await res.json().catch(() => ({})); setGoalError((b as { error?: string }).error ?? `HTTP ${res.status}`); return; }
      const player = players.find(p => p.id === goalPlayerId.trim());
      setGoalMsg(`Goal added: ${player?.name ?? goalPlayerId} (${goalType}, min ${minute}) in fixture ${goalFixtureId.trim()}`);
      setGoalFixtureId(''); setGoalPlayerId(''); setPlayerSearch(''); setGoalMinute(''); setGoalType('OpenPlay');
      await fetchOverrides(); await fetchIngestion();
    } catch (err) { setGoalError(String(err)); }
  }

  async function handleAddCard(e: React.FormEvent) {
    e.preventDefault(); setCardMsg(null); setCardError(null);
    if (!cardFixtureId.trim()) { setCardError('Fixture ID is required.'); return; }
    if (!cardPlayerId.trim()) { setCardError('Player is required.'); return; }
    const minute = parseInt(cardMinute, 10);
    if (isNaN(minute) || minute < 1) { setCardError('Minute must be a positive integer.'); return; }
    try {
      const res = await fetch(`/admin/fixtures/${encodeURIComponent(cardFixtureId.trim())}/cards`, {
        method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerId: cardPlayerId.trim(), type: cardType, minute }),
      });
      if (!res.ok) { const b = await res.json().catch(() => ({})); setCardError((b as { error?: string }).error ?? `HTTP ${res.status}`); return; }
      const player = players.find(p => p.id === cardPlayerId.trim());
      setCardMsg(`Card added: ${player?.name ?? cardPlayerId} (${cardType}, min ${minute}) in fixture ${cardFixtureId.trim()}`);
      setCardFixtureId(''); setCardPlayerId(''); setCardPlayerSearch(''); setCardMinute(''); setCardType('Yellow');
      await fetchOverrides();
    } catch (err) { setCardError(String(err)); }
  }

  async function handleAddSub(e: React.FormEvent) {
    e.preventDefault(); setSubMsg(null); setSubError(null);
    if (!subFixtureId.trim()) { setSubError('Fixture ID is required.'); return; }
    const playerInName = subPlayerInName.trim() || subPlayerInSearch.trim();
    const playerOutName = subPlayerOutName.trim() || subPlayerOutSearch.trim();
    if (!playerInName) { setSubError('Player In name is required.'); return; }
    if (!playerOutName) { setSubError('Player Out name is required.'); return; }
    const minute = parseInt(subMinute, 10);
    if (isNaN(minute) || minute < 1) { setSubError('Minute must be a positive integer.'); return; }
    try {
      const res = await fetch(`/admin/fixtures/${encodeURIComponent(subFixtureId.trim())}/substitutions`, {
        method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerInName, playerOutName, teamId: subTeamId.trim() || null, minute }),
      });
      if (!res.ok) { const b = await res.json().catch(() => ({})); setSubError((b as { error?: string }).error ?? `HTTP ${res.status}`); return; }
      setSubMsg(`Substitution added: ${playerInName} on / ${playerOutName} off at min ${minute} in fixture ${subFixtureId.trim()}`);
      setSubFixtureId(''); setSubPlayerInName(''); setSubPlayerInSearch(''); setSubPlayerOutName(''); setSubPlayerOutSearch(''); setSubTeamId(''); setSubMinute('');
      await fetchOverrides();
    } catch (err) { setSubError(String(err)); }
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

  async function handleSendTestPush(e: React.FormEvent) {
    e.preventDefault();
    setPushMsg(null);
    setPushError(null);
    setPushBusy(true);
    try {
      const res = await fetch('/admin/push/test', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userId: pushTargetUserId.trim() || null,
          title: pushTitle.trim() || null,
          body: pushBody.trim() || null,
        }),
      });
      const data = await res.json() as { sent?: number; message?: string; detail?: string };
      if (!res.ok) {
        setPushError(data.detail ?? `HTTP ${res.status}`);
        return;
      }
      setPushMsg(data.message ?? `Sent ${data.sent} notification(s).`);
    } catch (err) {
      setPushError(String(err));
    } finally {
      setPushBusy(false);
    }
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
                User created. Share this login link:
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

          {/* User list — paginated, 10 per page */}
          {inviteUsers.length > 0 && (() => {
            const PAGE_SIZE = 10;
            const totalPages = Math.max(1, Math.ceil(inviteUsers.length / PAGE_SIZE));
            const page = Math.min(userPage, totalPages - 1);
            const pagedUsers = inviteUsers.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
            const btnBase = 'w-7 h-7 flex items-center justify-center rounded-input transition-opacity disabled:opacity-25';
            return (
              <>
                <div className="divide-y divide-border">
                  {pagedUsers.map(u => (
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
                          onClick={() => navigator.clipboard.writeText(u.id)}
                          className="px-2.5 py-1 rounded-input text-[12px] font-semibold bg-surface-3 text-fg-secondary">
                          Copy ID
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

                {totalPages > 1 && (() => {
                  const windowSize = Math.min(5, totalPages);
                  const windowStart = Math.max(0, Math.min(totalPages - windowSize, page - Math.floor(windowSize / 2)));
                  const visiblePages = Array.from({ length: windowSize }, (_, i) => windowStart + i);
                  return (
                    <div className="flex items-center justify-center gap-3 pt-2">
                      <button onClick={() => setUserPage(p => p - 1)} disabled={page === 0}
                        className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }}
                        aria-label="Previous page">
                        <ChevronLeft size={16} />
                      </button>
                      <div className="flex items-center gap-2.5">
                        {visiblePages.map(pg => {
                          const active = page === pg;
                          return (
                            <button key={pg} onClick={() => setUserPage(pg)} aria-label={`Page ${pg + 1}`}
                              className="flex flex-col items-center gap-0.5">
                              <div className="rounded-full transition-all duration-150" style={{
                                width: active ? 10 : 7, height: active ? 10 : 7,
                                background: active ? 'var(--secondary)' : 'var(--surface-3)',
                              }} />
                              <span className="text-[9px] font-mono leading-none"
                                style={{ color: active ? 'var(--secondary)' : 'var(--fg-muted)' }}>
                                {pg + 1}
                              </span>
                            </button>
                          );
                        })}
                      </div>
                      <button onClick={() => setUserPage(p => p + 1)} disabled={page === totalPages - 1}
                        className={btnBase} style={{ background: 'var(--surface-3)', color: 'var(--fg-secondary)' }}
                        aria-label="Next page">
                        <ChevronRight size={16} />
                      </button>
                    </div>
                  );
                })()}
              </>
            );
          })()}
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

      {/* Push notifications test */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <h2 className="font-display font-bold text-lg tracking-tight">Push Notifications</h2>
        <form onSubmit={handleSendTestPush} className="space-y-3">
          <div>
            <label htmlFor="pushTargetUserId" className={labelCls}>Target user ID <span className="normal-case font-normal">(leave blank to send to yourself)</span></label>
            <input
              id="pushTargetUserId" type="text" value={pushTargetUserId}
              onChange={e => setPushTargetUserId(e.target.value)}
              placeholder="Leave blank for yourself"
              className={`${inputCls} w-full max-w-sm`}
            />
          </div>
          <div>
            <label htmlFor="pushTitle" className={labelCls}>Title</label>
            <input
              id="pushTitle" type="text" value={pushTitle}
              onChange={e => setPushTitle(e.target.value)}
              className={`${inputCls} w-full max-w-sm`}
            />
          </div>
          <div>
            <label htmlFor="pushBody" className={labelCls}>Body</label>
            <input
              id="pushBody" type="text" value={pushBody}
              onChange={e => setPushBody(e.target.value)}
              className={`${inputCls} w-full max-w-sm`}
            />
          </div>
          <button type="submit" disabled={pushBusy}
            className="px-4 py-2 rounded-input text-sm font-semibold transition-colors disabled:opacity-50"
            style={{ background: 'var(--secondary-fill)', color: 'var(--fg-onblue)' }}>
            {pushBusy ? 'Sending…' : 'Send test notification'}
          </button>
          {pushError && <p className="text-sm" style={{ color: 'var(--loss)' }}>{pushError}</p>}
          {pushMsg   && <p className="text-sm" style={{ color: 'var(--win)' }}>{pushMsg}</p>}
        </form>
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
          <div className="flex items-center gap-2">
            <input id="markAsLive" type="checkbox" checked={markAsLive}
              onChange={e => setMarkAsLive(e.target.checked)}
              className="w-4 h-4 accent-[var(--live)]" />
            <label htmlFor="markAsLive" className="text-sm text-fg cursor-pointer select-none">
              Mark as currently live <span className="text-fg-muted">(InProgress — for testing live scores)</span>
            </label>
          </div>
          {markAsLive && (
            <div className="flex items-end gap-3 pl-6">
              <div>
                <label htmlFor="elapsedMinute" className={labelCls}>Elapsed minute</label>
                <input id="elapsedMinute" type="number" min="1" max="120" value={elapsedMinute}
                  onChange={e => setElapsedMinute(e.target.value)}
                  placeholder="e.g. 37" className={`${inputCls} w-24`} />
              </div>
              <div>
                <label htmlFor="elapsedExtra" className={labelCls}>Stoppage time</label>
                <input id="elapsedExtra" type="number" min="1" max="20" value={elapsedExtra}
                  onChange={e => setElapsedExtra(e.target.value)}
                  placeholder="e.g. 3" className={`${inputCls} w-20`} />
              </div>
              <span className="text-xs text-fg-muted pb-2">shows as e.g. LIVE 45+3'</span>
            </div>
          )}
          {resultError && <p className="text-sm" style={{ color: 'var(--loss)' }}>{resultError}</p>}
          {resultMsg && <p className="text-sm" style={{ color: 'var(--win)' }}>{resultMsg}</p>}
        </form>
      </section>

      {/* Goal event override */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <h2 className="font-display font-bold text-lg tracking-tight">Goal Event Override</h2>
        <p className="text-sm text-fg-muted">Add a goal event to test Golden Six scoring. Triggers a full recompute.</p>
        <form onSubmit={handleAddGoal} className="space-y-3">
          <div className="flex gap-3 flex-wrap items-end">
            <div>
              <label htmlFor="goalFixtureId" className={labelCls}>Fixture ID</label>
              <input id="goalFixtureId" type="text" value={goalFixtureId} onChange={e => setGoalFixtureId(e.target.value)}
                placeholder="e.g. 1" className={`${inputCls} w-28`} />
            </div>
            <div>
              <label htmlFor="goalMinute" className={labelCls}>Minute</label>
              <input id="goalMinute" type="number" min="1" value={goalMinute} onChange={e => setGoalMinute(e.target.value)}
                placeholder="e.g. 45" className={`${inputCls} w-24`} />
            </div>
            <div>
              <label htmlFor="goalType" className={labelCls}>Type</label>
              <select id="goalType" value={goalType} onChange={e => setGoalType(e.target.value)}
                className={`${inputCls}`}>
                <option value="OpenPlay">Open play</option>
                <option value="PenaltyInMatch">Penalty (in-match)</option>
                <option value="Shootout">Shootout</option>
                <option value="OwnGoal">Own goal</option>
              </select>
            </div>
          </div>
          <div>
            <label htmlFor="playerSearch" className={labelCls}>Player <span className="normal-case font-normal">(search by name or team)</span></label>
            <input id="playerSearch" type="text" value={playerSearch}
              onChange={e => { setPlayerSearch(e.target.value); setGoalPlayerId(''); }}
              placeholder="Type to search…" className={`${inputCls} w-full max-w-sm`} />
            {playerSearch.trim().length >= 2 && (() => {
              const q = playerSearch.toLowerCase();
              const filtered = players.filter(p =>
                p.name.toLowerCase().includes(q) ||
                p.teamName.toLowerCase().includes(q)
              ).slice(0, 10);
              if (filtered.length === 0) return <p className="text-xs text-fg-muted mt-1">No players found.</p>;
              return (
                <div className="mt-1 border border-border rounded-input overflow-hidden max-w-sm">
                  {filtered.map(p => (
                    <button key={p.id} type="button"
                      onClick={() => { setGoalPlayerId(p.id); setPlayerSearch(`${p.name} (${p.teamName}, ${p.position})`); }}
                      className="w-full text-left px-3 py-2 text-sm hover:bg-surface-2 transition-colors flex items-center justify-between gap-2"
                      style={{ background: goalPlayerId === p.id ? 'var(--secondary-fill)' : undefined }}>
                      <span className="font-medium">{p.name}</span>
                      <span className="text-xs text-fg-muted shrink-0">{p.teamName} · {p.position}{p.shirtNumber != null ? ` #${p.shirtNumber}` : ''}</span>
                    </button>
                  ))}
                </div>
              );
            })()}
            {goalPlayerId && <p className="text-xs mt-1" style={{ color: 'var(--win)' }}>Selected ID: {goalPlayerId}</p>}
          </div>
          <button type="submit"
            className="px-4 py-2 rounded-input text-sm font-semibold transition-colors"
            style={{ background: 'var(--warning)', color: '#fff' }}>
            Add goal event
          </button>
          {goalError && <p className="text-sm" style={{ color: 'var(--loss)' }}>{goalError}</p>}
          {goalMsg   && <p className="text-sm" style={{ color: 'var(--win)' }}>{goalMsg}</p>}
        </form>
      </section>

      {/* Card event override */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <h2 className="font-display font-bold text-lg tracking-tight">Card Event Override</h2>
        <p className="text-sm text-fg-muted">Add a disciplinary card event (display only — does not affect scoring).</p>
        <form onSubmit={handleAddCard} className="space-y-3">
          <div className="flex gap-3 flex-wrap items-end">
            <div>
              <label htmlFor="cardFixtureId" className={labelCls}>Fixture ID</label>
              <input id="cardFixtureId" type="text" value={cardFixtureId} onChange={e => setCardFixtureId(e.target.value)}
                placeholder="e.g. 1" className={`${inputCls} w-28`} />
            </div>
            <div>
              <label htmlFor="cardMinute" className={labelCls}>Minute</label>
              <input id="cardMinute" type="number" min="1" value={cardMinute} onChange={e => setCardMinute(e.target.value)}
                placeholder="e.g. 55" className={`${inputCls} w-24`} />
            </div>
            <div>
              <label htmlFor="cardType" className={labelCls}>Type</label>
              <select id="cardType" value={cardType} onChange={e => setCardType(e.target.value)}
                className={`${inputCls}`}>
                <option value="Yellow">Yellow</option>
                <option value="SecondYellow">Second yellow</option>
                <option value="Red">Red</option>
              </select>
            </div>
          </div>
          <div>
            <label htmlFor="cardPlayerSearch" className={labelCls}>Player <span className="normal-case font-normal">(search by name or team)</span></label>
            <input id="cardPlayerSearch" type="text" value={cardPlayerSearch}
              onChange={e => { setCardPlayerSearch(e.target.value); setCardPlayerId(''); }}
              placeholder="Type to search…" className={`${inputCls} w-full max-w-sm`} />
            {cardPlayerSearch.trim().length >= 2 && (() => {
              const q = cardPlayerSearch.toLowerCase();
              const filtered = players.filter(p =>
                p.name.toLowerCase().includes(q) ||
                p.teamName.toLowerCase().includes(q)
              ).slice(0, 10);
              if (filtered.length === 0) return <p className="text-xs text-fg-muted mt-1">No players found.</p>;
              return (
                <div className="mt-1 border border-border rounded-input overflow-hidden max-w-sm">
                  {filtered.map(p => (
                    <button key={p.id} type="button"
                      onClick={() => { setCardPlayerId(p.id); setCardPlayerSearch(`${p.name} (${p.teamName}, ${p.position})`); }}
                      className="w-full text-left px-3 py-2 text-sm hover:bg-surface-2 transition-colors flex items-center justify-between gap-2"
                      style={{ background: cardPlayerId === p.id ? 'var(--secondary-fill)' : undefined }}>
                      <span className="font-medium">{p.name}</span>
                      <span className="text-xs text-fg-muted shrink-0">{p.teamName} · {p.position}{p.shirtNumber != null ? ` #${p.shirtNumber}` : ''}</span>
                    </button>
                  ))}
                </div>
              );
            })()}
            {cardPlayerId && <p className="text-xs mt-1" style={{ color: 'var(--win)' }}>Selected ID: {cardPlayerId}</p>}
          </div>
          <button type="submit"
            className="px-4 py-2 rounded-input text-sm font-semibold transition-colors"
            style={{ background: 'var(--warning)', color: '#fff' }}>
            Add card event
          </button>
          {cardError && <p className="text-sm" style={{ color: 'var(--loss)' }}>{cardError}</p>}
          {cardMsg   && <p className="text-sm" style={{ color: 'var(--win)' }}>{cardMsg}</p>}
        </form>
      </section>

      {/* Substitution event override */}
      <section className="rounded-card bg-surface border border-border p-5 space-y-4">
        <h2 className="font-display font-bold text-lg tracking-tight">Substitution Event Override</h2>
        <p className="text-sm text-fg-muted">Add a substitution event (display only — does not affect scoring).</p>
        <form onSubmit={handleAddSub} className="space-y-3">
          <div className="flex gap-3 flex-wrap items-end">
            <div>
              <label htmlFor="subFixtureId" className={labelCls}>Fixture ID</label>
              <input id="subFixtureId" type="text" value={subFixtureId} onChange={e => setSubFixtureId(e.target.value)}
                placeholder="e.g. 1" className={`${inputCls} w-28`} />
            </div>
            <div>
              <label htmlFor="subMinute" className={labelCls}>Minute</label>
              <input id="subMinute" type="number" min="1" value={subMinute} onChange={e => setSubMinute(e.target.value)}
                placeholder="e.g. 60" className={`${inputCls} w-24`} />
            </div>
            <div>
              <label htmlFor="subTeamId" className={labelCls}>Team ID <span className="normal-case font-normal">(FIFA code)</span></label>
              <input id="subTeamId" type="text" value={subTeamId} onChange={e => setSubTeamId(e.target.value)}
                placeholder="e.g. NED" className={`${inputCls} w-24`} />
            </div>
          </div>

          {/* Player In */}
          <div>
            <label htmlFor="subPlayerInSearch" className={labelCls}>Player In ▲ <span className="normal-case font-normal">(coming on)</span></label>
            <input id="subPlayerInSearch" type="text" value={subPlayerInSearch}
              onChange={e => { setSubPlayerInSearch(e.target.value); setSubPlayerInName(''); }}
              placeholder="Type to search or enter name…" className={`${inputCls} w-full max-w-sm`} />
            {subPlayerInSearch.trim().length >= 2 && !subPlayerInName && (() => {
              const q = subPlayerInSearch.toLowerCase();
              const filtered = players.filter(p =>
                p.name.toLowerCase().includes(q) || p.teamName.toLowerCase().includes(q)
              ).slice(0, 10);
              if (filtered.length === 0) return <p className="text-xs text-fg-muted mt-1">No players found — name will be used as typed.</p>;
              return (
                <div className="mt-1 border border-border rounded-input overflow-hidden max-w-sm">
                  {filtered.map(p => (
                    <button key={p.id} type="button"
                      onClick={() => { setSubPlayerInName(p.name); setSubPlayerInSearch(p.name); if (!subTeamId) setSubTeamId(p.teamId); }}
                      className="w-full text-left px-3 py-2 text-sm hover:bg-surface-2 transition-colors flex items-center justify-between gap-2">
                      <span className="font-medium">{p.name}</span>
                      <span className="text-xs text-fg-muted shrink-0">{p.teamName} · {p.position}{p.shirtNumber != null ? ` #${p.shirtNumber}` : ''}</span>
                    </button>
                  ))}
                </div>
              );
            })()}
            {subPlayerInName && <p className="text-xs mt-1" style={{ color: 'var(--win)' }}>✓ {subPlayerInName}</p>}
          </div>

          {/* Player Out */}
          <div>
            <label htmlFor="subPlayerOutSearch" className={labelCls}>Player Out ▼ <span className="normal-case font-normal">(coming off)</span></label>
            <input id="subPlayerOutSearch" type="text" value={subPlayerOutSearch}
              onChange={e => { setSubPlayerOutSearch(e.target.value); setSubPlayerOutName(''); }}
              placeholder="Type to search or enter name…" className={`${inputCls} w-full max-w-sm`} />
            {subPlayerOutSearch.trim().length >= 2 && !subPlayerOutName && (() => {
              const q = subPlayerOutSearch.toLowerCase();
              const filtered = players.filter(p =>
                p.name.toLowerCase().includes(q) || p.teamName.toLowerCase().includes(q)
              ).slice(0, 10);
              if (filtered.length === 0) return <p className="text-xs text-fg-muted mt-1">No players found — name will be used as typed.</p>;
              return (
                <div className="mt-1 border border-border rounded-input overflow-hidden max-w-sm">
                  {filtered.map(p => (
                    <button key={p.id} type="button"
                      onClick={() => { setSubPlayerOutName(p.name); setSubPlayerOutSearch(p.name); if (!subTeamId) setSubTeamId(p.teamId); }}
                      className="w-full text-left px-3 py-2 text-sm hover:bg-surface-2 transition-colors flex items-center justify-between gap-2">
                      <span className="font-medium">{p.name}</span>
                      <span className="text-xs text-fg-muted shrink-0">{p.teamName} · {p.position}{p.shirtNumber != null ? ` #${p.shirtNumber}` : ''}</span>
                    </button>
                  ))}
                </div>
              );
            })()}
            {subPlayerOutName && <p className="text-xs mt-1" style={{ color: 'var(--win)' }}>✓ {subPlayerOutName}</p>}
          </div>

          <div className="flex gap-3 items-center flex-wrap pt-1">
            <button type="submit"
              className="px-4 py-2 rounded-input text-sm font-semibold transition-colors"
              style={{ background: 'var(--warning)', color: '#fff' }}>
              Add substitution
            </button>
            <p className="text-xs text-fg-muted">If a player isn't found in the dropdown, type the name directly and press Add.</p>
          </div>
          {subError && <p className="text-sm" style={{ color: 'var(--loss)' }}>{subError}</p>}
          {subMsg   && <p className="text-sm" style={{ color: 'var(--win)' }}>{subMsg}</p>}
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
                  <th className="py-2"></th>
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
                    <td className="py-2 pl-3">
                      <button
                        disabled={deletingOverride === o.id}
                        onClick={() => handleDeleteOverride(o.id)}
                        className="px-2 py-0.5 rounded text-[11px] font-semibold whitespace-nowrap transition-colors disabled:opacity-40"
                        style={{ background: 'var(--live-soft)', color: 'var(--loss)' }}>
                        {deletingOverride === o.id ? '…' : 'Revert'}
                      </button>
                    </td>
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
