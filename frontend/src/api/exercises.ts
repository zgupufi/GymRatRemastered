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

export function getExercises(signal?: AbortSignal): Promise<Exercise[]> {
  return apiClient.get<Exercise[]>('/api/exercises', signal)
}

export type ExerciseSortField = 'Name' | 'MainMuscle' | 'Difficulty'

export function searchExercises(params: ListParams<ExerciseSortField>, signal?: AbortSignal): Promise<PagedResult<Exercise>> {
  return apiClient.get<PagedResult<Exercise>>(`/api/exercises${toQuery(params)}`, signal)
}
