import type { WeightUnit } from './auth'
import { apiClient } from './client'

export interface UserProfile {
  firstName: string
  lastName: string | null
  email: string
  weightUnit: WeightUnit
  defaultRestTimeSeconds: number
}

export function getUserProfile(signal?: AbortSignal): Promise<UserProfile> {
  return apiClient.get<UserProfile>('/api/users/me', signal)
}
