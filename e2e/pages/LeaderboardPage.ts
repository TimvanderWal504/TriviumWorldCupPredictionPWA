import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Leaderboard.
 *
 * Displays ranked entries. Authenticated users can click rows to see drill-down.
 */
export class LeaderboardPage {
  readonly page: Page;

  /** Page heading. */
  readonly heading: Locator;

  constructor(page: Page) {
    this.page    = page;
    this.heading = page.getByRole('heading', { name: 'Leaderboard' });
  }

  /** Wait until the page is done loading. */
  async waitForLoad(): Promise<void> {
    await this.page.waitForFunction(
      () =>
        !document.body.innerText.includes('Loading leaderboard…') &&
        (document.querySelector('h1') !== null ||
          document.body.innerText.includes('No scores yet')),
      { timeout: 15_000 },
    );
  }

  /**
   * Returns the leaderboard row buttons.
   * Rows are <button> elements inside the entries container.
   */
  rows(): Locator {
    return this.page.locator('.bg-slate-800.rounded-xl button');
  }

  /** The "no scores yet" empty-state message. */
  emptyState(): Locator {
    return this.page.locator('text=No scores yet');
  }

  /** Click the row for a specific display name. */
  async clickRowForUser(displayName: string): Promise<void> {
    await this.rows()
      .filter({ hasText: displayName })
      .click();
  }

  /** Wait for the drill-down panel to appear. */
  async waitForDrillDown(): Promise<void> {
    await this.page.getByText('Back to leaderboard').waitFor({ state: 'visible', timeout: 10_000 });
  }

  /** Navigate back from the drill-down panel. */
  async closeDrillDown(): Promise<void> {
    await this.page.getByRole('button', { name: 'Back to leaderboard' }).click();
  }
}
