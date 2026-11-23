import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { login as loginRequest, logout as logoutRequest, register as registerRequest, type RegisterRequest } from '../api/auth'
import { ApiError, refreshSession, setAccessToken, setUnauthorizedHandler } from '../api/client'
import { getUserProfile, updateUserSettings, type UserProfile, type UserSettings } from '../api/users'
import { AuthContext, LoginAfterRegisterError, type AuthApi, type AuthStatus } from './AuthContext'
import { clearSessionHint, hasSessionHint, markSessionActive } from './sessionHint'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>(() => (hasSessionHint() ? 'loading' : 'anonymous'))
  const [user, setUser] = useState<UserProfile | null>(null)
  const [sessionExpired, setSessionExpired] = useState(false)

  const endSession = useCallback(() => {
    clearSessionHint()
    setAccessToken(null)
    setUser(null)
    setStatus('anonymous')
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => {
      endSession()
      setSessionExpired(true)
    })
    return () => setUnauthorizedHandler(null)
  }, [endSession])

  useEffect(() => {
    if (status !== 'loading') {
      return
    }
    const controller = new AbortController()

    async function restore() {
      const outcome = await refreshSession()
      if (controller.signal.aborted) {
        return
      }
      if (outcome === 'rejected') {
        endSession()
        return
      }
      if (outcome === 'unavailable') {
        setStatus('anonymous')
        return
      }
      try {
        const profile = await getUserProfile(controller.signal)
        setUser(profile)
        setStatus('authenticated')
      } catch (error) {
        if (controller.signal.aborted) {
          return
        }
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          endSession()
        } else {
          setAccessToken(null)
          setStatus('anonymous')
        }
      }
    }

    void restore()
    return () => controller.abort()
  }, [status, endSession])

  const login = useCallback(async (email: string, password: string) => {
    const { accessToken } = await loginRequest({ email, password })
    setAccessToken(accessToken)
    setSessionExpired(false)
    try {
      setUser(await getUserProfile())
    } catch (error) {
      setAccessToken(null)
      void logoutRequest().catch(() => undefined)
      throw error
    }
    markSessionActive()
    setStatus('authenticated')
  }, [])

  const logout = useCallback(() => {
    void logoutRequest().catch(() => undefined)
    endSession()
  }, [endSession])

  const register = useCallback(
    async (request: RegisterRequest) => {
      await registerRequest(request)
      try {
        await login(request.email, request.password)
      } catch {
        throw new LoginAfterRegisterError()
      }
    },
    [login],
  )

  const updateSettings = useCallback(async (settings: UserSettings) => {
    await updateUserSettings(settings)
    setUser((current) => (current ? { ...current, ...settings } : current))
  }, [])

  const value = useMemo<AuthApi>(
    () => ({ status, user, sessionExpired, login, register, logout, updateSettings }),
    [status, user, sessionExpired, login, register, logout, updateSettings],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
