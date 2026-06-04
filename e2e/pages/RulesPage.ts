import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Rules & Scoring explainer page.
 */
export class RulesPage {
  readonly page: Page;

  /** Page heading. */
  readonly heading: Locator;

  constructor(page: Page) {
    this.page    = page;
    this.heading = page.getByRole('heading', { name: 'Rules & Scoring' });
  }

  /** Wait until the page heading is visible. */
  async waitForLoad(): Promise<void> {
    await this.heading.waitFor({ state: 'visible', timeout: 10_000 });
  }

  /** The "Tournament Format" section heading. */
  tournamentFormatSection(): Locator {
    return this.page.getByRole('heading', { name: 'Tournament Format' });
  }
}
