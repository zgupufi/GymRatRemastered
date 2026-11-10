import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { WeightUnit } from '../../api/auth'
import { finishSession, getLastSession, getTargets, saveExerciseSets, startSession, type ExerciseTarget } from '../../api/sessions'
import { getSplit } from '../../api/split'
import { getWorkout, getWorkoutSummaries, type Workout, type WorkoutSummary } from '../../api/workouts'
import { AuthContext, type AuthApi } from '../../auth/AuthContext'
import { ToastProvider } from '../../components/ui/Toast'
import { WorkoutExecutionPage } from './WorkoutExecutionPage'

vi.mock('../../api/sessions')
vi.mock('../../api/split')
vi.mock('../../api/workouts')

const push: WorkoutSummary = { id: 'w1', name: 'Push day', exerciseCount: 2, mainMuscles: ['Chest'] }

function exercise(id: string, name: string) {
  return { id, name, mainMuscle: 'Chest', subMuscles: null, equipment: null, difficulty: null, description: null, youtubeLink: null }
}

const workout: Workout = {
  id: 'w1',
  name: 'Push day',
  exercises: [
    { order: 1, targetSets: 2, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: exercise('e1', 'Bench Press') },
    { order: 2, targetSets: 1, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: exercise('e2', 'Cable Fly') },
  ],
}

const benchTarget: ExerciseTarget = {
  order: 1,
  exerciseId: 'e1',
  sets: 2,
  repsMin: 8,
  repsMax: 12,
  weightKg: 62.5,
  reps: 8,
  reason: 'IncreaseWeight',
  lastWeightKg: 60,
  lastReps: [12, 12],
}

const authValue: AuthApi = {
  status: 'authenticated',
  user: { firstName: 'Rafael', lastName: null, email: 'r@example.com', weightUnit: WeightUnit.Kilograms, defaultRestTimeSeconds: 60 },
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
  updateSettings: vi.fn(),
}

function todayDays() {
  const jsDay = new Date().getDay()
  const today = jsDay === 0 ? 7 : jsDay
  return [1, 2, 3, 4, 5, 6, 7].map((day) => ({ day, workout: day === today ? push : null }))
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/workout-execution']}>
      <AuthContext.Provider value={authValue}>
        <ToastProvider>
          <Routes>
            <Route path="workout-execution" element={<WorkoutExecutionPage />} />
          </Routes>
        </ToastProvider>
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

async function startToday(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: 'Start workout' }))
  await screen.findByText('Exercise 1 of 2')
}

describe('WorkoutExecutionPage targets and effort', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  beforeEach(() => {
    vi.mocked(getSplit).mockResolvedValue(todayDays())
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [push], page: 1, pageSize: 100, totalCount: 1 })
    vi.mocked(getWorkout).mockResolvedValue(workout)
    vi.mocked(getLastSession).mockResolvedValue({ sessionId: null, startedAt: null, durationSeconds: null, exercises: [] })
    vi.mocked(getTargets).mockResolvedValue([benchTarget])
    vi.mocked(startSession).mockResolvedValue({ id: 's1' })
    vi.mocked(saveExerciseSets).mockResolvedValue(undefined)
    vi.mocked(finishSession).mockResolvedValue(undefined)
  })

  afterEach(() => {
    vi.resetAllMocks()
  })

  it('shows the target, fills it, records effort and an extra set, and sends the served target', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    const banner = screen.getByRole('region', { name: "Today's target" })
    expect(within(banner).getByText('62.5 kg × 8')).toBeInTheDocument()
    expect(screen.getByLabelText('Set 1 weight')).toHaveAttribute('placeholder', '62.5')

    await user.click(within(banner).getByRole('button', { name: 'Use target' }))
    expect(screen.getByLabelText('Set 2 weight')).toHaveValue(62.5)
    expect(screen.getByLabelText('Set 2 reps')).toHaveValue(8)

    await user.click(screen.getByRole('button', { name: 'Complete set 1' }))
    await user.click(within(screen.getByRole('group', { name: 'Effort for set 1' })).getByRole('button', { name: 'Hard' }))
    await user.click(screen.getByRole('button', { name: 'Add set' }))
    expect(screen.getByLabelText('Set 3 weight')).toHaveValue(62.5)
    await user.click(screen.getByRole('button', { name: 'Remove last' }))
    expect(screen.queryByLabelText('Set 3 weight')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Add set' }))

    await user.click(screen.getByRole('button', { name: 'Next' }))

    await screen.findByText('Exercise 2 of 2')
    expect(saveExerciseSets).toHaveBeenCalledWith('s1', 'e1', [{ reps: 8, weightKg: 62.5, repsInReserve: 1 }], {
      weightKg: 62.5,
      reps: 8,
      reason: 'IncreaseWeight',
    })
    expect(screen.queryByRole('region', { name: "Today's target" })).not.toBeInTheDocument()
  })

  it('toggles an effort off again and disables remove once the last set is done', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    await user.type(screen.getByLabelText('Set 1 weight'), '60')
    await user.type(screen.getByLabelText('Set 1 reps'), '8')
    await user.click(screen.getByRole('button', { name: 'Complete set 1' }))
    const effort = within(screen.getByRole('group', { name: 'Effort for set 1' }))
    await user.click(effort.getByRole('button', { name: 'Max' }))
    expect(effort.getByRole('button', { name: 'Max' })).toHaveAttribute('aria-pressed', 'true')
    await user.click(effort.getByRole('button', { name: 'Max' }))
    expect(effort.getByRole('button', { name: 'Max' })).toHaveAttribute('aria-pressed', 'false')

    await user.click(screen.getByRole('button', { name: 'Remove last' }))
    expect(screen.getByRole('button', { name: 'Remove last' })).toBeDisabled()
  })

  it('starts without targets when they cannot be loaded', async () => {
    vi.mocked(getTargets).mockRejectedValue(new Error('boom'))
    const user = userEvent.setup()
    renderPage()

    await startToday(user)

    expect(screen.queryByRole('region', { name: "Today's target" })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Set 1 weight')).toHaveAttribute('placeholder', '-')
  })
})
