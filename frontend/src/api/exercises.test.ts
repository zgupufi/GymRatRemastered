import { afterEach, describe, expect, it, vi } from 'vitest'
import { getExercises, searchExercises } from './exercises'

describe('getExercises', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests the exercise catalog and returns it', async () => {
    const exercises = [{ id: 'a', name: 'Squat', mainMuscle: 'Quads' }]
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json(exercises))
    vi.stubGlobal('fetch', fetchMock)

    const result = await getExercises()

    expect(result).toEqual(exercises)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/exercises')
  })
})

describe('searchExercises', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('searches the catalog with dotted query parameters', async () => {
    const page = { items: [], page: 1, pageSize: 20, totalCount: 0 }
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json(page))
    vi.stubGlobal('fetch', fetchMock)

    const result = await searchExercises({ term: 'row', sortBy: 'MainMuscle', pageSize: 20 })

    expect(result).toEqual(page)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/exercises?search.term=row&sort.by=MainMuscle&pagination.pageSize=20')
  })
})
