import { expect, test, type APIRequestContext } from '@playwright/test'

const immutable = 'public, max-age=31536000, immutable'

async function assetPaths(request: APIRequestContext) {
  const html = await (await request.get('/')).text()
  const paths = [...html.matchAll(/(?:src|href)="(\/assets\/[^"]+)"/g)].map((match) => match[1])
  expect(paths.some((path) => path.endsWith('.js'))).toBe(true)
  expect(paths.some((path) => path.endsWith('.css'))).toBe(true)
  return paths
}

test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop', 'Response headers do not depend on the viewport')
})

test('hashed assets are compressed and cached for a year', async ({ request }) => {
  for (const path of await assetPaths(request)) {
    const response = await request.get(path, { headers: { 'Accept-Encoding': 'gzip' } })

    expect(response.status()).toBe(200)
    expect(response.headers()['cache-control']).toBe(immutable)
    expect(response.headers()['content-encoding']).toBe('gzip')
    expect(response.headers()['vary']).toContain('Accept-Encoding')
    expect(response.headers()['content-security-policy']).toContain("default-src 'self'")
  }
})

test('the app shell is always revalidated', async ({ request }) => {
  for (const path of ['/', '/index.html', '/settings']) {
    const response = await request.get(path)
    const etag = response.headers()['etag']

    expect(response.status()).toBe(200)
    expect(response.headers()['cache-control']).toBe('no-cache')
    expect(response.headers()['content-security-policy']).toContain("default-src 'self'")
    expect(etag).toBeTruthy()

    const revalidation = await request.get(path, { headers: { 'If-None-Match': etag } })

    expect(revalidation.status()).toBe(304)
  }
})

test('the favicon is cached for a day', async ({ request }) => {
  const response = await request.get('/favicon.svg')

  expect(response.status()).toBe(200)
  expect(response.headers()['cache-control']).toBe('public, max-age=86400')
  expect(response.headers()['content-security-policy']).toContain("default-src 'self'")
})

test('API responses are not compressed', async ({ request }) => {
  const account = {
    email: `caching-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`,
    password: 'Smoke-Test-Passw0rd!',
  }
  const registration = await request.post('/api/auth/register', { data: { ...account, firstName: 'Smoke' } })
  expect(registration.ok()).toBe(true)
  const login = await request.post('/api/auth/login', { data: account })
  expect(login.ok()).toBe(true)
  const { accessToken } = await login.json()

  const response = await request.get('/api/exercises', {
    headers: { 'Accept-Encoding': 'gzip', Authorization: `Bearer ${accessToken}` },
  })

  expect(response.status()).toBe(200)
  expect((await response.body()).byteLength).toBeGreaterThan(1024)
  expect(response.headers()['content-type']).toContain('application/json')
  expect(response.headers()['content-encoding']).toBeUndefined()
})

test('a missing asset is not answered with the app shell or cached', async ({ request }) => {
  const response = await request.get('/assets/missing-00000000.js')

  expect(response.status()).toBe(404)
  expect(response.headers()['cache-control']).toBeUndefined()
})

test('a second visit downloads no assets again', async ({ page }) => {
  await page.goto('/')
  await page.waitForLoadState('networkidle')

  await page.goto('/')
  await page.waitForLoadState('networkidle')
  const assets = await page.evaluate(() =>
    performance
      .getEntriesByType('resource')
      .map((entry) => entry as PerformanceResourceTiming)
      .filter((entry) => new URL(entry.name).pathname.startsWith('/assets/'))
      .map((entry) => ({ name: entry.name, transferSize: entry.transferSize })),
  )

  expect(assets.length).toBeGreaterThan(0)
  expect(assets.filter((asset) => asset.transferSize > 0)).toEqual([])
})
