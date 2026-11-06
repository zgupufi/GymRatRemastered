import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Spinner } from '../components/ui/Spinner'
import { useAuth } from './AuthContext'

export const SESSION_EXPIRED_NOTICE = 'Your session expired. Log in again to continue.'

export function RequireAuth() {
  const { status, sessionExpired } = useAuth()
  const location = useLocation()

  if (status === 'loading') {
    return (
      <main className="page" aria-busy="true">
        <Spinner />
      </main>
    )
  }
  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={sessionExpired ? { notice: SESSION_EXPIRED_NOTICE, from: location.pathname } : undefined} />
  }
  return <Outlet />
}
