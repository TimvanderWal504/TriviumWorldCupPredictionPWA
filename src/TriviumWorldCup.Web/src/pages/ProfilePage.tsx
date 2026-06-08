import { useEffect, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/useAuth.ts';
import { COUNTRIES } from '../data/countries.ts';
import type { UserProfile } from '../auth/types.ts';

async function fetchProfile(): Promise<UserProfile | null> {
  const res = await fetch('/profile', { credentials: 'include' });
  if (res.status === 404) return null;
  if (res.ok) return res.json() as Promise<UserProfile>;
  return null;
}
async function updateProfile(displayName: string, countryCode: string): Promise<{ ok: boolean; error?: string }> {
  const res = await fetch('/profile', {
    method: 'PUT', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
    body: JSON.stringify({ displayName, countryCode }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

function pushSupported(): boolean { return 'serviceWorker' in navigator && 'PushManager' in window; }

async function fetchVapidPublicKey(): Promise<string | null> {
  try {
    const res = await fetch('/push/vapid-public-key');
    if (!res.ok) return null;
    const body = await res.json() as { publicKey?: string };
    return body.publicKey ?? null;
  } catch { return null; }
}

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const trimmed = base64String.trim();
  const padding = '='.repeat((4 - (trimmed.length % 4)) % 4);
  const base64 = (trimmed + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = atob(base64);
  const output = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) output[i] = rawData.charCodeAt(i);
  return output;
}

async function getCurrentSubscription(): Promise<PushSubscription | null> {
  if (!pushSupported()) return null;
  try { const reg = await navigator.serviceWorker.ready; return await reg.pushManager.getSubscription(); }
  catch { return null; }
}

async function subscribeUser(vapidPublicKey: string): Promise<{ ok: boolean; error?: string }> {
  try {
    const reg = await navigator.serviceWorker.ready;
    const sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(vapidPublicKey).buffer as ArrayBuffer });
    const json = sub.toJSON();
    const keys = json.keys ?? {};
    const res = await fetch('/push/subscribe', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
      body: JSON.stringify({ endpoint: json.endpoint ?? '', p256dh: (keys as Record<string,string>)['p256dh'] ?? '', auth: (keys as Record<string,string>)['auth'] ?? '' }),
    });
    if (!res.ok) { const b = await res.json().catch(() => ({})) as { error?: string }; return { ok: false, error: b.error ?? `Server error ${res.status}` }; }
    return { ok: true };
  } catch (err) { return { ok: false, error: err instanceof Error ? err.message : String(err) }; }
}

async function unsubscribeUser(sub: PushSubscription): Promise<{ ok: boolean; error?: string }> {
  try {
    const endpoint = sub.endpoint;
    await sub.unsubscribe();
    const res = await fetch('/push/subscribe', {
      method: 'DELETE', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
      body: JSON.stringify({ endpoint }),
    });
    if (!res.ok && res.status !== 204) return { ok: false, error: `Server error ${res.status}` };
    return { ok: true };
  } catch (err) { return { ok: false, error: err instanceof Error ? err.message : String(err) }; }
}

export function ProfilePage() {
  const { user } = useAuth();
  const [_profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [displayName, setDisplayName] = useState('');
  const [countryCode, setCountryCode] = useState('');
  const [countrySearch, setCountrySearch] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [vapidPublicKey, setVapidPublicKey] = useState<string | null>(null);
  const [pushSubscribed, setPushSubscribed] = useState(false);
  const [currentSub, setCurrentSub] = useState<PushSubscription | null>(null);
  const [pushLoading, setPushLoading] = useState(false);
  const [pushError, setPushError] = useState<string | null>(null);
  const [pushSuccess, setPushSuccess] = useState<string | null>(null);
  const [notifPermission, setNotifPermission] = useState<NotificationPermission>('default');

  useEffect(() => {
    fetchProfile().then(p => {
      setProfile(p);
      setDisplayName(p?.displayName ?? '');
      setCountryCode(p?.countryCode ?? '');
      setCountrySearch(COUNTRIES.find(c => c.code === p?.countryCode)?.name ?? '');
    }).finally(() => setLoading(false));
    fetchVapidPublicKey().then(setVapidPublicKey);
    getCurrentSubscription().then(sub => { setCurrentSub(sub); setPushSubscribed(sub !== null); });
    if ('Notification' in window) setNotifPermission(Notification.permission);
  }, []);

  const filteredCountries = COUNTRIES.filter(c =>
    c.name.toLowerCase().includes(countrySearch.toLowerCase()) ||
    c.code.toLowerCase().includes(countrySearch.toLowerCase())
  );

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSaveError(null); setSaveSuccess(false);
    if (!displayName.trim()) { setSaveError('Display name is required.'); return; }
    if (displayName.trim().length < 2) { setSaveError('Display name must be at least 2 characters.'); return; }
    if (displayName.trim().length > 30) { setSaveError('Display name must be at most 30 characters.'); return; }
    if (!countryCode) { setSaveError('Please select a country.'); return; }
    setSaving(true);
    const result = await updateProfile(displayName.trim(), countryCode);
    setSaving(false);
    if (result.ok) { setProfile({ userId: user?.userId ?? '', displayName: displayName.trim(), countryCode }); setSaveSuccess(true); setTimeout(() => setSaveSuccess(false), 3000); }
    else setSaveError(result.error ?? 'Save failed.');
  };

  const handlePushToggle = async () => {
    setPushError(null); setPushSuccess(null);
    if (!vapidPublicKey) { setPushError('Push notifications are not available (server not configured).'); return; }
    setPushLoading(true);
    if (pushSubscribed && currentSub) {
      const result = await unsubscribeUser(currentSub);
      if (result.ok) { setPushSubscribed(false); setCurrentSub(null); setPushSuccess('Reminders off.'); setTimeout(() => setPushSuccess(null), 3000); }
      else setPushError(result.error ?? 'Failed to unsubscribe.');
    } else {
      const permission = await Notification.requestPermission();
      setNotifPermission(permission);
      if (permission !== 'granted') { setPushLoading(false); return; }
      const result = await subscribeUser(vapidPublicKey);
      if (result.ok) { const sub = await getCurrentSubscription(); setPushSubscribed(true); setCurrentSub(sub); setPushSuccess('Reminders on.'); setTimeout(() => setPushSuccess(null), 3000); }
      else setPushError(result.error ?? 'Failed to subscribe.');
    }
    setPushLoading(false);
  };

  if (loading) return <div className="p-8 text-fg-muted">Loading profile…</div>;

  const inputCls = 'w-full bg-surface-2 text-fg rounded-input px-4 py-2.5 border border-border placeholder:text-fg-muted';
  const labelCls = 'block text-sm font-medium text-fg-secondary mb-1';

  return (
    <div className="max-w-lg mx-auto px-4 py-4">

      <form onSubmit={handleSubmit} className="space-y-5">
        <div>
          <label htmlFor="pDisplayName" className={labelCls}>Display name</label>
          <input id="pDisplayName" type="text" value={displayName} onChange={e => setDisplayName(e.target.value)}
            maxLength={30} className={inputCls} />
          <p className="text-xs text-fg-muted mt-1">{displayName.trim().length}/30 characters</p>
        </div>

        <div>
          <label htmlFor="pCountrySearch" className={labelCls}>Country</label>
          <input id="pCountrySearch" type="text" value={countrySearch}
            onChange={e => { setCountrySearch(e.target.value); setCountryCode(''); }}
            placeholder="Search country…" className={`${inputCls} mb-1`} />
          <select size={5} value={countryCode} onChange={e => setCountryCode(e.target.value)}
            className="w-full bg-surface-2 text-fg rounded-input border border-border text-sm">
            {filteredCountries.map(c => <option key={c.code} value={c.code}>{c.name}</option>)}
          </select>
          {countryCode && (
            <p className="text-xs text-fg-muted mt-1">
              Selected: {COUNTRIES.find(c => c.code === countryCode)?.name} ({countryCode})
            </p>
          )}
        </div>

        {saveError && <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>{saveError}</p>}
        {saveSuccess && <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--win)', background: 'var(--win-soft)' }}>Profile saved.</p>}

        <button type="submit" disabled={saving}
          className="font-semibold rounded-input px-6 py-2.5 transition-colors disabled:opacity-50"
          style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}>
          {saving ? 'Saving…' : 'Save changes'}
        </button>
      </form>

      {/* Push notifications */}
      <div className="mt-8 pt-6 border-t border-border">
        <h2 className="font-display font-bold text-lg tracking-tight mb-3">Push notifications</h2>
        {pushSupported() ? (
          <div className="space-y-3">
            {notifPermission === 'denied' && (
              <div className="rounded-input p-4 space-y-2" style={{ background: 'var(--live-soft)', border: '1px solid var(--loss)' }}>
                <p className="text-[13px] font-semibold" style={{ color: 'var(--loss)' }}>Notifications are blocked</p>
                <p className="text-[12px] text-fg-muted leading-relaxed">
                  Your browser has blocked notifications for this site. To re-enable them:
                </p>
                <ol className="text-[12px] text-fg-muted space-y-1 list-decimal list-inside leading-relaxed">
                  <li>Click the <strong className="text-fg">lock icon</strong> in the address bar (next to the URL).</li>
                  <li>Find <strong className="text-fg">Notifications</strong> and set it to <strong className="text-fg">Allow</strong>.</li>
                  <li>Reload the page, then try the toggle again.</li>
                </ol>
              </div>
            )}

            {pushError   && <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>{pushError}</p>}
            {pushSuccess && <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--win)',  background: 'var(--win-soft)'  }}>{pushSuccess}</p>}

            <div className="rounded-card bg-surface border border-border p-4 flex items-center justify-between">
              <div className="pr-4">
                <p className="font-semibold text-[14px]">Lock reminders</p>
                <p className="text-[12px] text-fg-muted mt-0.5">
                  {pushLoading
                    ? 'Updating…'
                    : pushSubscribed
                      ? 'On — you will receive a reminder when you have unfilled predictions near a kickoff.'
                      : 'Off — enable to get reminders before kickoff when you have unfilled predictions.'}
                </p>
              </div>
              <button type="button" onClick={handlePushToggle}
                disabled={pushLoading || !vapidPublicKey || notifPermission === 'denied'}
                aria-pressed={pushSubscribed}
                className="relative w-12 h-7 rounded-chip transition-colors shrink-0 disabled:opacity-50"
                style={{ background: pushSubscribed ? 'var(--primary-fill)' : 'var(--surface-3)' }}>
                <span className="absolute top-1 w-5 h-5 rounded-chip bg-white shadow transition-all"
                      style={{ left: pushSubscribed ? '26px' : '4px' }} />
              </button>
            </div>
          </div>
        ) : (
          <p className="text-sm text-fg-muted">Push notifications are not supported in this browser.</p>
        )}

        <p className="mt-4 text-xs rounded-input px-4 py-3 leading-relaxed"
           style={{ color: 'var(--warning)', background: 'var(--warning-soft)' }}>
          On iPhone and iPad, push notifications only work when the app is installed to your home
          screen (iOS 16.4+). Safari on iOS does not support push.
        </p>
      </div>
    </div>
  );
}
