import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from './client'
import { getExerciseProgress, getSplit } from './progress'

vi.mock('./client', () => ({ apiClient: { get: vi.fn() } }))

describe('progress api', () => {
  afterEach(() => vi.clearAllMocks())

  it('requests the progress of an exercise', async () => {
    vi.mocked(apiClient.get).mockResolvedValue([])
    const signal = new AbortController().signal

    await expect(getExerciseProgress('e1', signal)).resolves.toEqual([])

    expect(apiClient.get).toHaveBeenCalledWith('/api/exercises/e1/progress', signal)
  })

  it('requests the weekly split', async () => {
    vi.mocked(apiClient.get).mockResolvedValue([])

    await getSplit()

    expect(apiClient.get).toHaveBeenCalledWith('/api/split', undefined)
  })
})
