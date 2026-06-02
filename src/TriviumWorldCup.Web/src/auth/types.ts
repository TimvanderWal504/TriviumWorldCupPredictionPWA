/**
 * Provider-agnostic identity types used throughout the application.
 * Feature code imports from here — never from a concrete provider module.
 */

export interface AppUser {
  userId: string;
  displayName: string;
  roles: string[];
}

export interface AuthContextValue {
  /** The currently authenticated user, or null if unauthenticated. */
  user: AppUser | null;
  /** Whether the initial auth state has been loaded from the server. */
  isLoading: boolean;
  /** Sign out the current user. */
  signOut: () => Promise<void>;
}
