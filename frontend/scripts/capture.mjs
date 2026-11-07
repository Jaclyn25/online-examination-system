import { chromium, expect } from '@playwright/test'
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs'
import { randomBytes, randomUUID } from 'node:crypto'
import { fileURLToPath } from 'node:url'
const base = process.env.SCREENSHOT_BASE_URL ?? 'http://127.0.0.1:18082'
const password =
  process.env.DEMO_INSTRUCTOR_PASSWORD ??
  readFileSync(new URL('../../.env', import.meta.url), 'utf8')
    .match(/^DEMO_INSTRUCTOR_PASSWORD=(.+)$/m)?.[1]
    .trim()
if (!password) throw new Error('Configure the local demo instructor before capturing screenshots.')
const output = fileURLToPath(new URL('../../docs/screenshots/', import.meta.url))
mkdirSync(output, { recursive: true })
const browser = await chromium.launch({ channel: process.env.PLAYWRIGHT_CHANNEL, headless: true })
const captures = []
async function shot(page, name) {
  await page.mouse.move(0, 0)
  await page.screenshot({ path: output + name + '.png', fullPage: false, animations: 'disabled' })
  captures.push({
    file: name + '.png',
    capturedAt: new Date().toISOString(),
    viewport: page.viewportSize(),
  })
}
try {
  const teacherContext = await browser.newContext({ viewport: { width: 1440, height: 1000 } })
  const teacher = await teacherContext.newPage()
  await teacher.goto(base + '/login')
  await teacher.getByRole('heading', { name: 'Welcome back' }).waitFor()
  await shot(teacher, 'login')
  await teacher.getByLabel('Email address').fill('instructor@aperture.test')
  await teacher.getByLabel('Password', { exact: true }).fill(password)
  await teacher.getByRole('button', { name: 'Sign in', exact: true }).click()
  await teacher.getByRole('heading', { name: /A fresh perspective/ }).waitFor()
  await teacher.setViewportSize({ width: 1440, height: 1200 })

  const learnerContext = await browser.newContext({ viewport: { width: 1440, height: 1050 } })
  const learner = await learnerContext.newPage()
  await learner.goto(base + '/register')
  await learner.getByLabel('Full name').fill('Avery Brooks')
  await learner.getByLabel('Email address').fill(`capture-${randomUUID()}@aperture.test`)
  await learner
    .getByLabel('Password', { exact: true })
    .fill(randomBytes(24).toString('hex').concat('Aa1!'))
  await learner.getByRole('button', { name: 'Create student account' }).click()
  await learner.getByRole('heading', { name: /Keep moving forward/ }).waitFor()
  const exam = learner.locator('article').filter({
    has: learner.getByRole('heading', { name: 'Web Platform Foundations', exact: true }),
  })
  await exam.getByRole('button', { name: 'View instructions' }).click()
  await exam.getByRole('button', { name: 'Begin timed attempt' }).click()
  await learner.getByRole('heading', { name: 'Your question map' }).waitFor()
  await learner.getByRole('radio').first().click()
  await expect(learner.getByRole('radio').first()).toBeChecked()
  await shot(learner, 'student-exam-player')
  for (let i = 1; i <= 4; i++) {
    await learner.getByRole('button', { name: 'Next question', exact: true }).click()
    const option = learner.getByRole('radio').nth(i === 4 ? 1 : 0)
    await option.click()
    await expect(option).toBeChecked()
  }
  await learner.getByRole('button', { name: 'Next question', exact: true }).click()
  await learner.getByRole('button', { name: 'Review & submit', exact: true }).first().click()
  await learner.getByRole('button', { name: 'Confirm final submission' }).click()
  await expect(learner.getByText('66.67%', { exact: true })).toBeVisible()
  await shot(learner, 'results')
  await learner.setViewportSize({ width: 390, height: 844 })
  await learner.goto(base + '/exams')
  await learner.getByRole('heading', { name: 'Find your next assessment.' }).waitFor()
  await shot(learner, 'mobile')
  await learner.getByRole('button', { name: 'Toggle navigation' }).click()
  await shot(learner, 'mobile-navigation')

  await teacher.goto(base)
  await teacher.getByRole('heading', { name: 'Examination activity' }).waitFor()
  await shot(teacher, 'instructor-dashboard')
  await teacher.getByRole('link', { name: 'Examinations', exact: true }).click()
  await teacher.getByRole('link', { name: 'Secure Systems Practice', exact: true }).waitFor()
  await shot(teacher, 'exam-management')
  await teacher.getByRole('link', { name: 'Secure Systems Practice', exact: true }).click()
  await teacher.getByRole('button', { name: 'Add question', exact: true }).click()
  await teacher
    .getByLabel('Question text')
    .fill('Which approach protects a browser session from script access?')
  await teacher.getByLabel('Choice 1', { exact: true }).fill('Use an HttpOnly session cookie')
  await teacher
    .getByLabel('Choice 2', { exact: true })
    .fill('Place the session token in a public URL')
  await teacher.getByRole('radio', { name: 'Choice 1 is correct' }).click()
  await teacher
    .locator('.question-editor')
    .evaluate((element) => element.scrollIntoView({ block: 'start' }))
  await shot(teacher, 'question-editor')
  await teacher.goto(base + '/reports')
  await teacher.setViewportSize({ width: 1440, height: 1400 })
  await teacher.getByRole('heading', { name: 'Score distribution' }).waitFor()
  await shot(teacher, 'reports')
  writeFileSync(
    output + 'capture-record.json',
    JSON.stringify(
      {
        base,
        product: 'Aperture Exams',
        source:
          'Running production Docker application with fictional seeded data and a real student attempt',
        captures,
      },
      null,
      2,
    ),
  )
  console.log(`Captured and verified ${captures.length} application screenshots.`)
} finally {
  await browser.close()
}
