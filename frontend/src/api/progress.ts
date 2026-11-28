import { apiClient } from './client'
import type { Schema } from './schema'

export type BestSet = Schema<'BestSetDto'>

export type ProgressPoint = Schema<'ExerciseProgressDto'>

function localTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined
  } catch {
    return undefined
  }
}

export type ProgressRange = 'ThreeMonths' | 'OneYear' | 'All'

export function getExerciseProgress(
  exerciseId: string,
  signal?: AbortSignal,
  range?: ProgressRange,
): Promise<ProgressPoint[]> {
  const params = new URLSearchParams()
  const timeZone = localTimeZone()
  if (timeZone) params.set('timeZone', timeZone)
  if (range) params.set('range', range)
  const query = params.size > 0 ? `?${params.toString()}` : ''
  return apiClient.get<ProgressPoint[]>(`/api/exercises/${exerciseId}/progress${query}`, signal)
}
