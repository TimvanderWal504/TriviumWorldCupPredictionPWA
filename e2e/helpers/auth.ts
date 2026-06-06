import { type Page, type APIRequestContext } from '@playwright/test';

/**
 * Stable E2E test user IDs.
 *
 * These GUIDs are seeded into the DB by seedInviteUser() before each suite.
 * They are intentionally different from the production admin GUID so tests
 * never accidentally interact with real user data.
 *
 * The link auth provider validates these IDs against the InviteUser collection
 * in the database — they must be seeded via POST /e2e/seed/invite-user before
 * any login attempt.
 */
export const USERS = {
  alice:   { id: 'a1111111-e2e0-0000-0000-000000000001', name: 'Alice',   roles: ['user'] },
  bob:     { id: 'b2222222-e2e0-0000-0000-000000000002', name: 'Bob',     roles: ['user'] },
  charlie: { id: 'c3333333-e2e0-0000-0000-000000000003', name: 'Charlie', roles: ['user'] },
  diana:   { id: 'd4444444-e2e0-0000-0000-000000000004', name: 'Diana',   roles: ['user', 'admin'] },
  evan:    { id: 'e5555555-e2e0-0000-0000-000000000005', name: 'Evan',    roles: ['user'] },
} as const;

export type UserKey = keyof typeof USERS;

/**
 * Signs in via the link auth provider.
 * Navigates to GET /auth/link/login?id=<userId> — the API validates, sets a
 * 30-day HttpOnly session cookie, then redirects to /.
 *
 * The InviteUser must already exist in the DB (call seedInviteUser first).
 *
 * Caller pattern:
 *   await loginAs(page, 'alice');  // page is now at / with cookie set
 */
export async function loginAs(page: Page, user: UserKey): Promise<void> {
  const userId = USERS[user].id;
  await page.goto(`/auth/link/login?id=${userId}`);
  // The API redirects to / — wait for that navigation to complete
  await page.waitForURL('/');
}

/**
 * Signs in and waits for the app shell to finish rendering.
 */
export async function loginAndWait(page: Page, user: UserKey): Promise<void> {
  await loginAs(page, user);
  await page.waitForSelector('[data-testid="app-nav"], [data-testid="signin-prompt"]', {
    timeout: 10_000,
  });
}

/**
 * Signs out via the link logout endpoint and clears cookies.
 */
export async function logout(page: Page): Promise<void> {
  await page.request.post('/auth/link/logout');
  await page.context().clearCookies();
}

/**
 * Lightweight API-only login (no browser navigation).
 * Makes a GET request to the link login URL; Playwright follows the redirect
 * and stores the resulting Set-Cookie in the APIRequestContext cookie jar.
 *
 * Useful in beforeAll / seed helpers that operate purely at the request level.
 * Note: cookies set here live in the APIRequestContext, not in a browser page.
 */
export async function apiLogin(
  request: APIRequestContext,
  user: UserKey,
): Promise<void> {
  const userId = USERS[user].id;
  const resp = await request.get(`/auth/link/login?id=${userId}`);
  // After the redirect the response is the React app HTML (200).
  // A non-200 here means the redirect chain itself failed.
  if (!resp.ok()) {
    throw new Error(`API login failed for "${user}" (${userId}): HTTP ${resp.status()}`);
  }
}
