import { afterEach, describe, expect, it, vi } from 'vitest'
import { getExercise, getExercises } from './exercises'

describe('exercises api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests a page of exercises with dotted query params', async () => {
    const page = { items: [{ id: 'a', name: 'Squat', mainMuscle: 'Legs' }], page: 2, pageSize: 24, totalCount: 40 }
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json(page))
    vi.stubGlobal('fetch', fetchMock)

    const result = await getExercises({ term: ' squat ', muscle: 'Legs', page: 2, pageSize: 24 })

    expect(result).toEqual(page)
    const url = new URL(String(fetchMock.mock.calls[0][0]), 'http://localhost')
    expect(url.pathname).toBe('/api/exercises')
    expect(url.searchParams.get('search.term')).toBe('squat')
    expect(url.searchParams.get('muscle')).toBe('Legs')
    expect(url.searchParams.get('sort.by')).toBe('MainMuscle')
    expect(url.searchParams.get('pagination.page')).toBe('2')
    expect(url.searchParams.get('pagination.pageSize')).toBe('24')
  })

  it('omits the search term and muscle when they are empty', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ items: [], page: 1, pageSize: 24, totalCount: 0 }))
    vi.stubGlobal('fetch', fetchMock)

    await getExercises({ term: '  ', muscle: '', page: 1, pageSize: 24 })

    const url = new URL(String(fetchMock.mock.calls[0][0]), 'http://localhost')
    expect(url.searchParams.has('search.term')).toBe(false)
    expect(url.searchParams.has('muscle')).toBe(false)
  })

  it('requests a single exercise by id', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ id: 'a' }))
    vi.stubGlobal('fetch', fetchMock)

    await getExercise('a/b')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/exercises/a%2Fb')
  })
})
