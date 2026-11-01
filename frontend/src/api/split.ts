import { apiClient } from './client'
import type { WorkoutSummary } from './workouts'

export interface SplitDay {
  day: number
  workout: WorkoutSummary | null
}

export function getSplit(signal?: AbortSignal): Promise<SplitDay[]> {
  return apiClient.get<SplitDay[]>('/api/split', signal)
}

export function assignSplitDay(day: number, workoutId: string | null): Promise<void> {
  return apiClient.put(`/api/split/${day}`, { workoutId })
}
