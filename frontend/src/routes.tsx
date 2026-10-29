import { Route, Routes } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { ExercisesPage } from './pages/ExercisesPage'
import { LandingPage } from './pages/LandingPage/LandingPage'

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<LandingPage />} />
        <Route path="login" element={<PlaceholderPage title="Welcome back" description="Log in to continue your progress." />} />
        <Route path="register" element={<PlaceholderPage title="Create account" description="Start your transformation right now." />} />
        <Route path="exercises" element={<ExercisesPage />} />
        <Route
          path="exercises/:id"
          element={<PlaceholderPage title="Exercise details" description="Form, muscles and equipment." />}
        />
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
