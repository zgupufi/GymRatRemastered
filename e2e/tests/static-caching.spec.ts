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
  const downloaded = await page.evaluate(() =>
    performance
      .getEntriesByType('resource')
      .map((entry) => entry as PerformanceResourceTiming)
      .filter((entry) => new URL(entry.name).pathname.startsWith('/assets/') && entry.transferSize > 0)
      .map((entry) => entry.name),
  )

  expect(downloaded).toEqual([])
})
