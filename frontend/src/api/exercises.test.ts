import { afterEach, describe, expect, it, vi } from 'vitest'
import { getExercise, searchExercises } from './exercises'

function stubFetch(body: unknown) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json(body))
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('exercises api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('searches the catalog with dotted query parameters', async () => {
    const page = { items: [], page: 1, pageSize: 20, totalCount: 0 }
    const fetchMock = stubFetch(page)

    const result = await searchExercises({ term: 'row', sortBy: 'MainMuscle', pageSize: 20 })

    expect(result).toEqual(page)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/exercises?search.term=row&sort.by=MainMuscle&pagination.pageSize=20')
  })

  it('adds the muscle filter', async () => {
    const fetchMock = stubFetch({ items: [], page: 2, pageSize: 24, totalCount: 0 })

    await searchExercises({ muscle: 'Legs', page: 2, pageSize: 24 })

    expect(fetchMock.mock.calls[0][0]).toBe('/api/exercises?pagination.page=2&pagination.pageSize=24&muscle=Legs')
  })

  it('sends no query string without params', async () => {
    const fetchMock = stubFetch({ items: [], page: 1, pageSize: 20, totalCount: 0 })

    await searchExercises({})

    expect(fetchMock.mock.calls[0][0]).toBe('/api/exercises')
  })

  it('requests a single exercise by id', async () => {
    const fetchMock = stubFetch({ id: 'a' })

    await getExercise('a/b')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/exercises/a%2Fb')
  })
})
