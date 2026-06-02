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

/**
 * Profile settings page — lets the authenticated user update their display name and country.
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

  useEffect(() => {
    fetchProfile().then(p => {
      setProfile(p);
      setDisplayName(p?.displayName ?? '');
      setCountryCode(p?.countryCode ?? '');
      setCountrySearch(COUNTRIES.find(c => c.code === p?.countryCode)?.name ?? '');
    }).finally(() => setLoading(false));
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
    </div>
  );
}
