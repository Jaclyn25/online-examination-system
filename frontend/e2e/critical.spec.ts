import { test, expect } from '@playwright/test'
import type { APIRequestContext, Page } from '@playwright/test'
import { randomBytes, randomUUID } from 'node:crypto'
const generatedPassword = () => randomBytes(24).toString('hex').concat('Aa1!')
async function write(context: APIRequestContext, path: string, method: string, data?: unknown) {
  const csrf = await context.get('/api/auth/csrf')
  expect(csrf.ok()).toBeTruthy()
  const { token } = (await csrf.json()) as { token: string }
  return context.fetch('/api' + path, { method, data, headers: { 'X-CSRF-TOKEN': token } })
}
async function instructor(context: APIRequestContext) {
  const password = process.env.DEMO_INSTRUCTOR_PASSWORD
  if (!password)
    throw new Error(
      'Set DEMO_INSTRUCTOR_PASSWORD or generate local .env using scripts/init-env.ps1.',
    )
  expect(
    (
      await write(context, '/auth/login', 'POST', { email: 'instructor@aperture.test', password })
    ).ok(),
  ).toBeTruthy()
}
async function student(context: APIRequestContext) {
  const credentials = {
    email: `e2e-${randomUUID()}@aperture.test`,
    password: generatedPassword(),
    displayName: 'Fictional Browser Student',
  }
  expect((await write(context, '/auth/register', 'POST', credentials)).status()).toBe(201)
  expect((await write(context, '/auth/login', 'POST', credentials)).status()).toBe(204)
  return credentials
}
async function assessment(context: APIRequestContext) {
  await instructor(context)
  const title = `Browser assessment ${randomUUID().slice(0, 8)}`
  const created = await write(context, '/instructor/exams', 'POST', {
    title,
    description: 'A fictional two-question browser assessment.',
    durationMinutes: 10,
    passingPercentage: 60,
  })
  expect(created.status()).toBe(201)
  const exam = (await created.json()) as { id: string }
  const questions = [
    {
      text: 'Which HTTP method retrieves data?',
      marks: 2,
      choices: [
        { text: 'GET', isCorrect: true },
        { text: 'POST', isCorrect: false },
      ],
      revision: 1,
    },
    {
      text: 'Which status means not found?',
      marks: 3,
      choices: [
        { text: '200', isCorrect: false },
        { text: '404', isCorrect: true },
      ],
      revision: 2,
    },
  ]
  for (const q of questions)
    expect(
      (await write(context, `/instructor/exams/${exam.id}/questions`, 'POST', q)).ok(),
    ).toBeTruthy()
  expect(
    (
      await write(context, `/instructor/exams/${exam.id}/publish`, 'POST', {
        revision: 3,
        showFeedback: true,
      })
    ).ok(),
  ).toBeTruthy()
  return { ...exam, title }
}
async function registerInBrowser(page: Page) {
  await page.goto('/register')
  await page.getByLabel('Full name').fill('Fictional Browser Student')
  await page.getByLabel('Email address').fill(`browser-${randomUUID()}@aperture.test`)
  await page.getByLabel('Password', { exact: true }).fill(generatedPassword())
  await page.getByRole('button', { name: 'Create student account' }).click()
  await expect(page.getByRole('heading', { name: /Keep moving forward/ })).toBeVisible()
}
test('instructor creates questions and publishes an exam through the UI', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Email address').fill('instructor@aperture.test')
  await page.getByLabel('Password', { exact: true }).fill(process.env.DEMO_INSTRUCTOR_PASSWORD!)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await page.getByRole('link', { name: 'Create examination', exact: true }).click()
  const title = `UI assessment ${randomUUID().slice(0, 8)}`
  await page.getByLabel('Exam title').fill(title)
  await page
    .getByLabel('Description & instructions')
    .fill('Read each question and choose one answer.')
  await page.getByRole('button', { name: 'Create exam draft' }).click()
  await page.getByRole('button', { name: 'Add question', exact: true }).click()
  await page.getByLabel('Question text').fill('Which HTTP method retrieves data?')
  await page.getByLabel('Choice 1', { exact: true }).fill('GET')
  await page.getByLabel('Choice 2', { exact: true }).fill('POST')
  await page.getByRole('radio', { name: 'Choice 1 is correct' }).click()
  await page.getByRole('button', { name: 'Save question', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Which HTTP method retrieves data?' }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Review & publish' }).click()
  await page.getByRole('button', { name: 'Confirm publish' }).click()
  await expect(page.getByRole('status')).toHaveText('Examination published.')
  await expect(page.getByLabel('Exam title')).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Return to draft' })).toBeVisible()
})
test('student registers, restores saved answers, submits and receives the correct result', async ({
  page,
  request,
}) => {
  const exam = await assessment(request)
  await registerInBrowser(page)
  const row = page
    .locator('article')
    .filter({ has: page.getByRole('heading', { name: exam.title, exact: true }) })
  await row.getByRole('button', { name: 'View instructions' }).click()
  await row.getByRole('button', { name: 'Begin timed attempt' }).click()
  await page.getByRole('radio', { name: /GET/ }).click()
  await expect(page.getByRole('radio', { name: /GET/ })).toBeChecked()
  const attemptUrl = page.url()
  await page.reload()
  await expect(page.getByRole('radio', { name: /GET/ })).toBeChecked()
  expect(page.url()).toBe(attemptUrl)
  await page.getByRole('button', { name: 'Next question', exact: true }).click()
  await page.getByRole('radio', { name: /404/ }).click()
  await expect(page.getByRole('radio', { name: /404/ })).toBeChecked()
  await page.getByRole('button', { name: 'Review & submit', exact: true }).first().click()
  await expect(page.getByRole('region', { name: 'Submission confirmation' })).toBeVisible()
  await page.getByRole('button', { name: 'Confirm final submission' }).click()
  await expect(page.getByText('100%', { exact: true })).toBeVisible()
  await expect(page.getByText('5 of 5 marks', { exact: true })).toBeVisible()
  await expect(page.getByText('Passed', { exact: true })).toBeVisible()
  await page.getByRole('link', { name: 'Back to my results' }).click()
  await expect(page.getByRole('cell', { name: new RegExp(exam.title) })).toBeVisible()
})
test('unauthorized access, answer-key protection and attempt isolation are enforced', async ({
  page,
  request,
  playwright,
  baseURL,
}) => {
  const exam = await assessment(request)
  const anonymous = await playwright.request.newContext({ baseURL })
  expect((await anonymous.get('/api/instructor/reports')).status()).toBe(401)
  await anonymous.dispose()
  await student(page.request)
  const start = await write(page.request, `/student/exams/${exam.id}/attempts`, 'POST')
  const attempt = (await start.json()) as { id: string; questions: { id: string }[] }
  expect(JSON.stringify(attempt)).not.toContain('isCorrect')
  expect(JSON.stringify(attempt)).not.toContain('correctChoiceId')
  expect((await page.request.get(`/api/student/attempts/${attempt.id}/result`)).status()).toBe(409)
  expect((await page.request.get(`/api/instructor/exams/${exam.id}/questions`)).status()).toBe(403)
  expect(
    (
      await write(page.request, '/instructor/exams', 'POST', {
        title: 'Forbidden',
        durationMinutes: 10,
        passingPercentage: 60,
        description: '',
      })
    ).status(),
  ).toBe(403)
  await page.goto('/exams/new')
  await expect(page.getByRole('alert')).toContainText('Your account cannot access this workspace.')
  const other = await playwright.request.newContext({ baseURL })
  await student(other)
  expect((await other.get(`/api/student/attempts/${attempt.id}`)).status()).toBe(404)
  expect((await write(other, `/student/attempts/${attempt.id}/submit`, 'POST')).status()).toBe(404)
  expect(
    (
      await write(
        other,
        `/student/attempts/${attempt.id}/answers/${attempt.questions[0].id}`,
        'PUT',
        { choiceId: null, revision: 1 },
      )
    ).status(),
  ).toBe(404)
  await other.dispose()
})
test('mobile navigation and focused exam player remain usable without horizontal overflow', async ({
  page,
  request,
}) => {
  const exam = await assessment(request)
  await page.setViewportSize({ width: 390, height: 844 })
  await student(page.request)
  await page.goto('/')
  await page.getByRole('button', { name: 'Toggle navigation' }).click()
  await expect(page.getByRole('navigation', { name: 'Main navigation' })).toBeVisible()
  await page.getByRole('link', { name: 'Available exams', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Find your next assessment.' })).toBeVisible()
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy()
  const attempt = (await (
    await write(page.request, `/student/exams/${exam.id}/attempts`, 'POST')
  ).json()) as { id: string }
  await page.goto(`/attempts/${attempt.id}`)
  await page.getByRole('radio', { name: /GET/ }).click()
  await expect(page.getByRole('radio', { name: /GET/ })).toBeChecked()
  await page.getByRole('button', { name: 'Question 2, unanswered' }).click()
  await expect(page.getByText('Which status means not found?', { exact: true })).toBeVisible()
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy()
})
