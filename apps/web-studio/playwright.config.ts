// SPDX-License-Identifier: Apache-2.0
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: 'list',
  timeout: 20_000,
  use: {
    baseURL: 'http://127.0.0.1:5174',
    headless: true,
    browserName: 'chromium',
    viewport: { width: 1330, height: 860 },
    launchOptions: process.env.ARCHPULSE_CHROMIUM_PATH
      ? { executablePath: process.env.ARCHPULSE_CHROMIUM_PATH,
          args: ['--no-sandbox'] }
      : undefined
  }
});
