import { type Page, type Locator } from '@playwright/test';

/**
 * Base page object covering the application shell (nav, auth state).
 * All feature page objects extend or compose this.
 */
export class AppPage {
  readonly page: Page;

  /** The bottom navigation bar — only visible when authenticated with a profile. */
  readonly nav: Locator;

  /** The sign-in prompt shown to unauthenticated visitors. */
  readonly signinPrompt: Locator;

  constructor(page: Page) {
    this.page         = page;
    this.nav          = page.getByTestId('app-nav');
    this.signinPrompt = page.getByTestId('signin-prompt');
  }

  /** Navigate to / and wait for the page to be done loading. */
  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.page.waitForLoadState('networkidle');
  }

  /** True when the nav bar is visible (authenticated + profile exists). */
  async isAuthenticated(): Promise<boolean> {
    return this.nav.isVisible();
  }

  /** Click a named bottom-nav tab button by its visible label. */
  async navTo(label: string): Promise<void> {
    await this.nav.getByRole('button', { name: label }).click();
  }

  /** Navigate to the Group Predictions page via the bottom nav. */
  async goToPredictions(): Promise<void> {
    await this.navTo('Predict');
  }

  /**
   * Navigate to the Tournament Prediction sub-page.
   * Tournament is a sub-pill inside the Predict tab — click Predict first,
   * then click the Tournament pill.
   */
  async goToTournament(): Promise<void> {
    await this.navTo('Predict');
    await this.page.getByRole('button', { name: 'Tournament' }).click();
  }

  /** Navigate to the Knockout Bracket tab (only visible once bracket is open). */
  async goToKnockout(): Promise<void> {
    await this.navTo('Bracket');
  }

  /**
   * Navigate to My Standings.
   * The Me tab shows Standings by default.
   */
  async goToStandings(): Promise<void> {
    await this.navTo('Me');
  }

  /** Navigate to the Leaderboard (Ranks tab). */
  async goToLeaderboard(): Promise<void> {
    await this.navTo('Ranks');
  }

  /**
   * Navigate to Live Scores.
   * The Live tab only appears when liveWindowActive=true — waits for it to become
   * visible before clicking, so tests that set a fixture to InProgress beforehand
   * do not need an explicit wait.
   */
  async goToLive(): Promise<void> {
    const liveBtn = this.nav.getByRole('button', { name: 'Live' });
    await liveBtn.waitFor({ state: 'visible', timeout: 10_000 });
    await liveBtn.click();
  }

  /** Navigate to the Rules page (top-level tab). */
  async goToRules(): Promise<void> {
    await this.navTo('Rules');
  }

  /**
   * Navigate to the Admin page (admin users only).
   * Admin is a sub-pill inside the Me tab — click Me first, then Admin.
   */
  async goToAdmin(): Promise<void> {
    await this.navTo('Me');
    await this.page.getByRole('button', { name: 'Admin' }).click();
  }

  /** Sign out using the Sign out button inside the Me tab. */
  async signOut(): Promise<void> {
    await this.navTo('Me');
    await this.page.getByRole('button', { name: 'Sign out' }).click();
    await this.signinPrompt.waitFor({ state: 'visible', timeout: 10_000 });
  }
}
