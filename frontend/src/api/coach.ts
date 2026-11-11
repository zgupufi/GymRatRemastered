import { apiClient } from './client'
import type { PagedResult } from './pagination'

export type SuggestionStatus = 'Pending' | 'Applied' | 'Dismissed' | 'Expired'

export interface CoachSuggestion {
  id: string
  workoutId: string
  workoutName: string
  exerciseId: string
  exerciseName: string
  targetSets: number | null
  repsMin: number | null
  repsMax: number | null
  weightStepKg: number | null
  currentTargetSets: number
  currentRepsMin: number
  currentRepsMax: number
  currentWeightStepKg: number
  rationale: string
  status: SuggestionStatus
}

const PENDING_PAGE_SIZE = 100

export function getPendingSuggestions(signal?: AbortSignal): Promise<PagedResult<CoachSuggestion>> {
  return apiClient.get<PagedResult<CoachSuggestion>>(`/api/coach/suggestions?pagination.pageSize=${PENDING_PAGE_SIZE}`, signal)
}

export function applySuggestion(id: string): Promise<void> {
  return apiClient.post(`/api/coach/suggestions/${id}/apply`, {})
}

export function dismissSuggestion(id: string): Promise<void> {
  return apiClient.post(`/api/coach/suggestions/${id}/dismiss`, {})
}
