import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for My Standings.
 *
 * Shows rank, total points, a breakdown by category, and the Golden Six table.
 */
export class StandingsPage {
  readonly page: Page;

  /** Page heading. */
  readonly heading: Locator;

  constructor(page: Page) {
    this.page    = page;
    this.heading = page.getByRole('heading', { name: 'My Standings' });
  }

  /** Wait until the page is done loading. */
  async waitForLoad(): Promise<void> {
    await this.heading.waitFor({ state: 'visible', timeout: 15_000 });
    await this.page.waitForFunction(
      () => !document.body.innerText.includes('Loading standings…'),
      { timeout: 15_000 },
    );
  }

  /** The "Current rank" display — returns text content. */
  rankDisplay(): Locator {
    return this.page.locator('text=Current rank').locator('..');
  }

  /** The "Total points" display. */
  totalPointsDisplay(): Locator {
    return this.page.locator('text=Total points').locator('..');
  }
}
