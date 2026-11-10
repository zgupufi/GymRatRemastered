import { apiClient } from './client'

export interface SessionSet {
  setNumber: number
  reps: number
  weightKg: number
  repsInReserve?: number | null
}

export interface LastSessionExercise {
  exerciseId: string
  sets: SessionSet[]
}

export interface LastSession {
  sessionId: string | null
  startedAt: string | null
  durationSeconds: number | null
  exercises: LastSessionExercise[]
}

export interface SessionSetEntry {
  reps: number
  weightKg: number
  repsInReserve?: number | null
}

export type ProgressionReason = 'NoHistory' | 'Hold' | 'Stalled' | 'IncreaseWeight' | 'AddRep'

export interface ExerciseTarget {
  order: number
  exerciseId: string
  sets: number
  repsMin: number
  repsMax: number
  weightKg: number | null
  reps: number
  reason: ProgressionReason
  lastWeightKg: number | null
  lastReps: number[]
}

export interface ServedTarget {
  weightKg: number | null
  reps: number
  reason: ProgressionReason
}

export function startSession(workoutId: string): Promise<{ id: string }> {
  return apiClient.post<{ id: string }>('/api/sessions', { workoutId })
}

export function saveExerciseSets(sessionId: string, exerciseId: string, sets: SessionSetEntry[], target?: ServedTarget): Promise<void> {
  return apiClient.put(`/api/sessions/${sessionId}/exercises/${exerciseId}/sets`, target ? { sets, target } : { sets })
}

export function finishSession(sessionId: string): Promise<void> {
  return apiClient.post(`/api/sessions/${sessionId}/finish`, {})
}

export function getLastSession(workoutId: string, signal?: AbortSignal): Promise<LastSession> {
  return apiClient.get<LastSession>(`/api/workouts/${workoutId}/last-session`, signal)
}

export function getTargets(workoutId: string, signal?: AbortSignal): Promise<ExerciseTarget[]> {
  return apiClient.get<ExerciseTarget[]>(`/api/workouts/${workoutId}/targets`, signal)
}
