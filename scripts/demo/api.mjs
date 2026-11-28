import { setTimeout as delay } from 'node:timers/promises'

const EMAIL_EXISTS = 'BLException_AuthService_EmailAlreadyExists'

const INVALID_CREDENTIALS = 'BLException_AuthService_InvalidCredentials'

const TOO_MANY_ATTEMPTS = 'BLException_AuthService_TooManyAttempts'

const MAXIMUM_RETRIES = 3

const MAXIMUM_WAIT_SECONDS = 90

export class ApiError extends Error {
  constructor(method, path, status, body, retryAfter = null) {
    super(`${method} ${path} answered ${status} ${JSON.stringify(body)}`)
    this.code = body?.code
    this.retryAfterSeconds = retryAfter === null ? Number.NaN : Number(retryAfter)
  }
}

export function createClient(baseUrl, { request = fetch, wait = delay, log = console.log } = {}) {
  let token
  const sendOnce = async (method, path, body) => {
    const response = await request(`${baseUrl}${path}`, {
      method,
      headers: {
        'Accept-Language': 'en',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    }).catch((error) => {
      throw new Error(`Could not reach the API at ${baseUrl}. Is the stack running? (${error.cause?.message ?? error.message})`)
    })
    const text = await response.text()
    let data = null
    try {
      data = text ? JSON.parse(text) : null
    } catch {
      data = text
    }
    if (!response.ok) {
      throw new ApiError(method, path, response.status, data, response.headers.get('Retry-After'))
    }
    return data
  }
  const send = async (method, path, body) => {
    for (let retries = 0; ; retries++) {
      try {
        return await sendOnce(method, path, body)
      } catch (error) {
        const seconds = error.retryAfterSeconds
        if (error.code !== TOO_MANY_ATTEMPTS || retries === MAXIMUM_RETRIES || !(seconds <= MAXIMUM_WAIT_SECONDS)) {
          throw error
        }
        log(`The API is rate limiting. Trying again in ${seconds + 1} seconds.`)
        await wait((seconds + 1) * 1000)
      }
    }
  }
  return {
    send,
    login: async (email, password) => {
      token = (await send('POST', '/api/auth/login', { email, password })).accessToken
    },
  }
}

async function deleteAccount(client, { email, password }) {
  try {
    await client.login(email, password)
  } catch (error) {
    if (error.code === INVALID_CREDENTIALS) {
      return
    }
    throw error
  }
  await client.send('DELETE', '/api/users/me', { password })
}

export async function createAccount(client, { email, password, reset }) {
  if (reset) {
    await deleteAccount(client, { email, password })
  }
  try {
    await client.send('POST', '/api/auth/register', { email, password, firstName: 'Demo', lastName: 'Lifter', weightUnit: 0 })
  } catch (error) {
    if (error.code !== EMAIL_EXISTS) {
      throw error
    }
    throw new Error(
      reset
        ? `${email} exists with another password, so it cannot be reset. Pass its password with --password, or use another --email.`
        : `${email} already exists. Run again with --reset to delete it with all its data and seed it again.`,
    )
  }
  await client.login(email, password)
}
