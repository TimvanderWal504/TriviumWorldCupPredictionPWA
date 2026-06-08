import { useState, type FormEvent } from 'react';
import { useAuth } from './useAuth.ts';
import { COUNTRIES } from '../data/countries.ts';

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
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ background: 'var(--overlay)' }}>
      <div className="bg-surface rounded-sheet shadow-sheet w-full max-w-md p-8 border border-border">
        <h2 className="font-display font-bold text-2xl tracking-tight mb-1">Welcome!</h2>
        <p className="text-fg-secondary mb-6">Set up your profile to start predicting.</p>

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label htmlFor="displayName" className="block text-sm font-medium text-fg-secondary mb-1">
              Display name
            </label>
            <input
              id="displayName"
              type="text"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              maxLength={30}
              placeholder="e.g. GoalMachine88"
              className="w-full bg-surface-2 text-fg rounded-input px-4 py-2.5 border border-border placeholder:text-fg-muted"
            />
            <p className="text-xs text-fg-muted mt-1">{displayName.trim().length}/30 characters</p>
          </div>

          <div>
            <label htmlFor="countrySearch" className="block text-sm font-medium text-fg-secondary mb-1">
              Country
            </label>
            <input
              id="countrySearch"
              type="text"
              value={countrySearch}
              onChange={e => { setCountrySearch(e.target.value); setCountryCode(''); }}
              placeholder="Search country…"
              className="w-full bg-surface-2 text-fg rounded-input px-4 py-2.5 border border-border placeholder:text-fg-muted mb-1"
            />
            <div className="w-full bg-surface-2 rounded-input border border-border overflow-y-auto"
                 style={{ maxHeight: '10rem' }} role="listbox" aria-label="Country list">
              {filteredCountries.length === 0 && (
                <p className="px-3 py-2 text-sm text-fg-muted">No results.</p>
              )}
              {filteredCountries.map(c => (
                <button key={c.code} type="button" role="option" aria-selected={countryCode === c.code}
                  onClick={() => { setCountryCode(c.code); setCountrySearch(c.name); }}
                  className="w-full text-left px-3 py-1.5 text-sm transition-colors"
                  style={countryCode === c.code
                    ? { background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }
                    : undefined}>
                  {c.name}
                </button>
              ))}
            </div>
            {countryCode && (
              <p className="text-xs text-fg-muted mt-1">
                Selected: {COUNTRIES.find(c => c.code === countryCode)?.name} ({countryCode})
              </p>
            )}
          </div>

          {error && (
            <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={submitting}
            className="w-full font-semibold rounded-input py-3 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
          >
            {submitting ? 'Saving…' : 'Save profile'}
          </button>
        </form>
      </div>
    </div>
  );
}
