import { useState, type FormEvent } from 'react';
import { useAuth } from './useAuth.ts';
import { COUNTRIES } from '../data/countries.ts';

/**
 * Full-screen, non-dismissable modal shown when the authenticated user has
 * no profile yet. Collects display name and country, then calls POST /profile.
 * On success it notifies the auth context so the app proceeds normally.
 */
export function ProfileSetupModal() {
  const { onProfileCreated } = useAuth();
  const [displayName, setDisplayName] = useState('');
  const [countryCode, setCountryCode] = useState('');
  const [countrySearch, setCountrySearch] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const filteredCountries = COUNTRIES.filter(c =>
    c.name.toLowerCase().includes(countrySearch.toLowerCase()) ||
    c.code.toLowerCase().includes(countrySearch.toLowerCase())
  );

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!displayName.trim()) { setError('Display name is required.'); return; }
    if (displayName.trim().length < 2) { setError('Display name must be at least 2 characters.'); return; }
    if (displayName.trim().length > 30) { setError('Display name must be at most 30 characters.'); return; }
    if (!countryCode) { setError('Please select a country.'); return; }

    setSubmitting(true);
    try {
      const res = await fetch('/profile', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ displayName: displayName.trim(), countryCode }),
      });

      if (res.ok) {
        await onProfileCreated();
      } else {
        const body = await res.json().catch(() => ({})) as { error?: string };
        setError(body.error ?? `Unexpected error (${res.status}). Please try again.`);
      }
    } catch {
      setError('Could not reach the server. Check your connection and try again.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-50 p-4">
      <div className="bg-slate-800 rounded-2xl shadow-2xl w-full max-w-md p-8">
        <h2 className="text-2xl font-bold text-white mb-1">Welcome!</h2>
        <p className="text-slate-400 mb-6">Set up your profile to start predicting.</p>

        <form onSubmit={handleSubmit} className="space-y-5">
          {/* Display name */}
          <div>
            <label htmlFor="displayName" className="block text-sm font-medium text-slate-300 mb-1">
              Display name
            </label>
            <input
              id="displayName"
              type="text"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              maxLength={30}
              placeholder="e.g. GoalMachine88"
              className="w-full bg-slate-700 text-white rounded-lg px-4 py-2.5 border border-slate-600
                         focus:outline-none focus:ring-2 focus:ring-blue-500 placeholder:text-slate-500"
            />
            <p className="text-xs text-slate-500 mt-1">{displayName.trim().length}/30 characters</p>
          </div>

          {/* Country search + select */}
          <div>
            <label htmlFor="countrySearch" className="block text-sm font-medium text-slate-300 mb-1">
              Country
            </label>
            <input
              id="countrySearch"
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

          {error && (
            <p className="text-red-400 text-sm bg-red-950/40 rounded-lg px-4 py-2">{error}</p>
          )}

          <button
            type="submit"
            disabled={submitting}
            className="w-full bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 disabled:cursor-not-allowed
                       text-white font-semibold rounded-lg py-3 transition-colors"
          >
            {submitting ? 'Saving…' : 'Save profile'}
          </button>
        </form>
      </div>
    </div>
  );
}
