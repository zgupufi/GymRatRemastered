import { apiClient } from './client'

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

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ExerciseQuery {
  term?: string
  muscle?: string
  page: number
  pageSize: number
}

export const MUSCLE_GROUPS = ['Back', 'Biceps', 'Chest', 'Core', 'Legs', 'Shoulders', 'Triceps']

export function getExercises(query: ExerciseQuery, signal?: AbortSignal): Promise<PagedResult<Exercise>> {
  const params = new URLSearchParams()
  const term = query.term?.trim()
  if (term) {
    params.set('search.term', term)
  }
  if (query.muscle) {
    params.set('muscle', query.muscle)
  }
  params.set('sort.by', 'MainMuscle')
  params.set('pagination.page', String(query.page))
  params.set('pagination.pageSize', String(query.pageSize))
  return apiClient.get<PagedResult<Exercise>>(`/api/exercises?${params.toString()}`, signal)
}

export function getExercise(id: string, signal?: AbortSignal): Promise<Exercise> {
  return apiClient.get<Exercise>(`/api/exercises/${encodeURIComponent(id)}`, signal)
}
