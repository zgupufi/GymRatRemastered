import { afterEach, describe, expect, it, vi } from 'vitest'
import { getWorkout, getWorkoutSummaries, replaceWorkoutExercises } from './workouts'

describe('workouts api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests the workout summaries with a large page', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(Response.json({ items: [], page: 1, pageSize: 100, totalCount: 0 }))
    vi.stubGlobal('fetch', fetchMock)

    await getWorkoutSummaries()

    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts?pagination.pageSize=100')
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
