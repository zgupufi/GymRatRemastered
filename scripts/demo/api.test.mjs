import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createAccount, createClient } from './api.mjs'

const answer = (status, body, headers) => new Response(body ? JSON.stringify(body) : null, { status, headers })
const refused = (code, headers) => answer(400, { code }, headers)
const limited = (seconds) => refused('BLException_AuthService_TooManyAttempts', seconds === undefined ? {} : { 'Retry-After': String(seconds) })
const loggedIn = () => answer(200, { accessToken: 'token' })
const created = () => answer(201)

function setup(...responses) {
  const calls = []
  const waits = []
  const client = createClient('http://localhost:8080', {
    request: async (url, { method }) => {
      calls.push(`${method} ${new URL(url).pathname}`)
      return responses.shift()
    },
    wait: async (milliseconds) => waits.push(milliseconds),
    log: () => {},
  })
  return { client, calls, waits }
}

const account = { email: 'demo@gymrat.test', password: 'GymRat-Demo-1', reset: false }

test('a rate limited call waits for Retry-After and is sent again', async () => {
  const { client, calls, waits } = setup(limited(42), created())

  await client.send('POST', '/api/auth/register', {})

  assert.deepEqual(calls, ['POST /api/auth/register', 'POST /api/auth/register'])
  assert.deepEqual(waits, [43000])
})

test('it gives up after three retries', async () => {
  const { client, calls } = setup(limited(1), limited(1), limited(1), limited(1))

  await assert.rejects(client.send('POST', '/api/auth/register', {}), /TooManyAttempts/)
  assert.equal(calls.length, 4)
})

test('a long or missing Retry-After and other errors are not retried', async () => {
  for (const response of [limited(900), limited(), refused('Other', { 'Retry-After': '1' })]) {
    const { client, waits } = setup(response)

    await assert.rejects(client.send('POST', '/api/auth/login', {}), /answered 400/)
    assert.deepEqual(waits, [])
  }
})

test('a rate limit on the register after a reset still recreates the account', async () => {
  const { client, calls } = setup(loggedIn(), answer(204), limited(5), created(), loggedIn())

  await createAccount(client, { ...account, reset: true })

  assert.deepEqual(calls, [
    'POST /api/auth/login',
    'DELETE /api/users/me',
    'POST /api/auth/register',
    'POST /api/auth/register',
    'POST /api/auth/login',
  ])
})

test('a reset without an existing account only registers it', async () => {
  const { client, calls } = setup(refused('BLException_AuthService_InvalidCredentials'), created(), loggedIn())

  await createAccount(client, { ...account, reset: true })

  assert.deepEqual(calls, ['POST /api/auth/login', 'POST /api/auth/register', 'POST /api/auth/login'])
})

test('a reset stops when the login fails for another reason', async () => {
  const { client, calls } = setup(limited(900))

  await assert.rejects(createAccount(client, { ...account, reset: true }), /TooManyAttempts/)
  assert.deepEqual(calls, ['POST /api/auth/login'])
})

test('an existing account is explained and never deleted without --reset', async () => {
  const exists = () => refused('BLException_AuthService_EmailAlreadyExists')
  const plain = setup(exists())
  const wrongPassword = setup(refused('BLException_AuthService_InvalidCredentials'), exists())

  await assert.rejects(createAccount(plain.client, account), /Run again with --reset/)
  await assert.rejects(createAccount(wrongPassword.client, { ...account, reset: true }), /another password/)
  assert.deepEqual(plain.calls, ['POST /api/auth/register'])
})
