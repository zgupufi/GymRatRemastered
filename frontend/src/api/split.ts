import { apiClient } from './client'
import type { Schema } from './schema'

export type SplitDay = Schema<'SplitDayDto'>

export function getSplit(signal?: AbortSignal): Promise<SplitDay[]> {
  return apiClient.get<SplitDay[]>('/api/split', signal)
}

export function assignSplitDay(day: number, workoutId: string | null): Promise<void> {
  return apiClient.put(`/api/split/${day}`, { workoutId } satisfies Schema<'AssignSplitDayRequest'>)
}
