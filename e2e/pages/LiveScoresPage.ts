import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Live Scores screen.
 *
 * Shows fixtures in the live window (InProgress, recently completed,
 * or kicking off within 30 minutes). Polls every 20 seconds while active.
 */
export class LiveScoresPage {
  readonly page: Page;

  /** Page heading rendered in the app shell. */
  readonly heading: Locator;

  /** Empty-state message shown when no live fixtures and liveWindowActive=false. */
  readonly emptyState: Locator;

  /** "Live updates every 20s" indicator — visible only when liveWindowActive=true. */
  readonly liveIndicator: Locator;

  constructor(page: Page) {
    this.page          = page;
    this.heading       = page.getByRole('heading', { name: 'Live Scores' });
    this.emptyState    = page.locator('text=No matches currently live');
    this.liveIndicator = page.locator('text=Live updates every 20s');
  }

  /** Wait until the initial fetch completes (loading spinner is gone). */
  async waitForLoad(): Promise<void> {
    await this.page.waitForFunction(
      () => !document.body.innerText.includes('Loading live scores'),
      { timeout: 15_000 },
    );
  }

  /**
   * The LIVE status badge element — present when at least one fixture is InProgress.
   * Uses .first() because each live card renders its own badge.
   */
  liveBadge(): Locator {
    return this.page.getByText('LIVE').first();
  }

  /**
   * The fixture card that contains the LIVE badge.
   * Suitable for scoped assertions (score, goal entries) within a single live match.
   */
  liveFixtureCard(): Locator {
    return this.page.locator('.rounded-card').filter({ hasText: 'LIVE' }).first();
  }

  /** All visible fixture cards (InProgress, Completed, or Scheduled within the live window). */
  fixtureCards(): Locator {
    return this.page.locator('.rounded-card');
  }
}
