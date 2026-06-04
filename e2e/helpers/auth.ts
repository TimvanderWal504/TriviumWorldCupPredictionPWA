import { type Page, type APIRequestContext } from '@playwright/test';

/**
 * Stable seeded user IDs from MockUsers.cs.
 * These GUIDs must never change — they match the values in the API.
 */
export const USERS = {
  alice:   { id: 'a1b2c3d4-0001-0001-0001-000000000001', name: 'Alice',   roles: ['user'] },
  bob:     { id: 'a1b2c3d4-0002-0002-0002-000000000002', name: 'Bob',     roles: ['user'] },
  charlie: { id: 'a1b2c3d4-0003-0003-0003-000000000003', name: 'Charlie', roles: ['user'] },
  diana:   { id: 'a1b2c3d4-0004-0004-0004-000000000004', name: 'Diana',   roles: ['user', 'admin'] },
  evan:    { id: 'a1b2c3d4-0005-0005-0005-000000000005', name: 'Evan',    roles: ['user'] },
} as const;

export type UserKey = keyof typeof USERS;

/**
 * Signs in as a named seeded user via the mock auth endpoint.
 * Sets the twc_mock_user cookie on the page context so subsequent
 * navigation is authenticated.
 *
 * Caller pattern:
 *   await loginAs(page, 'alice');
 *   await page.goto('/');
 */
export async function loginAs(page: Page, user: UserKey): Promise<void> {
  const userId = USERS[user].id;
  const baseUrl = page.context().browser()?.browserType().name() === 'chromium'
    ? (process.env['BASE_URL'] ?? 'http://localhost:5173')
    : (process.env['BASE_URL'] ?? 'http://localhost:5173');

  // POST to mock login via the page's request context so the cookie is set
  const response = await page.request.post('/auth/mock/login', {
    data: { userId },
    headers: { 'Content-Type': 'application/json' },
  });

  if (!response.ok()) {
    throw new Error(
      `Mock login failed for user "${user}" (${userId}): HTTP ${response.status()}`,
    );
  }
}

/**
 * Logs in as the named user, navigates to /, and waits for the app to finish
 * loading (the nav bar appears when authenticated + profile exists).
 */
export async function loginAndWait(page: Page, user: UserKey): Promise<void> {
  await loginAs(page, user);
  await page.goto('/');
  // Wait until either the nav bar (authenticated) or the sign-in prompt appears
  await page.waitForSelector('[data-testid="app-nav"], [data-testid="signin-prompt"]', {
    timeout: 10_000,
  });
}

/**
 * Signs out via the mock logout endpoint and clears cookies.
 */
export async function logout(page: Page): Promise<void> {
  await page.request.post('/auth/mock/logout');
  await page.context().clearCookies();
}

/**
 * Lightweight API-only login (no browser navigation).
 * Useful in beforeAll / seed helpers that work at the request level.
 */
export async function apiLogin(
  request: APIRequestContext,
  user: UserKey,
): Promise<void> {
  const userId = USERS[user].id;
  const resp = await request.post('/auth/mock/login', {
    data: { userId },
    headers: { 'Content-Type': 'application/json' },
  });
  if (!resp.ok()) {
    throw new Error(`API login failed for "${user}": HTTP ${resp.status()}`);
  }
}
