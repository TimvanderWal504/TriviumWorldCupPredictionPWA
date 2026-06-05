/**
 * Provider-agnostic identity types used throughout the application.
 * Feature code imports from here — never from a concrete provider module.
 */

export interface AppUser {
  userId: string;
  displayName: string;
  roles: string[];
}

export interface UserProfile {
  userId: string;
  displayName: string;
  countryCode: string;
}

export interface AuthContextValue {
  /** The currently authenticated user, or null if unauthenticated. */
  user: AppUser | null;
  /** Whether the initial auth state has been loaded from the server. */
  isLoading: boolean;
  /** True when the authenticated user has completed profile setup. */
  hasProfile: boolean;
  /** True when the API is running the link auth provider (admin-managed users). */
  isLinkAuth: boolean;
  /** Sign out the current user. */
  signOut: () => Promise<void>;
  /** Called after profile creation to re-hydrate auth state. */
  onProfileCreated: () => Promise<void>;
}
