import { useEffect, useState } from 'react';
import type { AppUser } from './types.ts';
import { useAuth } from './useAuth.ts';

/**
 * Dev/demo-only component that renders a floating user switcher.
 * MUST NOT be rendered in a production build — the caller is responsible for
 * gating this behind an environment check (see App.tsx).
 *
 * Calls the mock provider endpoints directly — keep this component in the
 * auth/mock module so it is easy to exclude when switching providers.
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
      .catch(() => {/* silently ignore if mock endpoints aren't registered */});
  }, []);

  async function signIn(userId: string) {
    setBusy(true);
    try {
      const res = await fetch('/auth/mock/login', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId }),
      });
      if (res.ok) {
        // Reload so AuthContext re-fetches /auth/me
        window.location.reload();
      }
    } finally {
      setBusy(false);
      setOpen(false);
    }
  }

  async function handleSignOut() {
    setBusy(true);
    try {
      await signOut();
    } finally {
      setBusy(false);
      setOpen(false);
    }
  }

  return (
    <div className="fixed bottom-4 right-4 z-50 text-sm">
      <button
        onClick={() => setOpen((o) => !o)}
        className="bg-yellow-400 text-black font-semibold px-3 py-1 rounded shadow-lg hover:bg-yellow-300 focus:outline-none"
        aria-label="Toggle dev user switcher"
      >
        {user ? `[DEV] ${user.displayName}` : '[DEV] Not signed in'}
      </button>

      {open && (
        <div className="absolute bottom-10 right-0 bg-white text-black rounded shadow-xl border border-gray-200 min-w-48 p-2">
          <p className="text-xs text-gray-500 px-1 pb-1 border-b mb-1">Switch demo user</p>
          {demoUsers.map((u) => (
            <button
              key={u.userId}
              disabled={busy || u.userId === user?.userId}
              onClick={() => signIn(u.userId)}
              className="block w-full text-left px-2 py-1 rounded hover:bg-gray-100 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {u.displayName}
              {u.userId === user?.userId && <span className="ml-1 text-xs text-gray-400">(current)</span>}
            </button>
          ))}
          {user && (
            <>
              <div className="border-t mt-1 pt-1">
                <button
                  disabled={busy}
                  onClick={handleSignOut}
                  className="block w-full text-left px-2 py-1 rounded text-red-600 hover:bg-red-50 disabled:opacity-40"
                >
                  Sign out
                </button>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}
