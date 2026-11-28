import { lazy } from 'react'
import { Route, Routes } from 'react-router-dom'
import { RequireAuth } from './auth/RequireAuth'
import { AppShell } from './components/layout/AppShell'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage/LoginPage'
import { RegisterPage } from './pages/RegisterPage/RegisterPage'

const ExercisesPage = lazy(async () => ({
  default: (await import('./pages/ExercisesPage/ExercisesPage')).ExercisesPage,
}))
const ExerciseDetailsPage = lazy(async () => ({
  default: (await import('./pages/ExerciseDetailsPage/ExerciseDetailsPage')).ExerciseDetailsPage,
}))
const WorkoutsPage = lazy(async () => ({
  default: (await import('./pages/WorkoutsPage/WorkoutsPage')).WorkoutsPage,
}))
const SplitPage = lazy(async () => ({
  default: (await import('./pages/SplitPage/SplitPage')).SplitPage,
}))
const WorkoutExecutionPage = lazy(async () => ({
  default: (await import('./pages/WorkoutExecutionPage/WorkoutExecutionPage')).WorkoutExecutionPage,
}))
const ProgressionPage = lazy(async () => ({
  default: (await import('./pages/ProgressionPage/ProgressionPage')).ProgressionPage,
}))
const SettingsPage = lazy(async () => ({
  default: (await import('./pages/SettingsPage/SettingsPage')).SettingsPage,
}))

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<HomePage />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route element={<RequireAuth />}>
          <Route path="exercises" element={<ExercisesPage />} />
          <Route path="exercises/:id" element={<ExerciseDetailsPage />} />
          <Route path="workouts" element={<WorkoutsPage />} />
          <Route path="split" element={<SplitPage />} />
          <Route path="workout-execution" element={<WorkoutExecutionPage />} />
          <Route path="progress" element={<ProgressionPage />} />
          <Route path="settings" element={<SettingsPage />} />
        </Route>
        <Route path="*" element={<PlaceholderPage title="Page not found" description="That page does not exist." homeLink />} />
      </Route>
    </Routes>
  )
}
