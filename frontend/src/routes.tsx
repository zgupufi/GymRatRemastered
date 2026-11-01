import { Route, Routes } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { ProgressionPage } from './pages/ProgressionPage/ProgressionPage'
import { WorkoutsPage } from './pages/WorkoutsPage/WorkoutsPage'
import { ExercisesPage } from './pages/ExercisesPage'
import { LandingPage } from './pages/LandingPage/LandingPage'
import { LoginPage } from './pages/LoginPage/LoginPage'
import { RegisterPage } from './pages/RegisterPage/RegisterPage'

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<LandingPage />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="exercises" element={<ExercisesPage />} />
        <Route
          path="exercises/:id"
          element={<PlaceholderPage title="Exercise details" description="Form, muscles and equipment." />}
        />
        <Route path="workouts" element={<WorkoutsPage />} />
        <Route path="split" element={<PlaceholderPage title="My split" description="Assign your workouts to each day of the week." />} />
        <Route
          path="workout-execution"
          element={<PlaceholderPage title="Workout" description="Log your sets and keep the rest timer going." />}
        />
        <Route path="progress" element={<ProgressionPage />} />
        <Route path="*" element={<PlaceholderPage title="Page not found" description="That page does not exist." />} />
      </Route>
    </Routes>
  )
}
