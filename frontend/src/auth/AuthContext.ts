import { createContext, useContext } from 'react'
import type { RegisterRequest } from '../api/auth'
import type { UserProfile, UserSettings } from '../api/users'

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface AuthApi {
  status: AuthStatus
  user: UserProfile | null
  login: (email: string, password: string) => Promise<void>
  register: (request: RegisterRequest) => Promise<void>
  logout: () => void
  updateSettings: (settings: UserSettings) => Promise<void>
}

export class LoginAfterRegisterError extends Error {
  constructor() {
    super('The account was created but logging in failed')
    this.name = 'LoginAfterRegisterError'
  }
}

export const AuthContext = createContext<AuthApi | null>(null)

export function useAuth(): AuthApi {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used inside an AuthProvider')
  }
  return context
}
