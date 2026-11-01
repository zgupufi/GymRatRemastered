import { afterEach, describe, expect, it, vi } from 'vitest'
import { finishSession, getLastSession, saveExerciseSets, startSession } from './sessions'

function stubFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response)
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('sessions api', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts a session', async () => {
    const fetchMock = stubFetch(Response.json({ id: 's1' }, { status: 201 }))

    expect(await startSession('w1')).toEqual({ id: 's1' })
    expect(fetchMock.mock.calls[0][0]).toBe('/api/sessions')
    expect(fetchMock.mock.calls[0][1]?.method).toBe('POST')
    expect(fetchMock.mock.calls[0][1]?.body).toBe(JSON.stringify({ workoutId: 'w1' }))
  })

  it('saves the sets of an exercise', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await saveExerciseSets('s1', 'e1', [{ reps: 8, weightKg: 60 }])

    expect(fetchMock.mock.calls[0][0]).toBe('/api/sessions/s1/exercises/e1/sets')
    expect(fetchMock.mock.calls[0][1]?.method).toBe('PUT')
    expect(fetchMock.mock.calls[0][1]?.body).toBe(JSON.stringify({ sets: [{ reps: 8, weightKg: 60 }] }))
  })

  it('finishes a session', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }))

    await finishSession('s1')

    expect(fetchMock.mock.calls[0][0]).toBe('/api/sessions/s1/finish')
    expect(fetchMock.mock.calls[0][1]?.method).toBe('POST')
  })

  it('loads the last session of a workout', async () => {
    const last = { sessionId: null, startedAt: null, durationSeconds: null, exercises: [] }
    const fetchMock = stubFetch(Response.json(last))

    expect(await getLastSession('w1')).toEqual(last)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/workouts/w1/last-session')
  })
})
