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
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ displayName, countryCode }),
  });
  if (res.ok) return { ok: true };
  const body = await res.json().catch(() => ({})) as { error?: string };
  return { ok: false, error: body.error ?? `Error ${res.status}` };
}

// ── Push helpers ─────────────────────────────────────────────────────────────

function pushSupported(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in window;
}

async function fetchVapidPublicKey(): Promise<string | null> {
  try {
    const res = await fetch('/push/vapid-public-key');
    if (!res.ok) return null;
    const body = await res.json() as { publicKey?: string };
    return body.publicKey ?? null;
  } catch {
    return null;
  }
}

/** Converts a base64url VAPID public key string to a Uint8Array for PushManager. */
function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

/** Returns the current PushSubscription if the user is already opted in, else null. */
async function getCurrentSubscription(): Promise<PushSubscription | null> {
  if (!pushSupported()) return null;
  try {
    const reg = await navigator.serviceWorker.ready;
    return await reg.pushManager.getSubscription();
  } catch {
    return null;
  }
}

async function subscribeUser(vapidPublicKey: string): Promise<{ ok: boolean; error?: string }> {
  try {
    const reg = await navigator.serviceWorker.ready;
    const sub = await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(vapidPublicKey).buffer as ArrayBuffer,
    });

    const json = sub.toJSON();
    const keys = json.keys ?? {};
    const endpoint = json.endpoint ?? '';
    const p256dh = (keys as Record<string, string>)['p256dh'] ?? '';
    const auth   = (keys as Record<string, string>)['auth']   ?? '';

    const res = await fetch('/push/subscribe', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ endpoint, p256dh, auth }),
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({})) as { error?: string };
      return { ok: false, error: body.error ?? `Server error ${res.status}` };
    }
    return { ok: true };
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { ok: false, error: msg };
  }
}

async function unsubscribeUser(sub: PushSubscription): Promise<{ ok: boolean; error?: string }> {
  try {
    const endpoint = sub.endpoint;
    await sub.unsubscribe();

    const res = await fetch('/push/subscribe', {
      method: 'DELETE',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ endpoint }),
    });
    // 204 or 200 both indicate success (idempotent)
    if (!res.ok && res.status !== 204) {
      return { ok: false, error: `Server error ${res.status}` };
    }
    return { ok: true };
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { ok: false, error: msg };
  }
}

// ── Component ─────────────────────────────────────────────────────────────────

/**
 * Profile settings page — lets the authenticated user update their display name and country,
 * and manage their push notification subscription.
 */
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

  // Push notification state
  const [vapidPublicKey, setVapidPublicKey] = useState<string | null>(null);
  const [pushSubscribed, setPushSubscribed] = useState<boolean>(false);
  const [currentSub, setCurrentSub] = useState<PushSubscription | null>(null);
  const [pushLoading, setPushLoading] = useState(false);
  const [pushError, setPushError] = useState<string | null>(null);
  const [pushSuccess, setPushSuccess] = useState<string | null>(null);

  useEffect(() => {
    fetchProfile().then(p => {
      setProfile(p);
      setDisplayName(p?.displayName ?? '');
      setCountryCode(p?.countryCode ?? '');
      setCountrySearch(COUNTRIES.find(c => c.code === p?.countryCode)?.name ?? '');
    }).finally(() => setLoading(false));

    // Load VAPID key and current subscription status
    fetchVapidPublicKey().then(key => setVapidPublicKey(key));
    getCurrentSubscription().then(sub => {
      setCurrentSub(sub);
      setPushSubscribed(sub !== null);
    });
  }, []);

  const filteredCountries = COUNTRIES.filter(c =>
    c.name.toLowerCase().includes(countrySearch.toLowerCase()) ||
    c.code.toLowerCase().includes(countrySearch.toLowerCase())
  );

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSaveError(null);
    setSaveSuccess(false);

    if (!displayName.trim()) { setSaveError('Display name is required.'); return; }
    if (displayName.trim().length < 2) { setSaveError('Display name must be at least 2 characters.'); return; }
    if (displayName.trim().length > 30) { setSaveError('Display name must be at most 30 characters.'); return; }
    if (!countryCode) { setSaveError('Please select a country.'); return; }

    setSaving(true);
    const result = await updateProfile(displayName.trim(), countryCode);
    setSaving(false);

    if (result.ok) {
      setProfile({ userId: user?.userId ?? '', displayName: displayName.trim(), countryCode });
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } else {
      setSaveError(result.error ?? 'Save failed.');
    }
  };

  const handlePushToggle = async () => {
    setPushError(null);
    setPushSuccess(null);

    if (!vapidPublicKey) {
      setPushError('Push notifications are not available (server not configured).');
      return;
    }

    setPushLoading(true);
    if (pushSubscribed && currentSub) {
      const result = await unsubscribeUser(currentSub);
      if (result.ok) {
        setPushSubscribed(false);
        setCurrentSub(null);
        setPushSuccess('Push notifications disabled.');
        setTimeout(() => setPushSuccess(null), 3000);
      } else {
        setPushError(result.error ?? 'Failed to unsubscribe.');
      }
    } else {
      // Check permission first
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        setPushError('Notification permission was denied. Please allow notifications in your browser settings.');
        setPushLoading(false);
        return;
      }
      const result = await subscribeUser(vapidPublicKey);
      if (result.ok) {
        const sub = await getCurrentSubscription();
        setPushSubscribed(true);
        setCurrentSub(sub);
        setPushSuccess('Push notifications enabled.');
        setTimeout(() => setPushSuccess(null), 3000);
      } else {
        setPushError(result.error ?? 'Failed to subscribe.');
      }
    }
    setPushLoading(false);
  };

  if (loading) {
    return <div className="p-8 text-slate-400">Loading profile…</div>;
  }

  return (
    <div className="max-w-lg mx-auto p-6">
      <h1 className="text-2xl font-bold text-white mb-6">Your profile</h1>

      <form onSubmit={handleSubmit} className="space-y-5">
        {/* Display name */}
        <div>
          <label htmlFor="pDisplayName" className="block text-sm font-medium text-slate-300 mb-1">
            Display name
          </label>
          <input
            id="pDisplayName"
            type="text"
            value={displayName}
            onChange={e => setDisplayName(e.target.value)}
            maxLength={30}
            className="w-full bg-slate-700 text-white rounded-lg px-4 py-2.5 border border-slate-600
                       focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <p className="text-xs text-slate-500 mt-1">{displayName.trim().length}/30 characters</p>
        </div>

        {/* Country */}
        <div>
          <label htmlFor="pCountrySearch" className="block text-sm font-medium text-slate-300 mb-1">
            Country
          </label>
          <input
            id="pCountrySearch"
            type="text"
            value={countrySearch}
            onChange={e => { setCountrySearch(e.target.value); setCountryCode(''); }}
            placeholder="Search country…"
            className="w-full bg-slate-700 text-white rounded-lg px-4 py-2.5 border border-slate-600
                       focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder:text-slate-500 mb-1"
          />
          <select
            size={5}
            value={countryCode}
            onChange={e => setCountryCode(e.target.value)}
            className="w-full bg-slate-700 text-white rounded-lg border border-slate-600
                       focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm"
          >
            {filteredCountries.map(c => (
              <option key={c.code} value={c.code}>{c.name}</option>
            ))}
          </select>
          {countryCode && (
            <p className="text-xs text-slate-400 mt-1">
              Selected: {COUNTRIES.find(c => c.code === countryCode)?.name} ({countryCode})
            </p>
          )}
        </div>

        {saveError && (
          <p className="text-red-400 text-sm bg-red-950/40 rounded-lg px-4 py-2">{saveError}</p>
        )}
        {saveSuccess && (
          <p className="text-green-400 text-sm bg-green-950/40 rounded-lg px-4 py-2">Profile saved.</p>
        )}

        <button
          type="submit"
          disabled={saving}
          className="bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 disabled:cursor-not-allowed
                     text-white font-semibold rounded-lg px-6 py-2.5 transition-colors"
        >
          {saving ? 'Saving…' : 'Save changes'}
        </button>
      </form>

      {/* ── Push notifications ───────────────────────────────────────────────── */}
      <div className="mt-8 pt-6 border-t border-slate-700">
        <h2 className="text-lg font-semibold text-white mb-3">Push notifications</h2>

        {pushSupported() ? (
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-sm text-slate-300">
                Prediction lock reminders
              </span>
              <button
                type="button"
                onClick={handlePushToggle}
                disabled={pushLoading || vapidPublicKey === null}
                aria-pressed={pushSubscribed}
                className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors
                  focus:outline-none focus:ring-2 focus:ring-blue-500
                  ${pushSubscribed ? 'bg-blue-600' : 'bg-slate-600'}
                  ${pushLoading || vapidPublicKey === null ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
              >
                <span
                  className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform
                    ${pushSubscribed ? 'translate-x-6' : 'translate-x-1'}`}
                />
              </button>
            </div>
            <p className="text-xs text-slate-500">
              {pushSubscribed
                ? 'You will receive a reminder when you have unfilled predictions near a kickoff.'
                : 'Enable to get reminders before kickoff when you have unfilled predictions.'}
            </p>

            {pushError && (
              <p className="text-red-400 text-sm bg-red-950/40 rounded-lg px-4 py-2">{pushError}</p>
            )}
            {pushSuccess && (
              <p className="text-green-400 text-sm bg-green-950/40 rounded-lg px-4 py-2">{pushSuccess}</p>
            )}
          </div>
        ) : (
          <p className="text-sm text-slate-400">
            Push notifications are not supported in this browser.
          </p>
        )}

        {/* iOS notice — always shown */}
        <p className="mt-4 text-xs text-amber-400/80 bg-amber-950/30 rounded-lg px-4 py-3 leading-relaxed">
          On iPhone and iPad, push notifications only work when the app is installed to your home
          screen (iOS 16.4+). Safari on iOS does not support push.
        </p>
      </div>
    </div>
  );
}
