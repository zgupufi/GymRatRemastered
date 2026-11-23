import type { WeightUnit } from './auth'
import { apiClient } from './client'

export interface UserProfile {
  firstName: string
  lastName: string | null
  email: string
  weightUnit: WeightUnit
  defaultRestTimeSeconds: number
}

export interface UserSettings {
  weightUnit: WeightUnit
  defaultRestTimeSeconds: number
}

export function getUserProfile(signal?: AbortSignal): Promise<UserProfile> {
  return apiClient.get<UserProfile>('/api/users/me', signal)
}

export function updateUserSettings(settings: UserSettings): Promise<void> {
  return apiClient.patch('/api/users/me/settings', settings)
}

export function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiClient.post('/api/users/me/password', { currentPassword, newPassword })
}

export function deleteAccount(password: string): Promise<void> {
  return apiClient.delete('/api/users/me', { password })
}

export function downloadExport(signal?: AbortSignal): Promise<Blob> {
  return apiClient.download('/api/users/me/export', signal)
}

export function downloadSetsCsv(signal?: AbortSignal): Promise<Blob> {
  return apiClient.download('/api/users/me/export/sets', signal)
}
