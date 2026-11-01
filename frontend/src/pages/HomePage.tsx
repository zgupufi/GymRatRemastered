import { useAuth } from '../auth/AuthContext'
import { Spinner } from '../components/ui/Spinner'
import { DashboardPage } from './DashboardPage/DashboardPage'
import { LandingPage } from './LandingPage/LandingPage'

export function HomePage() {
  const { status } = useAuth()

  if (status === 'loading') {
    return (
      <main className="page" aria-busy="true">
        <Spinner />
      </main>
    )
  }
  return status === 'authenticated' ? <DashboardPage /> : <LandingPage />
}
