import { type Page, type Locator } from '@playwright/test';

/**
 * Base page object covering the application shell (nav, auth state).
 * All feature page objects extend or compose this.
 */
export class AppPage {
  readonly page: Page;

  /** The top navigation bar — only visible when authenticated with a profile. */
  readonly nav: Locator;

  /** The sign-in prompt shown to unauthenticated visitors. */
  readonly signinPrompt: Locator;

  constructor(page: Page) {
    this.page    = page;
    this.nav     = page.getByTestId('app-nav');
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

  /** Click a named nav item by its visible text. */
  async navTo(label: string): Promise<void> {
    await this.nav.getByRole('button', { name: label }).click();
  }

  /** Navigate to the Predictions page via the nav bar. */
  async goToPredictions(): Promise<void> {
    await this.navTo('Predictions');
  }

  /** Navigate to the Tournament page via the nav bar. */
  async goToTournament(): Promise<void> {
    await this.navTo('Tournament');
  }

  /** Navigate to the Knockout Bracket page via the nav bar. */
  async goToKnockout(): Promise<void> {
    await this.navTo('Bracket');
  }

  /** Navigate to My Standings via the nav bar. */
  async goToStandings(): Promise<void> {
    await this.navTo('My Standings');
  }

  /** Navigate to Leaderboard via the nav bar. */
  async goToLeaderboard(): Promise<void> {
    await this.navTo('Leaderboard');
  }

  /** Navigate to Live scores via the nav bar. */
  async goToLive(): Promise<void> {
    await this.navTo('Live');
  }

  /** Navigate to Rules via the nav bar. */
  async goToRules(): Promise<void> {
    await this.navTo('Rules');
  }

  /** Navigate to Admin via the nav bar (only visible for admin users). */
  async goToAdmin(): Promise<void> {
    await this.navTo('Admin');
  }

  /** Sign out using the nav bar Sign out button. */
  async signOut(): Promise<void> {
    await this.nav.getByRole('button', { name: 'Sign out' }).click();
    // Wait for the sign-in prompt to appear
    await this.signinPrompt.waitFor({ state: 'visible', timeout: 10_000 });
  }
}
