import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Admin screen.
 *
 * Only accessible to users with the 'admin' role (Diana in the seed data).
 * Covers ingestion status, manual result override, and recompute.
 */
export class AdminPage {
  readonly page: Page;

  /** "Access denied" message shown to non-admins. */
  readonly accessDenied: Locator;

  /** The "Trigger Recompute" button. */
  readonly recomputeButton: Locator;

  /** Fixture ID input in the manual result form. */
  readonly fixtureIdInput: Locator;

  /** Home score input in the manual result form. */
  readonly homeScoreInput: Locator;

  /** Away score input in the manual result form. */
  readonly awayScoreInput: Locator;

  /** Submit button for the manual result form. */
  readonly setResultButton: Locator;

  constructor(page: Page) {
    this.page            = page;
    this.accessDenied    = page.locator('text=Access denied');
    this.recomputeButton = page.getByRole('button', { name: /Recompute|Trigger/ });
    this.fixtureIdInput  = page.getByLabel(/Fixture ID/i).first();
    this.homeScoreInput  = page.getByLabel(/Home/i).first();
    this.awayScoreInput  = page.getByLabel(/Away/i).first();
    this.setResultButton = page.getByRole('button', { name: /Set Result/i });
  }

  /** Wait for the admin page to finish loading (either the access denied message or ingestion panel). */
  async waitForLoad(): Promise<void> {
    await this.page.waitForFunction(
      () =>
        document.body.innerText.includes('Access denied') ||
        document.body.innerText.includes('Ingestion') ||
        document.body.innerText.includes('Manual Result'),
      { timeout: 15_000 },
    );
  }

  /** Returns true when the access-denied message is shown. */
  async isAccessDenied(): Promise<boolean> {
    return this.accessDenied.isVisible();
  }
}
