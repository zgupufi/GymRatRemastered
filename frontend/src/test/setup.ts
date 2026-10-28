import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

class TestIntersectionObserver {
  readonly root = null
  readonly rootMargin = ''
  readonly thresholds = []
  observe() {}
  unobserve() {}
  disconnect() {}
  takeRecords() {
    return []
  }
}

if (!('IntersectionObserver' in globalThis)) {
  Object.defineProperty(globalThis, 'IntersectionObserver', { value: TestIntersectionObserver, writable: true })
}

afterEach(() => {
  cleanup()
})
