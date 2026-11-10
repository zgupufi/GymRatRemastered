import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { SESSION_EXPIRED_NOTICE } from '../auth/RequireAuth'
import { Spinner } from '../components/ui/Spinner'
import { DashboardPage } from './DashboardPage/DashboardPage'
import { LandingPage } from './LandingPage/LandingPage'

export function HomePage() {
  const { status, sessionExpired } = useAuth()

  if (status === 'loading') {
    return (
      <main className="page" aria-busy="true">
        <Spinner />
      </main>
    )
  }
  if (status === 'anonymous' && sessionExpired) {
    return <Navigate to="/login" replace state={{ notice: SESSION_EXPIRED_NOTICE, from: '/' }} />
  }
  return status === 'authenticated' ? <DashboardPage /> : <LandingPage />
}
