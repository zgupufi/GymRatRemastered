import { apiClient } from './client'
import { toQuery, type ListParams, type PagedResult } from './pagination'

export interface Exercise {
  id: string
  name: string
  mainMuscle: string
  subMuscles: string | null
  equipment: string | null
  difficulty: string | null
  description: string | null
  youtubeLink: string | null
}

export const MUSCLE_GROUPS = ['Back', 'Biceps', 'Chest', 'Core', 'Legs', 'Shoulders', 'Triceps']

export type ExerciseSortField = 'Name' | 'MainMuscle' | 'Difficulty'

export interface ExerciseListParams extends ListParams<ExerciseSortField> {
  muscle?: string
}

export function searchExercises(params: ExerciseListParams, signal?: AbortSignal): Promise<PagedResult<Exercise>> {
  const query = new URLSearchParams(toQuery(params))
  if (params.muscle) {
    query.set('muscle', params.muscle)
  }
  const text = query.toString()
  return apiClient.get<PagedResult<Exercise>>(`/api/exercises${text ? `?${text}` : ''}`, signal)
}

export function getExercise(id: string, signal?: AbortSignal): Promise<Exercise> {
  return apiClient.get<Exercise>(`/api/exercises/${encodeURIComponent(id)}`, signal)
}
