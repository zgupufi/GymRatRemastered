import { apiClient } from './client'

export const WeightUnit = { Kilograms: 0, Pounds: 1 } as const

export type WeightUnit = (typeof WeightUnit)[keyof typeof WeightUnit]

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
}

export interface RegisterRequest {
  email: string
  password: string
  firstName: string
  lastName: string | null
  weightUnit: WeightUnit
}

export function login(request: LoginRequest): Promise<LoginResponse> {
  return apiClient.post<LoginResponse>('/api/auth/login', request)
}

export function register(request: RegisterRequest): Promise<void> {
  return apiClient.post('/api/auth/register', request)
}
