import { afterEach, describe, expect, it, vi } from 'vitest'
import { getWorkout, getWorkoutSummaries, replaceWorkoutExercises } from './workouts'

describe('workouts api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests every page of workout summaries', async () => {
    const summary = (id: string) => ({ id, name: id, exerciseCount: 0, mainMuscles: [] })
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(Response.json({ items: [summary('a')], page: 1, pageSize: 100, totalCount: 2 }))
      .mockResolvedValueOnce(Response.json({ items: [summary('b')], page: 2, pageSize: 100, totalCount: 2 }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await getWorkoutSummaries()

    expect(result.map((item) => item.id)).toEqual(['a', 'b'])
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts?pagination.page=1&pagination.pageSize=100')
    expect(fetchMock.mock.calls[1][0]).toBe('/api/workouts?pagination.page=2&pagination.pageSize=100')
  })

  it('stops when a page comes back empty', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ items: [], page: 1, pageSize: 100, totalCount: 5 }))
    vi.stubGlobal('fetch', fetchMock)

    expect(await getWorkoutSummaries()).toEqual([])
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('requests a workout by id', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ id: 'w', name: 'Push', exercises: [] }))
    vi.stubGlobal('fetch', fetchMock)

    await getWorkout('w')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts/w')
  })

  it('replaces the exercises of a workout', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await replaceWorkoutExercises('w', [{ exerciseId: 'e', targetSets: 3 }])

    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts/w/exercises')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ exercises: [{ exerciseId: 'e', targetSets: 3 }] }),
    })
  })
})
