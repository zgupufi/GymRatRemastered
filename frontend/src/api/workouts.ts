import { apiClient } from './client'
import { toQuery, type ListParams, type PagedResult } from './pagination'
import type { Schema } from './schema'

export type WorkoutSummary = Schema<'WorkoutSummaryDto'>

export type WorkoutExercise = Schema<'WorkoutExerciseDto'>

export type Workout = Schema<'WorkoutDto'>

export type WorkoutExerciseEntry = Schema<'WorkoutExerciseRequest'>

export type WorkoutSortField = 'CreatedAt' | 'Name'

export function getWorkoutSummaries(
  params: ListParams<WorkoutSortField>,
  signal?: AbortSignal,
): Promise<PagedResult<WorkoutSummary>> {
  return apiClient.get<PagedResult<WorkoutSummary>>(`/api/workouts${toQuery(params)}`, signal)
}

export function getWorkout(id: string, signal?: AbortSignal): Promise<Workout> {
  return apiClient.get<Workout>(`/api/workouts/${id}`, signal)
}

export function createWorkout(name: string): Promise<Schema<'CreateWorkoutResponse'>> {
  return apiClient.post<Schema<'CreateWorkoutResponse'>>('/api/workouts', {
    name,
  } satisfies Schema<'CreateWorkoutRequest'>)
}

export function renameWorkout(id: string, name: string): Promise<void> {
  return apiClient.patch(`/api/workouts/${id}`, { name } satisfies Schema<'RenameWorkoutRequest'>)
}

export function replaceWorkoutExercises(id: string, exercises: WorkoutExerciseEntry[]): Promise<void> {
  return apiClient.put(`/api/workouts/${id}/exercises`, {
    exercises,
  } satisfies Schema<'ReplaceWorkoutExercisesRequest'>)
}

export function deleteWorkout(id: string): Promise<void> {
  return apiClient.delete(`/api/workouts/${id}`)
}

const SUMMARY_PAGE_SIZE = 100

export async function getAllWorkoutSummaries(signal?: AbortSignal): Promise<WorkoutSummary[]> {
  const summaries: WorkoutSummary[] = []
  for (let page = 1; ; page++) {
    const result = await getWorkoutSummaries(
      { sortBy: 'Name', direction: 'Ascending', page, pageSize: SUMMARY_PAGE_SIZE },
      signal,
    )
    summaries.push(...result.items)
    if (result.items.length === 0 || summaries.length >= result.totalCount) {
      return summaries
    }
  }
}
