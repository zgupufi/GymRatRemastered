import { apiClient } from './client'

export interface BestSet {
  reps: number
  weightKg: number
}

export interface ProgressPoint {
  date: string
  bestSet: BestSet
  estimatedOneRepMax: number
}

function localTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined
  } catch {
    return undefined
  }
}

export function getExerciseProgress(exerciseId: string, signal?: AbortSignal): Promise<ProgressPoint[]> {
  const timeZone = localTimeZone()
  const query = timeZone ? `?timeZone=${encodeURIComponent(timeZone)}` : ''
  return apiClient.get<ProgressPoint[]>(`/api/exercises/${exerciseId}/progress${query}`, signal)
}
