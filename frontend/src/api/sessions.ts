import { apiClient } from './client'
import type { Schema } from './schema'

export type SessionSet = Schema<'SessionSetDto'>

export type LastSessionExercise = Schema<'LastSessionExerciseDto'>

export type LastSession = Schema<'LastSessionDto'>

export type ActiveSession = Schema<'ActiveSessionDto'>

export type SessionSetEntry = Schema<'SessionSetRequest'>

export type ProgressionReason = Schema<'ProgressionReason'>

export type ExerciseTarget = Schema<'ExerciseTargetDto'>

export type ServedTarget = Schema<'ServedTargetRequest'>

const WRITE_TIMEOUT_MS = 15_000

export function startSession(workoutId: string): Promise<Schema<'StartSessionResponse'>> {
  return apiClient.post<Schema<'StartSessionResponse'>>('/api/sessions', { workoutId } satisfies Schema<'StartSessionRequest'>)
}

export function getActiveSession(signal?: AbortSignal): Promise<ActiveSession> {
  return apiClient.get<ActiveSession>('/api/sessions/active', signal)
}

export function saveExerciseSets(sessionId: string, exerciseId: string, sets: SessionSetEntry[], target?: ServedTarget): Promise<void> {
  return apiClient.put(
    `/api/sessions/${sessionId}/exercises/${exerciseId}/sets`,
    (target ? { sets, target } : { sets }) satisfies Schema<'SaveExerciseSetsRequest'>,
    AbortSignal.timeout(WRITE_TIMEOUT_MS),
  )
}

export function finishSession(sessionId: string): Promise<void> {
  return apiClient.post(`/api/sessions/${sessionId}/finish`, {}, AbortSignal.timeout(WRITE_TIMEOUT_MS))
}

export function getLastSession(workoutId: string, signal?: AbortSignal): Promise<LastSession> {
  return apiClient.get<LastSession>(`/api/workouts/${workoutId}/last-session`, signal)
}

export function getTargets(workoutId: string, signal?: AbortSignal): Promise<ExerciseTarget[]> {
  return apiClient.get<ExerciseTarget[]>(`/api/workouts/${workoutId}/targets`, signal)
}
