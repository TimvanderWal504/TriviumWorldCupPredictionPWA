import { type APIRequestContext } from '@playwright/test';
import { type UserKey, USERS, apiLogin } from './auth.js';

/**
 * API_BASE: the base URL for direct API calls from the seed helpers.
 *
 * In Docker Compose mode the web container (nginx) proxies /e2e/* to the API.
 * BASE_URL should be http://localhost:80 (or http://localhost for nginx).
 * In Vite dev mode Vite proxies /e2e/* to localhost:8080.
 * BASE_URL defaults to http://localhost:5173 for the browser, but API_BASE
 * follows the same root so the proxy routes work.
 *
 * Override with API_BASE env var if the API is exposed directly on a different port.
 */
const API_BASE = process.env['API_BASE'] ?? process.env['BASE_URL'] ?? 'http://localhost:5173';

/**
 * Resets all user-authored data: predictions, profiles, scores, goal events.
 * Fixture reference data (teams, kickoff times, groups) is preserved.
 * Fixture statuses are reset to Scheduled, scores set to null.
 *
 * Call in beforeEach / beforeAll to establish a known DB state.
 * Guard: only runs if the /e2e/reset endpoint is reachable (non-Production).
 */
export async function resetDb(request: APIRequestContext): Promise<void> {
  const resp = await request.post(`${API_BASE}/e2e/reset`);
  if (!resp.ok()) {
    throw new Error(
      `DB reset failed: HTTP ${resp.status()} — is the stack running in non-Production mode?`,
    );
  }
}

/**
 * Restores all fixture kickoff times to their canonical seeded values.
 * Call this in afterEach/afterAll if a test moved kickoffs.
 */
export async function resetFixtureKickoffs(request: APIRequestContext): Promise<void> {
  const resp = await request.post(`${API_BASE}/e2e/reset/fixtures-kickoff`);
  if (!resp.ok()) {
    throw new Error(`Fixture kickoff reset failed: HTTP ${resp.status()}`);
  }
}

/**
 * Seeds a UserProfile for a named seeded user.
 * The profile must exist before the user can navigate past the setup modal.
 *
 * @param request - Playwright APIRequestContext
 * @param user    - key from USERS (e.g. 'alice')
 * @param displayName - override display name (defaults to the user's seed name)
 * @param countryCode - ISO alpha-2 country code (defaults to 'NL')
 */
export async function seedProfile(
  request: APIRequestContext,
  user: UserKey,
  displayName?: string,
  countryCode?: string,
): Promise<void> {
  const u = USERS[user];
  const resp = await request.post(`${API_BASE}/e2e/seed/profile`, {
    data: {
      userId:      u.id,
      displayName: displayName ?? u.name,
      countryCode: countryCode ?? 'NL',
    },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!resp.ok()) {
    throw new Error(`seedProfile("${user}") failed: HTTP ${resp.status()}`);
  }
}

/**
 * Moves a fixture's KickoffUtc to a given offset from "now".
 * Positive offsets = future; negative = past.
 *
 * Example — move fixture "1" to 1 hour ago:
 *   await setFixtureKickoff(request, '1', -60);
 */
export async function setFixtureKickoff(
  request: APIRequestContext,
  fixtureId: string,
  offsetMinutes: number,
): Promise<void> {
  const kickoffUtc = new Date(Date.now() + offsetMinutes * 60_000).toISOString();
  const resp = await request.post(`${API_BASE}/e2e/fixtures/${fixtureId}/kickoff`, {
    data: { kickoffUtc },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!resp.ok()) {
    throw new Error(
      `setFixtureKickoff("${fixtureId}", ${offsetMinutes}m) failed: HTTP ${resp.status()}`,
    );
  }
}

/**
 * Injects a deterministic result for a fixture without hitting the live API.
 * Triggers scoring recompute so leaderboard/standings reflect the result.
 *
 * Requires no authentication — it is a test-control endpoint, not an admin one.
 */
export async function injectResult(
  request: APIRequestContext,
  fixtureId: string,
  homeScore: number,
  awayScore: number,
): Promise<void> {
  const resp = await request.post(`${API_BASE}/e2e/fixtures/${fixtureId}/result`, {
    data: { homeScore, awayScore },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!resp.ok()) {
    throw new Error(
      `injectResult("${fixtureId}", ${homeScore}-${awayScore}) failed: HTTP ${resp.status()}`,
    );
  }
}

/**
 * Convenience: reset DB, seed profiles for all five seeded users.
 * Suitable as a beforeAll for most suites.
 */
export async function resetAndSeedAllProfiles(request: APIRequestContext): Promise<void> {
  await resetDb(request);
  for (const key of ['alice', 'bob', 'charlie', 'diana', 'evan'] as UserKey[]) {
    await seedProfile(request, key);
  }
}

export { apiLogin };
