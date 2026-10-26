const unauthorizedListeners = new Set()

export class ApiError extends Error {
  constructor(message, status) {
    super(message)
    this.status = status
  }
}

export function onUnauthorized(listener) {
  unauthorizedListeners.add(listener)
  return () => unauthorizedListeners.delete(listener)
}

export async function request(method, path, body, { silent = false } = {}) {
  let response
  try {
    response = await fetch(`/api${path}`, {
      method,
      credentials: 'same-origin',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError('ارتباط با سرور برقرار نشد.', 0)
  }

  const payload = await response.json().catch(() => null)

  if (!response.ok) {
    if (response.status === 401 && !silent) {
      for (const listener of unauthorizedListeners) listener()
    }
    throw new ApiError(payload?.error || 'سرور درخواست را قبول نکرد.', response.status)
  }
  return payload
}
