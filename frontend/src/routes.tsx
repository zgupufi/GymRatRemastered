import { Route, Routes } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { ExercisesPage } from './pages/ExercisesPage/ExercisesPage'
import { ExerciseDetailsPage } from './pages/ExerciseDetailsPage/ExerciseDetailsPage'
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
        <Route path="exercises/:id" element={<ExerciseDetailsPage />} />
        <Route path="workouts" element={<PlaceholderPage title="My workouts" description="Create, edit and organize your routines." />} />
        <Route path="split" element={<PlaceholderPage title="My split" description="Assign your workouts to each day of the week." />} />
        <Route
          path="workout-execution"
          element={<PlaceholderPage title="Workout" description="Log your sets and keep the rest timer going." />}
        />
        <Route path="progress" element={<PlaceholderPage title="Progression" description="Track your evolution and break plateaus." />} />
        <Route path="*" element={<PlaceholderPage title="Page not found" description="That page does not exist." />} />
      </Route>
    </Routes>
  )
}
