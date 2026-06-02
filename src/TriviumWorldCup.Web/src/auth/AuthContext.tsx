import { createContext, useCallback, useEffect, useState, type ReactNode } from 'react';
import type { AppUser, AuthContextValue } from './types.ts';

/**
 * Provider-agnostic auth context.
 * Feature code consumes this via useAuth() — never import a concrete provider.
 */
export const AuthContext = createContext<AuthContextValue | null>(null);

interface MeResponse {
  authenticated: boolean;
  user: AppUser | null;
}

async function fetchCurrentUser(): Promise<AppUser | null> {
  const res = await fetch('/auth/me', { credentials: 'include' });
  if (!res.ok) return null;
  const data = (await res.json()) as MeResponse;
  return data.authenticated ? data.user : null;
}

async function postSignOut(): Promise<void> {
  await fetch('/auth/mock/logout', { method: 'POST', credentials: 'include' });
}

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Wraps the application and exposes auth state.
 * On mount it calls GET /auth/me to hydrate the initial user from the server
 * (the cookie set by the mock provider persists across page refreshes).
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<AppUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    fetchCurrentUser()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setIsLoading(false));
  }, []);

  const signOut = useCallback(async () => {
    await postSignOut();
    setUser(null);
  }, []);

  const value: AuthContextValue = { user, isLoading, signOut };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
