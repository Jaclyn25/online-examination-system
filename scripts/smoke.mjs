import assert from 'node:assert/strict'
import { randomBytes, randomUUID } from 'node:crypto'
import { execFileSync } from 'node:child_process'
const base = process.env.SMOKE_BASE_URL ?? 'http://127.0.0.1:18082'
const jar = new Map()
async function request(path, method = 'GET', body) {
  const token = method === 'GET' ? null : (await request('/api/auth/csrf')).token
  const headers = { Cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') }
  if (token) headers['X-CSRF-TOKEN'] = token
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const response = await fetch(base + path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  for (const cookie of response.headers.getSetCookie()) {
    const pair = cookie.split(';')[0]
    const i = pair.indexOf('=')
    jar.set(pair.slice(0, i), pair.slice(i + 1))
  }
  if (!response.ok) throw new Error(`${method} ${path}: HTTP ${response.status}`)
  return response.status === 204 ? null : response.json()
}
assert.match(await (await fetch(base)).text(), /Aperture Exams/)
assert.equal((await request('/api/health')).status, 'healthy')
assert.match((await request('/openapi/v1.json')).openapi, /^3\./)
assert.equal((await fetch(base + '/api/instructor/reports')).status, 401)
const identity = {
  email: `smoke-${randomUUID()}@aperture.test`,
  password: randomBytes(24).toString('hex').concat('Aa1!'),
  displayName: 'Fictional Container QA',
}
await request('/api/auth/register', 'POST', identity)
await request('/api/auth/login', 'POST', identity)
assert.equal((await request('/api/auth/me')).role, 'Student')
assert.equal(
  (
    await fetch(base + '/api/instructor/reports', {
      headers: { Cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') },
    })
  ).status,
  403,
)
const exams = await request('/api/student/exams')
assert.ok(exams.length > 0)
const attempt = await request(`/api/student/exams/${exams[0].examId}/attempts`, 'POST')
assert.ok(attempt.questions.length > 0)
assert.equal(JSON.stringify(attempt).includes('isCorrect'), false)
const result = await request(`/api/student/attempts/${attempt.id}/submit`, 'POST')
assert.equal(result.earnedMarks, 0)
assert.equal(result.unansweredCount, attempt.questions.length)
const duplicate = await request(`/api/student/attempts/${attempt.id}/submit`, 'POST')
assert.deepEqual(duplicate, result)
if (process.env.SMOKE_RESTART_CHECK === 'true') {
  execFileSync('docker', ['compose', 'restart', 'api'], { stdio: 'pipe' })
  let ready = false
  for (let i = 0; i < 60; i++) {
    try {
      if ((await fetch(base + '/api/health')).ok) {
        ready = true
        break
      }
    } catch {
      /* API is restarting. */
    }
    await new Promise((resolve) => setTimeout(resolve, 500))
  }
  assert.ok(ready, 'API should become ready after restart')
  assert.equal((await request('/api/auth/me')).role, 'Student')
}
await request('/api/auth/logout', 'POST')
console.log(
  JSON.stringify(
    {
      status: 'passed',
      base,
      product: 'Aperture Exams',
      restartSessionVerified: process.env.SMOKE_RESTART_CHECK === 'true',
      checks: [
        'web',
        'PostgreSQL health',
        'OpenAPI',
        'anonymous denial',
        'registration',
        'login',
        'role denial',
        'protected question payload',
        'server scoring',
        'duplicate submission',
        'logout',
      ],
      verifiedAt: new Date().toISOString(),
    },
    null,
    2,
  ),
)
