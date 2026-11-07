import { afterEach, expect, it, vi } from 'vitest'
import { api, ApiError, json } from './api'
afterEach(() => vi.unstubAllGlobals())
it('adds a fresh antiforgery token to writes and keeps credentials same-origin', async () => {
  const fetcher = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ token: 'fictional-csrf' }))).mockResolvedValueOnce(new Response(null, { status: 204 }))
  vi.stubGlobal('fetch', fetcher)
  await api('/auth/logout', json('POST'))
  expect(fetcher.mock.calls[0][0]).toBe('/api/auth/csrf')
  const request = fetcher.mock.calls[1][1] as RequestInit
  expect(new Headers(request.headers).get('X-CSRF-TOKEN')).toBe('fictional-csrf')
  expect(request.credentials).toBe('same-origin')
})
it('surfaces validation errors from the server', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ errors: { title: ['Title is required.'] } }), { status: 400 })))
  await expect(api('/instructor/exams')).rejects.toThrow('Title is required.')
})
it('preserves unauthorized status for session recovery', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })))
  await expect(api('/auth/me')).rejects.toEqual(new ApiError('Sign in to continue.', 401))
})
