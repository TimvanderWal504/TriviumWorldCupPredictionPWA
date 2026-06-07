import { useState, type FormEvent } from 'react';

interface SignUpPageProps {
  onSwitchToLogin: () => void;
}

export function SignUpPage({ onSwitchToLogin }: SignUpPageProps) {
  const [email, setEmail]       = useState('');
  const [token, setToken]       = useState<string | null>(null);
  const [error, setError]       = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [copied, setCopied]     = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch('/auth/link/signup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: email.trim() }),
      });
      const body = await res.json() as { token?: string; error?: string };
      if (res.ok && body.token) {
        setToken(body.token);
      } else {
        setError(body.error ?? `Onverwachte fout (${res.status}).`);
      }
    } catch {
      setError('Kon de server niet bereiken. Controleer je verbinding.');
    } finally {
      setSubmitting(false);
    }
  };

  const copyToken = async () => {
    if (!token) return;
    await navigator.clipboard.writeText(token);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  if (token) {
    return (
      <div className="bg-surface rounded-sheet shadow-sheet w-full max-w-md p-8 border border-border">
        <h2 className="font-display font-bold text-2xl tracking-tight mb-1">Account aangemaakt</h2>
        <p className="text-fg-secondary mb-2 text-sm">
          Bewaar onderstaande token zorgvuldig — dit is je wachtwoord om in te loggen.
        </p>
        <p className="text-fg-muted text-xs mb-5">
          De token wordt maar eenmalig getoond. Sla hem op in een wachtwoordmanager of noteer hem.
        </p>
        <div
          className="bg-surface-2 rounded-input border border-border px-4 py-3 font-mono text-sm break-all mb-3 select-all cursor-text"
          aria-label="Jouw login-token"
        >
          {token}
        </div>
        <button
          onClick={copyToken}
          className="w-full font-semibold rounded-input py-2.5 transition-colors mb-6"
          style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
        >
          {copied ? 'Gekopieerd!' : 'Kopieer token'}
        </button>
        <button
          onClick={onSwitchToLogin}
          className="w-full text-sm text-fg-secondary hover:text-fg transition-colors"
        >
          Naar inloggen
        </button>
      </div>
    );
  }

  return (
    <div className="bg-surface rounded-sheet shadow-sheet w-full max-w-md p-8 border border-border">
      <h2 className="font-display font-bold text-2xl tracking-tight mb-1">Account aanmaken</h2>
      <p className="text-fg-secondary mb-6 text-sm">
        Vul je werk-e-mailadres in. Je krijgt daarna een token om mee in te loggen.
      </p>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="signup-email" className="block text-sm font-medium text-fg-secondary mb-1">
            E-mailadres
          </label>
          <input
            id="signup-email"
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            required
            autoFocus
            placeholder="naam@bedrijf.nl"
            className="w-full bg-surface-2 text-fg rounded-input px-4 py-2.5 border border-border placeholder:text-fg-muted"
          />
        </div>

        {error && (
          <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={submitting || !email.trim()}
          className="w-full font-semibold rounded-input py-3 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
        >
          {submitting ? 'Aanmaken…' : 'Account aanmaken'}
        </button>
      </form>

      <p className="text-sm text-fg-muted text-center mt-6">
        Al een account?{' '}
        <button
          onClick={onSwitchToLogin}
          className="text-fg-secondary hover:text-fg transition-colors font-medium"
        >
          Inloggen
        </button>
      </p>
    </div>
  );
}
