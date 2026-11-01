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

export function getExerciseProgress(exerciseId: string, signal?: AbortSignal): Promise<ProgressPoint[]> {
  return apiClient.get<ProgressPoint[]>(`/api/exercises/${exerciseId}/progress`, signal)
}
