import { describe, expect, it } from 'vitest'
import tokensCss from './tokens.css?raw'

const stylesheets = import.meta.glob<string>('../**/*.css', { query: '?raw', import: 'default', eager: true })

const textTokens = ['--text', '--text-2', '--text-3', '--text-4']
const surfaceTokens = ['--bg', '--bg-elevated', '--surface', '--surface-2', '--surface-3']

function declarations(block: string) {
  return Object.fromEntries([...block.matchAll(/(--[\w-]+):\s*(#[0-9a-f]{6})\s*;/gi)].map((match) => [match[1], match[2]]))
}

function blockAfter(marker: string) {
  const start = tokensCss.indexOf('{', tokensCss.indexOf(marker) + marker.length)
  return tokensCss.slice(start + 1, tokensCss.indexOf('}', start))
}

function luminance(hex: string) {
  const [red, green, blue] = [1, 3, 5].map((index) => {
    const channel = parseInt(hex.slice(index, index + 2), 16) / 255
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

function contrast(first: string, second: string) {
  const [darker, lighter] = [luminance(first), luminance(second)].sort((a, b) => a - b)
  return (lighter + 0.05) / (darker + 0.05)
}

const base = declarations(blockAfter(':root'))
const moreContrast = { ...base, ...declarations(blockAfter('(prefers-contrast: more) {\n  :root')) }
const pairs = textTokens.flatMap((text) => surfaceTokens.map((surface) => ({ text, surface })))

describe.each([
  ['default', base],
  ['more contrast', moreContrast],
])('%s text colors', (_, colors) => {
  it.each(pairs)('$text on $surface meets WCAG AA', ({ text, surface }) => {
    expect(contrast(colors[text], colors[surface])).toBeGreaterThanOrEqual(4.5)
  })
})

describe('text color usage', () => {
  it.each(textTokens)('does not dim %s when more contrast is requested', (text) => {
    expect(contrast(moreContrast[text], moreContrast['--surface-3'])).toBeGreaterThanOrEqual(
      contrast(base[text], base['--surface-3']),
    )
  })

  it('keeps --text-4 for disabled controls only', () => {
    const rules = Object.values(stylesheets).flatMap((css) => [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)])
    const selectors = rules.filter(([, , body]) => body.includes('var(--text-4)')).map(([, selector]) => selector.trim())

    expect(selectors.length).toBeGreaterThan(0)
    expect(selectors.filter((selector) => !selector.split(',').every((part) => part.includes(':disabled')))).toEqual([])
  })
})
