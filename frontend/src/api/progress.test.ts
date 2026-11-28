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
    vi.spyOn(Intl, 'DateTimeFormat').mockReturnValue({
      resolvedOptions: () => ({ timeZone: 'Europe/Bucharest' }),
    } as Intl.DateTimeFormat)
    vi.mocked(apiClient.get).mockResolvedValue([])
    const signal = new AbortController().signal

    await expect(getExerciseProgress('e1', signal)).resolves.toEqual([])

    expect(apiClient.get).toHaveBeenCalledWith('/api/exercises/e1/progress?timeZone=Europe%2FBucharest', signal)
  })

  it('omits the time zone when the browser cannot report one', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockReturnValue({
      resolvedOptions: () => ({ timeZone: '' }),
    } as Intl.DateTimeFormat)
    vi.mocked(apiClient.get).mockResolvedValue([])

    await getExerciseProgress('e1')

    expect(apiClient.get).toHaveBeenCalledWith('/api/exercises/e1/progress', undefined)
  })

  it('omits the time zone when reading it throws', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(() => {
      throw new Error('unsupported')
    })
    vi.mocked(apiClient.get).mockResolvedValue([])

    await getExerciseProgress('e1')

    expect(apiClient.get).toHaveBeenCalledWith('/api/exercises/e1/progress', undefined)
  })

  it('sends the requested range next to the time zone', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockReturnValue({
      resolvedOptions: () => ({ timeZone: 'Europe/Bucharest' }),
    } as Intl.DateTimeFormat)
    vi.mocked(apiClient.get).mockResolvedValue([])

    await getExerciseProgress('e1', undefined, 'ThreeMonths')

    expect(apiClient.get).toHaveBeenCalledWith(
      '/api/exercises/e1/progress?timeZone=Europe%2FBucharest&range=ThreeMonths',
      undefined,
    )
  })
})
