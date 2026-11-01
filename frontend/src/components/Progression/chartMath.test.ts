import { describe, expect, it } from 'vitest'
import { formatDate, formatFullDate, niceDomain, pickLabelIndexes, smoothPath } from './chartMath'

describe('chartMath', () => {
  it('formats dates without shifting the day', () => {
    expect(formatDate('2026-11-07')).toBe('7 Nov')
    expect(formatFullDate('2026-11-07')).toBe('7 Nov 2026')
  })

  it('builds a domain that covers the values with round ticks', () => {
    const domain = niceDomain([82, 108])

    expect(domain.min).toBeLessThanOrEqual(82)
    expect(domain.max).toBeGreaterThanOrEqual(108)
    expect(domain.ticks.length).toBeGreaterThanOrEqual(4)
    expect(domain.ticks[0]).toBe(domain.min)
  })

  it('handles a flat series', () => {
    const domain = niceDomain([60, 60])

    expect(domain.min).toBeLessThan(60)
    expect(domain.max).toBeGreaterThan(60)
  })

  it('never goes below zero', () => {
    expect(niceDomain([0, 5]).min).toBe(0)
  })

  it('draws a single point and a monotone curve', () => {
    expect(smoothPath([{ x: 1, y: 2 }])).toBe('M1,2')
    const path = smoothPath([
      { x: 0, y: 10 },
      { x: 10, y: 5 },
      { x: 20, y: 5 },
      { x: 30, y: 0 },
    ])
    expect(path.startsWith('M0,10')).toBe(true)
    expect(path.match(/C/g)).toHaveLength(3)
  })

  it('draws a two point curve', () => {
    expect(smoothPath([{ x: 0, y: 0 }, { x: 3, y: 3 }])).toBe('M0,0 C1,1 2,2 3,3')
  })

  it('picks evenly spread label indexes', () => {
    expect(pickLabelIndexes(3, 5)).toEqual([0, 1, 2])
    expect(pickLabelIndexes(10, 4)).toEqual([0, 3, 6, 9])
  })
})
