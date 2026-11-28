import { lazy } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { SESSION_EXPIRED_NOTICE } from '../auth/RequireAuth'
import { PageLoading } from '../components/layout/PageLoading'
import { LandingPage } from './LandingPage/LandingPage'

const DashboardPage = lazy(async () => ({
  default: (await import('./DashboardPage/DashboardPage')).DashboardPage,
}))

export function HomePage() {
  const { status, sessionExpired } = useAuth()

  if (status === 'loading') {
    return <PageLoading />
  }
  if (status === 'anonymous' && sessionExpired) {
    return <Navigate to="/login" replace state={{ notice: SESSION_EXPIRED_NOTICE, from: '/' }} />
  }
  return status === 'authenticated' ? <DashboardPage /> : <LandingPage />
}
