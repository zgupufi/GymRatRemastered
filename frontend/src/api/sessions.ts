import { apiClient } from './client'

export interface SessionSet {
  setNumber: number
  reps: number
  weightKg: number
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
}

export function startSession(workoutId: string): Promise<{ id: string }> {
  return apiClient.post<{ id: string }>('/api/sessions', { workoutId })
}

export function saveExerciseSets(sessionId: string, exerciseId: string, sets: SessionSetEntry[]): Promise<void> {
  return apiClient.put(`/api/sessions/${sessionId}/exercises/${exerciseId}/sets`, { sets })
}

export function finishSession(sessionId: string): Promise<void> {
  return apiClient.post(`/api/sessions/${sessionId}/finish`, {})
}

export function getLastSession(workoutId: string, signal?: AbortSignal): Promise<LastSession> {
  return apiClient.get<LastSession>(`/api/workouts/${workoutId}/last-session`, signal)
}
