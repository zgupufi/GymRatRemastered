import { useState } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Spinner } from '../components/ui/Spinner'
import { useAuth } from './AuthContext'

export const SESSION_EXPIRED_NOTICE = 'Your session expired. Log in again to continue.'

export function RequireAuth() {
  const { status, sessionExpired } = useAuth()
  const location = useLocation()
  const [wasAuthenticated, setWasAuthenticated] = useState(false)

  if (status === 'authenticated' && !wasAuthenticated) {
    setWasAuthenticated(true)
  }

  if (status === 'loading') {
    return (
      <main className="page" aria-busy="true">
        <Spinner />
      </main>
    )
  }
  if (status === 'anonymous') {
    const choseToLeave = wasAuthenticated && !sessionExpired
    const state = {
      ...(choseToLeave ? {} : { from: location.pathname + location.search }),
      ...(sessionExpired ? { notice: SESSION_EXPIRED_NOTICE } : {}),
    }
    return <Navigate to="/login" replace state={state} />
  }
  return <Outlet />
}
