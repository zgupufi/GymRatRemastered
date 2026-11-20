import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from './client'
import { getExerciseProgress } from './progress'

vi.mock('./client', () => ({ apiClient: { get: vi.fn() } }))

describe('progress api', () => {
  afterEach(() => {
    vi.clearAllMocks()
    vi.restoreAllMocks()
  })

  it('requests the progress of an exercise', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockReturnValue({ resolvedOptions: () => ({ timeZone: 'Europe/Bucharest' }) } as Intl.DateTimeFormat)
    vi.mocked(apiClient.get).mockResolvedValue([])
    const signal = new AbortController().signal

    await expect(getExerciseProgress('e1', signal)).resolves.toEqual([])

    expect(apiClient.get).toHaveBeenCalledWith('/api/exercises/e1/progress?timeZone=Europe%2FBucharest', signal)
  })
})
