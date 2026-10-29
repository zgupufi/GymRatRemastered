import { apiClient } from './client'
import type { Exercise, PagedResult } from './exercises'

export interface WorkoutSummary {
  id: string
  name: string
  exerciseCount: number
  mainMuscles: string[]
}

export interface WorkoutExercise {
  order: number
  targetSets: number
  exercise: Exercise
}

export interface Workout {
  id: string
  name: string
  exercises: WorkoutExercise[]
}

export interface WorkoutExerciseRequest {
  exerciseId: string
  targetSets: number
}

const SUMMARY_PAGE_SIZE = 100

export async function getWorkoutSummaries(signal?: AbortSignal): Promise<WorkoutSummary[]> {
  const summaries: WorkoutSummary[] = []
  for (let page = 1; ; page++) {
    const result = await apiClient.get<PagedResult<WorkoutSummary>>(
      `/api/workouts?pagination.page=${page}&pagination.pageSize=${SUMMARY_PAGE_SIZE}`,
      signal,
    )
    summaries.push(...result.items)
    if (result.items.length === 0 || summaries.length >= result.totalCount) {
      return summaries
    }
  }
}

export function getWorkout(id: string, signal?: AbortSignal): Promise<Workout> {
  return apiClient.get<Workout>(`/api/workouts/${encodeURIComponent(id)}`, signal)
}

export function replaceWorkoutExercises(id: string, exercises: WorkoutExerciseRequest[]): Promise<void> {
  return apiClient.put(`/api/workouts/${encodeURIComponent(id)}/exercises`, { exercises })
}
