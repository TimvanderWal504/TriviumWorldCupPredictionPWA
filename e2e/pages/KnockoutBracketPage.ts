import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Knockout Bracket screen.
 *
 * NOTE: HomeTeamId/AwayTeamId for bracket slots are null until the TWC-32
 * resolver runs. The page object covers the bracket display and prediction
 * forms but does not exercise slot population — that is TWC-31 territory.
 */
export class KnockoutBracketPage {
  readonly page: Page;

  /** Page heading. */
  readonly heading: Locator;

  /** Round selector tabs (R32, R16, QF, SF, Final). */
  readonly roundTabs: Locator;

  constructor(page: Page) {
    this.page      = page;
    this.heading   = page.getByRole('heading', { name: /Knockout Bracket|Bracket/ });
    this.roundTabs = page.locator('[role="tablist"]');
  }

  /** Wait for the bracket page to load. */
  async waitForLoad(): Promise<void> {
    await this.page.waitForFunction(
      () =>
        !document.body.innerText.includes('Loading…') &&
        (document.body.innerText.includes('Bracket') ||
          document.body.innerText.includes('R32') ||
          document.body.innerText.includes('Round of 32')),
      { timeout: 15_000 },
    );
  }

  /** Bracket slot cards currently visible. */
  slotCards(): Locator {
    return this.page.locator('.rounded-xl').filter({ hasText: /TBD|vs/ });
  }
}
