import { apiClient } from './client'
import type { Schema } from './schema'

export type UserProfile = Schema<'UserProfileDto'>

export type UserSettings = {
  [Key in keyof Schema<'UpdateUserSettingsRequest'>]: NonNullable<Schema<'UpdateUserSettingsRequest'>[Key]>
}

export function getUserProfile(signal?: AbortSignal): Promise<UserProfile> {
  return apiClient.get<UserProfile>('/api/users/me', signal)
}

export function updateUserSettings(settings: UserSettings): Promise<void> {
  return apiClient.patch('/api/users/me/settings', settings)
}

export function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiClient.post('/api/users/me/password', {
    currentPassword,
    newPassword,
  } satisfies Schema<'ChangePasswordRequest'>)
}

export function deleteAccount(password: string): Promise<void> {
  return apiClient.delete('/api/users/me', { password } satisfies Schema<'DeleteAccountRequest'>)
}

export function downloadExport(signal?: AbortSignal): Promise<Blob> {
  return apiClient.download('/api/users/me/export', signal)
}

export function downloadSetsCsv(signal?: AbortSignal): Promise<Blob> {
  return apiClient.download('/api/users/me/export/sets', signal)
}
