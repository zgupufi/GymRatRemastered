import { describe, expect, it } from 'vitest'
import { toQuery } from './pagination'

describe('toQuery', () => {
  it('returns an empty string without parameters', () => {
    expect(toQuery({})).toBe('')
  })

  it('uses dotted keys for every parameter', () => {
    const query = toQuery({ term: ' bench ', sortBy: 'Name', direction: 'Descending', page: 2, pageSize: 10 })

    expect(query).toBe('?search.term=bench&sort.by=Name&sort.direction=Descending&pagination.page=2&pagination.pageSize=10')
  })

  it('skips a blank search term', () => {
    expect(toQuery({ term: '   ', page: 1 })).toBe('?pagination.page=1')
  })
})
