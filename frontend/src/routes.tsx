import { Route, Routes } from 'react-router-dom'
import { AppShell } from './components/layout/AppShell'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { WorkoutsPage } from './pages/WorkoutsPage/WorkoutsPage'
import { SplitPage } from './pages/SplitPage/SplitPage'
import { ExercisesPage } from './pages/ExercisesPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage/LoginPage'
import { RegisterPage } from './pages/RegisterPage/RegisterPage'

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<HomePage />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="exercises" element={<ExercisesPage />} />
        <Route
          path="exercises/:id"
          element={<PlaceholderPage title="Exercise details" description="Form, muscles and equipment." />}
        />
        <Route path="workouts" element={<WorkoutsPage />} />
        <Route path="split" element={<SplitPage />} />
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
