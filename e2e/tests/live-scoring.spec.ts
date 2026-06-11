import { test, expect } from '@playwright/test';
import { loginAndWait, apiLogin } from '../helpers/auth.js';
import {
  resetDb,
  resetFixtureKickoffs,
  seedInviteUser,
  seedProfile,
  setFixtureKickoff,
  setFixtureInProgress,
  injectGoal,
  injectResult,
  getAnyPlayerId,
} from '../helpers/seed.js';
import { AppPage } from '../pages/AppPage.js';
import { LiveScoresPage } from '../pages/LiveScoresPage.js';

/**
 * TWC-30 — Live updates: scores move mid-match, points only at full-time.
 *
 * Three tests cover the three acceptance criteria:
 *   AC1 — Mid-match score and goal entry appear on the Live Scores page.
 *   AC2 — Standings (group points, total) are unchanged while a match is InProgress.
 *   AC3 — After the match reaches full-time, points update to reflect the result.
 *
 * Setup: Alice predicts 1-0 for match 1 (fixture "1") before kickoff, then the
 * test harness moves kickoff to the past, sets the fixture to InProgress with
 * a 1-0 live score, and injects one goal event.
 */

// Module-level variable so the goal player ID resolved in beforeAll is available in test 1.
let anyPlayerId: string;

test.describe('TWC-30 live updates', () => {
  test.beforeAll(async ({ request }) => {
    await resetDb(request);
    await seedInviteUser(request, 'alice');
    await seedProfile(request, 'alice');

    // Submit Alice's group prediction while the kickoff is still in the future (not locked).
    await apiLogin(request, 'alice');
    const predResp = await request.post('/predictions/group/1', {
      data: { homeScore: 1, awayScore: 0 },
      headers: { 'Content-Type': 'application/json' },
    });
    if (!predResp.ok()) {
      throw new Error(`Prediction submission failed: HTTP ${predResp.status()}`);
    }

    // Move kickoff 30 minutes into the past so the fixture enters the live window.
    await setFixtureKickoff(request, '1', -30);

    // Set the fixture to InProgress with a live score of 1-0.
    await setFixtureInProgress(request, '1', 1, 0);

    // Inject one goal event so the goal list appears on the live page.
    anyPlayerId = await getAnyPlayerId(request);
    await injectGoal(request, '1', anyPlayerId, 23);
  });

  test.afterAll(async ({ request }) => {
    await resetFixtureKickoffs(request);
  });

  // ── AC1: Mid-match score and goal entry visible on the Live Scores page ─────

  test('live fixture card shows LIVE badge, current score, and goal entry', async ({ page }) => {
    await loginAndWait(page, 'alice');
    const app = new AppPage(page);

    // goToLive() waits for the Live tab to become visible (liveWindowActive must be true).
    await app.goToLive();
    const livePage = new LiveScoresPage(page);
    await livePage.waitForLoad();

    // LIVE badge is present on the page.
    await expect(livePage.liveBadge()).toBeVisible();

    // The live fixture card itself is visible.
    const card = livePage.liveFixtureCard();
    await expect(card).toBeVisible();

    // The live score 1-0 is rendered on the card.
    await expect(card).toContainText('1');
    await expect(card).toContainText('0');

    // The injected goal's minute "23'" appears in the goal list.
    await expect(card.getByText("23'")).toBeVisible();
  });

  // ── AC2: Standings unchanged while match is InProgress ──────────────────────

  test('standings are unchanged while match is in progress', async ({ request }) => {
    await apiLogin(request, 'alice');
    const resp = await request.get('/scores/me');
    expect(resp.ok()).toBeTruthy();

    const standings = (await resp.json()) as {
      groupMatchPoints: number;
      totalPoints: number;
    };

    // No scoring recompute is triggered for InProgress — Alice has 0 points.
    expect(standings.groupMatchPoints).toBe(0);
    expect(standings.totalPoints).toBe(0);
  });

  // ── AC3: Points update after full-time ──────────────────────────────────────

  test('standings update after match reaches full-time', async ({ request }) => {
    // Complete the match with the same 1-0 result Alice predicted.
    // injectResult sets status to Completed and triggers a full scoring recompute.
    await injectResult(request, '1', 1, 0);

    // Alice predicted the exact score (1-0) → 10 points (exact score tier).
    await apiLogin(request, 'alice');
    const resp = await request.get('/scores/me');
    expect(resp.ok()).toBeTruthy();

    const standings = (await resp.json()) as {
      groupMatchPoints: number;
      totalPoints: number;
    };

    expect(standings.groupMatchPoints).toBe(10);
    expect(standings.totalPoints).toBe(10);
  });
});
