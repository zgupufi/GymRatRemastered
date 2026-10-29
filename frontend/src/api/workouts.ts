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

export function getWorkoutSummaries(signal?: AbortSignal): Promise<PagedResult<WorkoutSummary>> {
  return apiClient.get<PagedResult<WorkoutSummary>>('/api/workouts?pagination.pageSize=100', signal)
}

export function getWorkout(id: string, signal?: AbortSignal): Promise<Workout> {
  return apiClient.get<Workout>(`/api/workouts/${encodeURIComponent(id)}`, signal)
}

export function replaceWorkoutExercises(id: string, exercises: WorkoutExerciseRequest[]): Promise<void> {
  return apiClient.put(`/api/workouts/${encodeURIComponent(id)}/exercises`, { exercises })
}
