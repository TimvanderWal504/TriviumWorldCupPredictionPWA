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

async function checkProfileExists(_userId: string): Promise<boolean> {
  const res = await fetch('/profile', { credentials: 'include' });
  if (res.status === 404) return false;
  if (res.ok) return true;
  // Unexpected — treat as no profile so setup modal appears.
  return false;
}

async function postSignOut(): Promise<void> {
  await fetch('/auth/mock/logout', { method: 'POST', credentials: 'include' });
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

  const loadAuthState = useCallback(async () => {
    try {
      const currentUser = await fetchCurrentUser();
      setUser(currentUser);
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
    await postSignOut();
    setUser(null);
    setHasProfile(false);
  }, []);

  // Called by ProfileSetupModal after successful POST /profile.
  // Re-fetches /auth/me so the display name reflects the chosen profile name.
  const onProfileCreated = useCallback(async () => {
    const updatedUser = await fetchCurrentUser();
    setUser(updatedUser);
    setHasProfile(true);
  }, []);

  const value: AuthContextValue = { user, isLoading, hasProfile, signOut, onProfileCreated };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
