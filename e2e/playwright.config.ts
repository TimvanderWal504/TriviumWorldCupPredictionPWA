import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for Trivium World Cup 2026 E2E tests.
 *
 * Target: local-only (no CI). The stack must be running before executing:
 *   docker compose up -d    (or dotnet run + npm run dev for dev mode)
 *
 * Base URL: React dev server proxies /auth, /fixtures, etc. to the API.
 * When running against Docker Compose the web container listens on :80.
 *
 * Default base URL is http://localhost:5173 (Vite dev server).
 * Override with BASE_URL env var: BASE_URL=http://localhost:80 npx playwright test
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false, // DB state is shared — run serially by default
  forbidOnly: false,
  retries: 0,
  workers: 1,

  // Artifacts on failure
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: '../playwright-report' }],
  ],

  use: {
    baseURL: process.env['BASE_URL'] ?? 'http://localhost:5173',
    // Credentials (cookies) must follow redirects
    extraHTTPHeaders: {
      Accept: 'application/json',
    },
    // Capture traces, screenshots, and video on failure
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
