import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { login as loginRequest, register as registerRequest, type RegisterRequest } from '../api/auth'
import { ApiError, setAccessToken } from '../api/client'
import { getUserProfile, type UserProfile } from '../api/users'
import { AuthContext, type AuthApi, type AuthStatus } from './AuthContext'
import { clearSession, loadSession, saveSession } from './sessionStorage'

function restoreToken(): boolean {
  const session = loadSession()
  setAccessToken(session?.accessToken ?? null)
  return session !== null
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>(() => (restoreToken() ? 'loading' : 'anonymous'))
  const [user, setUser] = useState<UserProfile | null>(null)

  const endSession = useCallback(() => {
    clearSession()
    setAccessToken(null)
    setUser(null)
    setStatus('anonymous')
  }, [])

  useEffect(() => {
    if (status !== 'loading') {
      return
    }
    const controller = new AbortController()
    getUserProfile(controller.signal)
      .then((profile) => {
        setUser(profile)
        setStatus('authenticated')
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return
        }
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          endSession()
        } else {
          setAccessToken(null)
          setStatus('anonymous')
        }
      })
    return () => controller.abort()
  }, [status, endSession])

  const login = useCallback(async (email: string, password: string) => {
    const { accessToken, expiresAt } = await loginRequest({ email, password })
    setAccessToken(accessToken)
    try {
      setUser(await getUserProfile())
    } catch (error) {
      setAccessToken(null)
      throw error
    }
    saveSession({ accessToken, expiresAt })
    setStatus('authenticated')
  }, [])

  const register = useCallback(
    async (request: RegisterRequest) => {
      await registerRequest(request)
      await login(request.email, request.password)
    },
    [login],
  )

  const value = useMemo<AuthApi>(
    () => ({ status, user, login, register, logout: endSession }),
    [status, user, login, register, endSession],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
