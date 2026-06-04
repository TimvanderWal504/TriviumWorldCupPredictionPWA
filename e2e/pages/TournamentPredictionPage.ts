import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Tournament Prediction screen.
 *
 * Allows the user to pick a Champion team and up to 6 Golden Six players
 * (one per position: GK, DEF×2, MID×2, FWD).
 */
export class TournamentPredictionPage {
  readonly page: Page;

  /** Page heading. */
  readonly heading: Locator;

  /** Champion team selector (a <select> element). */
  readonly championSelect: Locator;

  /** Save button. */
  readonly saveButton: Locator;

  constructor(page: Page) {
    this.page           = page;
    this.heading        = page.getByRole('heading', { name: 'Tournament Prediction' });
    this.championSelect = page.getByRole('combobox').first();
    this.saveButton     = page.getByRole('button', { name: /Save|Update/ });
  }

  /** Wait until the page content is loaded. */
  async waitForLoad(): Promise<void> {
    await this.heading.waitFor({ state: 'visible', timeout: 15_000 });
    await this.page.waitForFunction(
      () => !document.body.innerText.includes('Loading…'),
      { timeout: 15_000 },
    );
  }

  /** Returns the locked banner if predictions are locked. */
  lockedBanner(): Locator {
    return this.page.locator('text=Tournament predictions are locked');
  }

  /** True if predictions are currently locked. */
  async isLocked(): Promise<boolean> {
    return this.lockedBanner().isVisible();
  }
}
