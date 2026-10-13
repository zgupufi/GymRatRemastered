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

export function getExercises(signal?: AbortSignal): Promise<Exercise[]> {
  return apiClient.get<Exercise[]>('/api/exercises', signal)
}
