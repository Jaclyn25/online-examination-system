import { defineConfig } from '@playwright/test'
import { existsSync, readFileSync } from 'node:fs'
if (existsSync('../.env')) {
  const value = readFileSync('../.env', 'utf8')
    .match(/^DEMO_INSTRUCTOR_PASSWORD=(.+)$/m)?.[1]
    .trim()
  if (value && !process.env.DEMO_INSTRUCTOR_PASSWORD) process.env.DEMO_INSTRUCTOR_PASSWORD = value
}
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60000,
  expect: { timeout: 10000 },
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
    ['json', { outputFile: 'test-results/playwright.json' }],
  ],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5175',
    channel: process.env.PLAYWRIGHT_CHANNEL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    viewport: { width: 1440, height: 1000 },
  },
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: 'npm run dev',
        url: 'http://127.0.0.1:5175',
        reuseExistingServer: !process.env.CI,
        timeout: 60000,
      },
})
