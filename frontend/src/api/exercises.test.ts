import { afterEach, describe, expect, it, vi } from 'vitest'
import { getExercises } from './exercises'

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
