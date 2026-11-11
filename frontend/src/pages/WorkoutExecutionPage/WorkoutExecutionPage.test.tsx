import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { WeightUnit } from '../../api/auth'
import { finishSession, getLastSession, getTargets, saveExerciseSets, startSession } from '../../api/sessions'
import { getSplit } from '../../api/split'
import { getWorkout, getWorkoutSummaries, type Workout, type WorkoutSummary } from '../../api/workouts'
import { AuthContext, type AuthApi } from '../../auth/AuthContext'
import { ToastProvider } from '../../components/ui/Toast'
import { WorkoutExecutionPage } from './WorkoutExecutionPage'

vi.mock('../../api/sessions')
vi.mock('../../api/split')
vi.mock('../../api/workouts')

const push: WorkoutSummary = { id: 'w1', name: 'Push day', exerciseCount: 2, mainMuscles: ['Chest'] }
const legs: WorkoutSummary = { id: 'w2', name: 'Leg day', exerciseCount: 1, mainMuscles: ['Quads'] }

function exercise(id: string, name: string) {
  return { id, name, mainMuscle: 'Chest', subMuscles: null, equipment: null, difficulty: null, description: null, youtubeLink: null }
}

const workout: Workout = {
  id: 'w1',
  name: 'Push day',
  exercises: [
    { order: 1, targetSets: 2, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: exercise('e1', 'Bench Press') },
    { order: 2, targetSets: 0, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: exercise('e2', 'Cable Fly') },
  ],
}

function authValue(weightUnit: WeightUnit): AuthApi {
  return {
    status: 'authenticated',
    sessionExpired: false,
    user: { firstName: 'Rafael', lastName: null, email: 'r@example.com', weightUnit, defaultRestTimeSeconds: 60 },
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    updateSettings: vi.fn(),
  }
}

function todayDays(assigned: WorkoutSummary | null) {
  const jsDay = new Date().getDay()
  const today = jsDay === 0 ? 7 : jsDay
  return [1, 2, 3, 4, 5, 6, 7].map((day) => ({ day, workout: day === today ? assigned : null }))
}

function renderPage(unit: WeightUnit = WeightUnit.Kilograms) {
  return render(
    <MemoryRouter initialEntries={['/workout-execution']}>
      <AuthContext.Provider value={authValue(unit)}>
        <ToastProvider>
          <Routes>
            <Route path="workout-execution" element={<WorkoutExecutionPage />} />
            <Route path="/" element={<h1>Home</h1>} />
            <Route path="/split" element={<h1>Split</h1>} />
            <Route path="/exercises/:id" element={<h1>Exercise details</h1>} />
          </Routes>
        </ToastProvider>
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

async function fillSet(user: ReturnType<typeof userEvent.setup>, set: number, weight: string, reps: string) {
  await user.type(screen.getByLabelText(`Set ${set} weight`), weight)
  await user.type(screen.getByLabelText(`Set ${set} reps`), reps)
  await user.click(screen.getByRole('button', { name: `Complete set ${set}` }))
}

async function endWorkout(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('button', { name: 'End' }))
  const dialog = await screen.findByRole('dialog', { name: 'End this workout?' })
  await user.click(within(dialog).getByRole('button', { name: 'End workout' }))
}

async function startToday(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: 'Start workout' }))
  await screen.findByText('Exercise 1 of 2')
}

describe('WorkoutExecutionPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  beforeEach(() => {
    vi.mocked(getSplit).mockResolvedValue(todayDays(push))
    vi.mocked(getTargets).mockResolvedValue([])
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [push, legs], page: 1, pageSize: 100, totalCount: 2 })
    vi.mocked(getWorkout).mockResolvedValue(workout)
    vi.mocked(getLastSession).mockResolvedValue({
      sessionId: 's0',
      startedAt: null,
      durationSeconds: null,
      exercises: [{ exerciseId: 'e1', sets: [{ setNumber: 1, reps: 8, weightKg: 100 }] }],
    })
    vi.mocked(startSession).mockResolvedValue({ id: 's1' })
    vi.mocked(saveExerciseSets).mockResolvedValue(undefined)
    vi.mocked(finishSession).mockResolvedValue(undefined)
  })

  afterEach(async () => {
    cleanup()
    await new Promise((resolve) => setTimeout(resolve, 10))
    vi.resetAllMocks()
    vi.useRealTimers()
  })

  it('shows today\'s workout from the split', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: 'Push day' })).toBeInTheDocument()
    expect(screen.queryByText(/exercises planned/)).not.toBeInTheDocument()
  })

  it('shows the rest day state and links to the split', async () => {
    vi.mocked(getSplit).mockResolvedValue(todayDays(null))
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: 'Rest day' })).toBeInTheDocument()
    await user.click(screen.getByRole('link', { name: 'View split' }))

    expect(await screen.findByRole('heading', { name: 'Split' })).toBeInTheDocument()
  })

  it('picks a workout manually on a rest day', async () => {
    vi.mocked(getSplit).mockResolvedValue(todayDays(null))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Start a workout' }))
    const dialog = await screen.findByRole('dialog', { name: 'Select workout' })
    await user.click(within(dialog).getByRole('button', { name: /Leg day/ }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Leg day' })).toBeInTheDocument()
  })

  it('switches the planned workout and marks the active one', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Switch workout' }))
    const dialog = await screen.findByRole('dialog', { name: 'Select workout' })
    expect(within(dialog).getByRole('button', { name: /Push day/ })).toHaveAttribute('aria-current', 'true')
    await user.click(within(dialog).getByRole('button', { name: /Leg day/ }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Leg day' })).toBeInTheDocument()
  })

  it('tells the user when there are no workouts to pick', async () => {
    vi.mocked(getSplit).mockResolvedValue(todayDays(null))
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0 })
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Start a workout' }))

    expect(await screen.findByText('You have no workouts yet. Create one first.')).toBeInTheDocument()
  })

  it('offers a retry when loading fails', async () => {
    vi.mocked(getSplit).mockRejectedValueOnce(new Error('offline'))
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: 'Could not load your workout' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Push day' })).toBeInTheDocument()
  })

  it('reports a failure to start', async () => {
    vi.mocked(startSession).mockRejectedValue(new Error('boom'))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Start workout' }))

    expect(await screen.findByText('Could not start the workout. Try again.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Push day' })).toBeInTheDocument()
  })

  it('starts the session and shows last session values as placeholders', async () => {
    const user = userEvent.setup()
    renderPage()

    await startToday(user)

    expect(startSession).toHaveBeenCalledWith('w1')
    expect(screen.getByRole('link', { name: 'Bench Press' })).toHaveAttribute('href', '/exercises/e1')
    expect(screen.getByLabelText('Set 1 weight')).toHaveAttribute('placeholder', '100')
    expect(screen.getByLabelText('Set 1 reps')).toHaveAttribute('placeholder', '8')
    expect(screen.getByLabelText('Set 2 weight')).toHaveAttribute('placeholder', '-')
    expect(screen.getByRole('progressbar', { name: 'Workout progress' })).toHaveAttribute('aria-valuenow', '1')
    expect(screen.getByLabelText('Elapsed time')).toHaveTextContent('00:00')
  })

  it('shows weights in pounds when the user prefers them', async () => {
    const user = userEvent.setup()
    renderPage(WeightUnit.Pounds)

    await startToday(user)

    expect(screen.getByLabelText('Set 1 weight')).toHaveAttribute('placeholder', '220.5')
    await fillSet(user, 1, '220', '5')
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await screen.findByText('Exercise 2 of 2')

    expect(saveExerciseSets).toHaveBeenCalledWith('s1', 'e1', [{ reps: 5, weightKg: 99.79 }])
  })

  it('only enables the check once both fields are valid and can undo a set', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    const check = screen.getByRole('button', { name: 'Complete set 1' })
    expect(check).toBeDisabled()
    await user.type(screen.getByLabelText('Set 1 weight'), '60')
    expect(check).toBeDisabled()
    await user.type(screen.getByLabelText('Set 1 reps'), '8')
    expect(check).toBeEnabled()

    await user.click(check)
    expect(screen.getByRole('button', { name: 'Undo set 1' })).toHaveAttribute('aria-pressed', 'true')
    await user.click(screen.getByRole('button', { name: 'Undo set 1' }))
    expect(screen.getByRole('button', { name: 'Complete set 1' })).toBeEnabled()
  })

  it('runs the rest timer after a set with extend, reduce and skip', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    await fillSet(user, 1, '60', '8')

    const timer = await screen.findByRole('timer', { name: 'Rest timer' })
    expect(timer).toHaveTextContent('01:00')
    await user.click(within(timer).getByRole('button', { name: '+30s' }))
    expect(timer).toHaveTextContent('01:30')
    await user.click(within(timer).getByRole('button', { name: '−10s' }))
    expect(timer).toHaveTextContent('01:20')
    await user.click(within(timer).getByRole('button', { name: 'Skip' }))
    await waitFor(() => expect(screen.queryByRole('timer')).not.toBeInTheDocument())
  })

  it('ticks the stopwatch and the rest countdown', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')

    act(() => {
      vi.advanceTimersByTime(5000)
    })

    expect(screen.getByLabelText('Elapsed time')).toHaveTextContent(/00:0[5-9]/)
    expect(screen.getByRole('timer')).toHaveTextContent(/00:5[0-5]/)
  })

  it('saves completed sets and moves between exercises', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')

    await user.click(screen.getByRole('button', { name: 'Next' }))

    await screen.findByText('Exercise 2 of 2')
    expect(saveExerciseSets).toHaveBeenCalledWith('s1', 'e1', [{ reps: 8, weightKg: 60 }])
    expect(screen.getAllByLabelText(/Set \d weight/)).toHaveLength(3)
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '2')

    await user.click(screen.getByRole('button', { name: 'Previous' }))

    await screen.findByText('Exercise 1 of 2')
    expect(screen.getByLabelText('Set 1 weight')).toHaveValue(60)
    expect(saveExerciseSets).toHaveBeenCalledTimes(2)
  })

  it('saves a set as soon as it is ticked and again when it is undone', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    await fillSet(user, 1, '60', '8')
    await waitFor(() => expect(saveExerciseSets).toHaveBeenLastCalledWith('s1', 'e1', [{ reps: 8, weightKg: 60 }]))

    await user.click(screen.getByRole('button', { name: 'Undo set 1' }))
    await waitFor(() => expect(saveExerciseSets).toHaveBeenLastCalledWith('s1', 'e1', []))
    expect(saveExerciseSets).toHaveBeenCalledTimes(2)
  })

  it('does not finish an empty session when the running workout is left', async () => {
    const user = userEvent.setup()
    const view = renderPage()
    await startToday(user)

    view.unmount()
    await new Promise((resolve) => setTimeout(resolve, 20))

    expect(finishSession).not.toHaveBeenCalled()
  })

  it('finishes the session only after pending set saves complete', async () => {
    const order: string[] = []
    let release: () => void = () => undefined
    vi.mocked(saveExerciseSets).mockImplementation(
      () =>
        new Promise<void>((resolve) => {
          release = () => {
            order.push('saved')
            resolve()
          }
        }),
    )
    vi.mocked(finishSession).mockImplementation(async () => {
      order.push('finished')
    })
    const user = userEvent.setup()
    const view = renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')

    view.unmount()
    await new Promise((resolve) => setTimeout(resolve, 20))
    expect(finishSession).not.toHaveBeenCalled()
    release()

    await waitFor(() => expect(finishSession).toHaveBeenCalledWith('s1'))
    expect(order).toEqual(['saved', 'finished'])
  })

  it('finishes the session when the running workout is left without ending it', async () => {
    const user = userEvent.setup()
    const view = renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')

    view.unmount()

    await waitFor(() => expect(finishSession).toHaveBeenCalledWith('s1'))
    expect(finishSession).toHaveBeenCalledTimes(1)
  })

  it('does not finish the session twice when it is ended normally', async () => {
    const user = userEvent.setup()
    const view = renderPage()
    await startToday(user)
    await endWorkout(user)
    await screen.findByRole('heading', { level: 1, name: 'Workout finished' })

    view.unmount()
    await new Promise((resolve) => setTimeout(resolve, 20))

    expect(finishSession).toHaveBeenCalledTimes(1)
  })

  it('warns before the page is closed while a workout is running', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    const event = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(true)
  })

  it('stays on the exercise when saving fails', async () => {
    vi.mocked(saveExerciseSets).mockRejectedValue(new Error('boom'))
    const user = userEvent.setup()
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')

    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findAllByText('Could not save your sets. Try again.')).not.toHaveLength(0)
    expect(screen.getByText('Exercise 1 of 2')).toBeInTheDocument()
  })

  it('advances automatically after the last set of an exercise', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')
    await fillSet(user, 2, '60', '7')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600)
    })

    expect(screen.getByText('Exercise 2 of 2')).toBeInTheDocument()
    expect(saveExerciseSets).toHaveBeenCalledWith('s1', 'e1', [
      { reps: 8, weightKg: 60 },
      { reps: 7, weightKg: 60 },
    ])
  })

  it('reports a failure to save during auto advance', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.mocked(saveExerciseSets).mockRejectedValue(new Error('boom'))
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')
    await fillSet(user, 2, '60', '7')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600)
    })

    expect(screen.getByText('Could not save your sets. Try again.')).toBeInTheDocument()
    expect(screen.getByText('Exercise 1 of 2')).toBeInTheDocument()
  })

  it('does not auto advance past the last exercise', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await startToday(user)
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await screen.findByText('Exercise 2 of 2')
    for (const set of [1, 2, 3]) {
      await fillSet(user, set, '20', '12')
    }

    await act(async () => {
      await vi.advanceTimersByTimeAsync(600)
    })

    expect(screen.getByText('Exercise 2 of 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Finish/ })).toBeInTheDocument()
  })

  it('finishes the workout from the last exercise', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await screen.findByText('Exercise 2 of 2')
    await fillSet(user, 1, '20', '12')

    await user.click(screen.getByRole('button', { name: /Finish/ }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Workout finished' })).toBeInTheDocument()
    expect(screen.getByText('Push day')).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'Best set per exercise' })).toHaveTextContent('Cable Fly20 kg × 12')
    expect(saveExerciseSets).toHaveBeenCalledWith('s1', 'e2', [{ reps: 12, weightKg: 20 }])
    expect(finishSession).toHaveBeenCalledWith('s1')
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Done' }))
    expect(await screen.findByRole('heading', { name: 'Home' })).toBeInTheDocument()
  })

  it('stops the workout early', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')

    await endWorkout(user)

    expect(await screen.findByRole('heading', { level: 1, name: 'Workout finished' })).toBeInTheDocument()
    expect(saveExerciseSets).toHaveBeenCalledWith('s1', 'e1', [{ reps: 8, weightKg: 60 }])
  })

  it('stays on the workout when finishing fails', async () => {
    vi.mocked(finishSession).mockRejectedValue(new Error('boom'))
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    await endWorkout(user)

    expect(await screen.findByText('Could not finish the workout. Try again.')).toBeInTheDocument()
    expect(screen.getByText('Exercise 1 of 2')).toBeInTheDocument()
  })

  it('only lets sets be completed in order so saved set numbers match', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)
    await user.type(screen.getByLabelText('Set 2 weight'), '60')
    await user.type(screen.getByLabelText('Set 2 reps'), '8')

    expect(screen.getByRole('button', { name: 'Complete set 2' })).toBeDisabled()

    await fillSet(user, 1, '60', '8')
    expect(screen.getByRole('button', { name: 'Complete set 2' })).toBeEnabled()
    await user.click(screen.getByRole('button', { name: 'Complete set 2' }))

    expect(screen.getByRole('button', { name: 'Undo set 1' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Undo set 2' })).toBeEnabled()
  })

  it('cancels the pending auto advance when the last set is undone', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')
    await fillSet(user, 2, '60', '7')

    await user.click(screen.getByRole('button', { name: 'Undo set 2' }))
    await act(async () => {
      await vi.advanceTimersByTimeAsync(600)
    })

    expect(screen.getByText('Exercise 1 of 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Complete set 2' })).toBeEnabled()
    expect(saveExerciseSets).toHaveBeenLastCalledWith('s1', 'e1', [{ reps: 8, weightKg: 60 }])
  })

  it('asks before ending and keeps going when asked to', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)
    await fillSet(user, 1, '60', '8')

    await user.click(screen.getByRole('button', { name: 'End' }))

    const dialog = await screen.findByRole('dialog', { name: 'End this workout?' })
    expect(dialog).toHaveTextContent('1 of 2 exercises started')
    await user.click(within(dialog).getByRole('button', { name: 'Keep going' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(finishSession).not.toHaveBeenCalled()
    expect(screen.getByText('Exercise 1 of 2')).toBeInTheDocument()
  })

  it('asks before leaving through the back arrow', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)

    await user.click(screen.getByRole('button', { name: 'Leave workout' }))

    expect(await screen.findByRole('dialog', { name: 'End this workout?' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Home' })).not.toBeInTheDocument()
  })

  it('links the finish summary to progression', async () => {
    const user = userEvent.setup()
    renderPage()
    await startToday(user)
    await endWorkout(user)
    await screen.findByRole('heading', { level: 1, name: 'Workout finished' })

    expect(screen.getByRole('link', { name: 'View progression' })).toHaveAttribute('href', '/progress')
  })

  it('works without a previous session', async () => {
    vi.mocked(getLastSession).mockResolvedValue({ sessionId: null, startedAt: null, durationSeconds: null, exercises: [] })
    const user = userEvent.setup()
    renderPage()

    await startToday(user)

    expect(screen.getByLabelText('Set 1 weight')).toHaveAttribute('placeholder', '-')
  })
})

describe('WorkoutExecutionPage picker', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  beforeEach(() => {
    vi.mocked(getSplit).mockResolvedValue(todayDays(push))
    vi.mocked(getTargets).mockResolvedValue([])
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [push, legs], page: 1, pageSize: 100, totalCount: 2 })
  })

  afterEach(() => {
    vi.resetAllMocks()
  })

  it('closes the picker without changing the workout', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Switch workout' }))
    await screen.findByRole('dialog', { name: 'Select workout' })
    await user.keyboard('{Escape}')

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.getByRole('heading', { level: 1, name: 'Push day' })).toBeInTheDocument()
  })
})
