import { useState, type FormEvent } from 'react';

interface LoginPageProps {
  onLoggedIn: () => Promise<void>;
  onSwitchToSignUp: () => void;
}

export function LoginPage({ onLoggedIn, onSwitchToSignUp }: LoginPageProps) {
  const [email, setEmail]       = useState('');
  const [token, setToken]       = useState('');
  const [error, setError]       = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch('/auth/link/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ email: email.trim(), token: token.trim() }),
      });
      if (res.ok) {
        await onLoggedIn();
      } else {
        const body = await res.json().catch(() => ({})) as { error?: string };
        setError(body.error ?? 'Invalid credentials. Check your email address and token.');
      }
    } catch {
      setError('Could not reach the server. Check your connection.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="bg-surface rounded-sheet shadow-sheet w-full max-w-md p-8 border border-border">
      <h2 className="font-display font-bold text-2xl tracking-tight mb-1">Sign in</h2>
      <p className="text-fg-secondary mb-6 text-sm">
        Enter your Trivium email address and personal token.
      </p>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="login-email" className="block text-sm font-medium text-fg-secondary mb-1">
            Email address
          </label>
          <input
            id="login-email"
            type="email"
            value={email}
            onChange={e => setEmail(e.target.value)}
            required
            autoFocus
            placeholder="name@trivium-esolutions.com"
            className="w-full bg-surface-2 text-fg rounded-input px-4 py-2.5 border border-border placeholder:text-fg-muted"
          />
        </div>
        <div>
          <label htmlFor="login-token" className="block text-sm font-medium text-fg-secondary mb-1">
            Token
          </label>
          <input
            id="login-token"
            type="password"
            value={token}
            onChange={e => setToken(e.target.value)}
            required
            placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
            className="w-full bg-surface-2 text-fg rounded-input px-4 py-2.5 border border-border placeholder:text-fg-muted font-mono text-sm"
          />
        </div>

        {error && (
          <p className="text-[13px] px-4 py-2 rounded-input" style={{ color: 'var(--loss)', background: 'var(--live-soft)' }}>
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={submitting || !email.trim() || !token.trim()}
          className="w-full font-semibold rounded-input py-3 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          style={{ background: 'var(--primary-fill)', color: 'var(--fg-onbrand)' }}
        >
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

      <p className="text-sm text-fg-muted text-center mt-6">
        Don't have an account?{' '}
        <button
          onClick={onSwitchToSignUp}
          className="text-fg-secondary hover:text-fg transition-colors font-medium"
        >
          Create account
        </button>
      </p>
    </div>
  );
}
