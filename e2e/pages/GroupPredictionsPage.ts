import { type Page, type Locator } from '@playwright/test';

/**
 * Page object for the Group Predictions screen.
 *
 * The page renders group-stage fixtures in group tabs (A–L). Each fixture
 * has score inputs (home / away) and a Save/Update button when unlocked.
 */
export class GroupPredictionsPage {
  readonly page: Page;

  /** Page heading. */
  readonly heading: Locator;

  /** Group tab list. */
  readonly groupTabs: Locator;

  constructor(page: Page) {
    this.page      = page;
    this.heading   = page.getByRole('heading', { name: 'Group Stage Predictions' });
    this.groupTabs = page.getByRole('tablist');
  }

  /** Wait until the page has finished loading fixtures. */
  async waitForLoad(): Promise<void> {
    await this.heading.waitFor({ state: 'visible', timeout: 15_000 });
    // Wait for either a fixture card or the loading spinner to disappear
    await this.page.waitForFunction(
      () => !document.body.innerText.includes('Loading fixtures…'),
      { timeout: 15_000 },
    );
  }

  /** Select a group tab by letter (e.g. 'A'). */
  async selectGroup(letter: string): Promise<void> {
    await this.groupTabs.getByRole('tab', { name: `Group ${letter}` }).click();
  }

  /**
   * Returns the fixture cards currently visible (active group).
   * Each card has a form with two number inputs and a save button.
   */
  fixtureCards(): Locator {
    return this.page.locator('.rounded-xl.border.p-4');
  }

  /**
   * Fill in a prediction for the first unlocked fixture in the active group.
   * Returns false if no unlocked fixture found.
   */
  async fillFirstUnlockedPrediction(home: number, away: number): Promise<boolean> {
    const cards = this.fixtureCards();
    const count = await cards.count();
    for (let i = 0; i < count; i++) {
      const card = cards.nth(i);
      // Unlocked cards have a Save / Update button
      const saveBtn = card.getByRole('button', { name: /Save|Update/ });
      if (await saveBtn.isVisible()) {
        const homeInput = card.getByRole('spinbutton').first();
        const awayInput = card.getByRole('spinbutton').last();
        await homeInput.fill(String(home));
        await awayInput.fill(String(away));
        await saveBtn.click();
        return true;
      }
    }
    return false;
  }

  /** Returns true if the 'Locked' badge is visible on at least one card. */
  async hasLockedFixture(): Promise<boolean> {
    return this.page.locator('text=Locked').isVisible();
  }
}
