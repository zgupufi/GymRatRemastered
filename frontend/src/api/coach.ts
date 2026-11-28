import { apiClient } from './client'
import type { PagedResult } from './pagination'
import type { Schema } from './schema'

export type SuggestionStatus = Schema<'SuggestionStatus'>

export type CoachSuggestion = Schema<'CoachSuggestionDto'>

const PENDING_PAGE_SIZE = 100

export function getPendingSuggestions(signal?: AbortSignal): Promise<PagedResult<CoachSuggestion>> {
  return apiClient.get<PagedResult<CoachSuggestion>>(
    `/api/coach/suggestions?pagination.pageSize=${PENDING_PAGE_SIZE}`,
    signal,
  )
}

export function applySuggestion(id: string): Promise<void> {
  return apiClient.post(`/api/coach/suggestions/${id}/apply`, {})
}

export function dismissSuggestion(id: string): Promise<void> {
  return apiClient.post(`/api/coach/suggestions/${id}/dismiss`, {})
}
