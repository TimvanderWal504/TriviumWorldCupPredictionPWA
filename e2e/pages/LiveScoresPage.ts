import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Live Scores screen.
 *
 * Shows fixtures in the live window (InProgress, recently completed,
 * or kicking off within 30 minutes). Polls every 20 seconds while active.
 */
export class LiveScoresPage {
  readonly page: Page;

  /** Page heading. */
  readonly heading: Locator;

  /** Empty-state message shown when no live fixtures. */
  readonly emptyState: Locator;

  constructor(page: Page) {
    this.page       = page;
    this.heading    = page.getByRole('heading', { name: 'Live Scores' });
    this.emptyState = page.locator('text=No matches currently live');
  }

  /** Wait until the live scores page is loaded. */
  async waitForLoad(): Promise<void> {
    await this.page.waitForFunction(
      () =>
        !document.body.innerText.includes('Loading…') &&
        (document.body.innerText.includes('Live Scores') ||
          document.body.innerText.includes('No live matches')),
      { timeout: 15_000 },
    );
  }

  /** Returns the live fixture cards. */
  fixtureCards(): Locator {
    return this.page.locator('.rounded-xl').filter({ hasText: /InProgress|Completed/ });
  }
}
