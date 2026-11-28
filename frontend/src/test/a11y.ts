import { expect } from 'vitest'
import { configureAxe } from 'vitest-axe'

const axe = configureAxe({
  iframes: false,
  rules: {
    'color-contrast': { enabled: false },
    region: { enabled: false },
  },
})

export async function expectNoA11yViolations() {
  const { violations } = await axe(document.body)

  expect(violations.map(({ id, nodes }) => ({ id, nodes: nodes.map((node) => node.html) }))).toEqual([])
}
