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
  authProvider?: string;
}

interface MeResult {
  user: AppUser | null;
  authProvider: string | null;
}

async function fetchCurrentUser(): Promise<MeResult> {
  const res = await fetch('/auth/me', { credentials: 'include' });
  if (!res.ok) return { user: null, authProvider: null };
  const data = (await res.json()) as MeResponse;
  return {
    user: data.authenticated ? data.user : null,
    authProvider: data.authProvider ?? null,
  };
}

async function checkProfileExists(_userId: string): Promise<boolean> {
  const res = await fetch('/profile', { credentials: 'include' });
  if (res.status === 404) return false;
  if (res.ok) return true;
  // Unexpected — treat as no profile so setup modal appears.
  return false;
}

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Wraps the application and exposes auth state including profile status.
 * On mount it calls GET /auth/me to hydrate the initial user, then checks
 * GET /profile to determine whether profile setup is needed.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<AppUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [hasProfile, setHasProfile] = useState(false);
  const [authProvider, setAuthProvider] = useState<string | null>(null);

  const isLinkAuth = authProvider === 'link';

  const loadAuthState = useCallback(async () => {
    try {
      const { user: currentUser, authProvider: provider } = await fetchCurrentUser();
      setUser(currentUser);
      setAuthProvider(provider);
      if (currentUser) {
        const profileExists = await checkProfileExists(currentUser.userId);
        setHasProfile(profileExists);
      } else {
        setHasProfile(false);
      }
    } catch {
      setUser(null);
      setHasProfile(false);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadAuthState();
  }, [loadAuthState]);

  const signOut = useCallback(async () => {
    await fetch('/auth/link/logout', { method: 'POST', credentials: 'include' });
    setUser(null);
    setHasProfile(false);
  }, []);

  // Called by ProfileSetupModal after successful POST /profile.
  // Re-fetches /auth/me so the display name reflects the chosen profile name.
  const onProfileCreated = useCallback(async () => {
    const { user: updatedUser } = await fetchCurrentUser();
    setUser(updatedUser);
    setHasProfile(true);
  }, []);

  const value: AuthContextValue = { user, isLoading, hasProfile, isLinkAuth, signOut, onProfileCreated };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
