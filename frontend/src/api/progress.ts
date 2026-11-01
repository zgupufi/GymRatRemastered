import { apiClient } from './client'
import type { WorkoutSummary } from './workouts'

export interface BestSet {
  reps: number
  weightKg: number
}

export interface ProgressPoint {
  date: string
  bestSet: BestSet
  estimatedOneRepMax: number
}

export interface SplitDay {
  day: number
  workout: WorkoutSummary | null
}

export function getExerciseProgress(exerciseId: string, signal?: AbortSignal): Promise<ProgressPoint[]> {
  return apiClient.get<ProgressPoint[]>(`/api/exercises/${exerciseId}/progress`, signal)
}

export function getSplit(signal?: AbortSignal): Promise<SplitDay[]> {
  return apiClient.get<SplitDay[]>('/api/split', signal)
}
