import { useContext } from 'react';
import { AuthContext } from './AuthContext.tsx';
import type { AuthContextValue } from './types.ts';

/**
 * Returns the current auth context.
 * Must be used inside an <AuthProvider>.
 * Feature code only depends on this hook — never on a concrete provider.
 */
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (ctx === null) {
    throw new Error('useAuth() must be used within an <AuthProvider>.');
  }
  return ctx;
}
