import { apiClient } from './client'
import type { Schema } from './schema'

export const WeightUnit = { Kilograms: 0, Pounds: 1 } as const satisfies Record<string, Schema<'WeightUnit'>>

export type WeightUnit = Schema<'WeightUnit'>

export type LoginRequest = Schema<'LoginRequest'>

export type LoginResponse = Schema<'LoginResponse'>

export type RegisterRequest = Schema<'RegisterRequest'>

export function login(request: LoginRequest): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/login', request)
}

export function logout(): Promise<void> {
  return apiClient.post('/api/auth/logout', undefined)
}

export function register(request: RegisterRequest): Promise<void> {
  return apiClient.post('/api/auth/register', request)
}
