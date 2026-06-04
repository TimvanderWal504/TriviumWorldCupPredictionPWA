import { useEffect, useState } from 'react';
import type { AppUser } from './types.ts';
import { useAuth } from './useAuth.ts';

/**
 * Dev/demo-only inline component for the header.
 * MUST NOT be rendered in a production build.
 */
export function DevUserSwitcher() {
  const { user, signOut } = useAuth();
  const [demoUsers, setDemoUsers] = useState<AppUser[]>([]);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    fetch('/auth/mock/users', { credentials: 'include' })
      .then((r) => r.json())
      .then((data: AppUser[]) => setDemoUsers(data))
      .catch(() => {});
  }, []);

  async function signIn(userId: string) {
    setBusy(true);
    try {
      const res = await fetch('/auth/mock/login', {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId }),
      });
      if (res.ok) window.location.reload();
    } finally { setBusy(false); setOpen(false); }
  }

  async function handleSignOut() {
    setBusy(true);
    try { await signOut(); }
    finally { setBusy(false); setOpen(false); }
  }

  return (
    <div className="relative text-sm">
      <button
        onClick={() => setOpen((o) => !o)}
        className="bg-gold-400 text-ink-950 font-semibold px-2.5 py-1 rounded-input text-[12px] hover:bg-gold-300 transition-colors"
        aria-label="Toggle dev user switcher"
      >
        {user ? `[DEV] ${user.displayName}` : '[DEV]'}
      </button>

      {open && (
        <div className="absolute top-full right-0 mt-1 z-50 bg-surface border border-border-strong rounded-card shadow-raised min-w-48 p-2">
          <p className="text-[11px] text-fg-muted px-1 pb-1 border-b border-border mb-1">Switch demo user</p>
          {demoUsers.map((u) => (
            <button
              key={u.userId}
              disabled={busy || u.userId === user?.userId}
              onClick={() => signIn(u.userId)}
              className="block w-full text-left px-2 py-1.5 rounded-input text-[13px] hover:bg-surface-2 transition-colors disabled:opacity-40 disabled:cursor-not-allowed text-fg"
            >
              {u.displayName}
              {u.userId === user?.userId && <span className="ml-1 text-[11px] text-fg-muted">(current)</span>}
            </button>
          ))}
          {user && (
            <div className="border-t border-border mt-1 pt-1">
              <button
                disabled={busy}
                onClick={handleSignOut}
                className="block w-full text-left px-2 py-1.5 rounded-input text-[13px] transition-colors disabled:opacity-40"
                style={{ color: 'var(--loss)' }}
              >
                Sign out
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
