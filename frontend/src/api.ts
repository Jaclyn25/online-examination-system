export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message)
  }
}
export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const method = options.method ?? 'GET'
  const headers = new Headers(options.headers)
  if (options.body) headers.set('Content-Type', 'application/json')
  if (!['GET', 'HEAD'].includes(method)) {
    const tokenResponse = await fetch('/api/auth/csrf', { credentials: 'same-origin' })
    if (!tokenResponse.ok)
      throw new ApiError('Unable to verify your session. Refresh and retry.', tokenResponse.status)
    const { token } = (await tokenResponse.json()) as { token: string }
    headers.set('X-CSRF-TOKEN', token)
  }
  const response = await fetch('/api' + path, { ...options, headers, credentials: 'same-origin' })
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as {
      title?: string
      errors?: Record<string, string[]>
    }
    const message = problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.title
    throw new ApiError(
      message ??
        (response.status === 401
          ? 'Sign in to continue.'
          : response.status === 403
            ? 'Your account cannot access this workspace.'
            : 'The request failed. Please retry.'),
      response.status,
    )
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}
export const json = (method: string, body?: unknown): RequestInit => ({
  method,
  ...(body === undefined ? {} : { body: JSON.stringify(body) }),
})
