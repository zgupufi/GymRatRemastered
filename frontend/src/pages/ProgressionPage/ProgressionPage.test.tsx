import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { searchExercises, type Exercise } from '../../api/exercises'
import { getExerciseProgress, type ProgressPoint } from '../../api/progress'
import { getSplit } from '../../api/split'
import { getWorkout, type Workout } from '../../api/workouts'
import { ProgressionPage } from './ProgressionPage'

vi.mock('../../api/exercises')
vi.mock('../../api/progress')
vi.mock('../../api/split')
vi.mock('../../api/workouts')
vi.mock('../../hooks/useDebouncedValue', () => ({ useDebouncedValue: <T,>(value: T) => value }))

const bench: Exercise = {
  id: 'e1',
  name: 'Bench Press',
  mainMuscle: 'Chest',
  subMuscles: null,
  equipment: null,
  difficulty: null,
  description: null,
  youtubeLink: null,
}
const fly: Exercise = { ...bench, id: 'e2', name: 'Cable Fly' }

const workout: Workout = {
  id: 'w1',
  name: 'Push day',
  exercises: [
    { order: 1, targetSets: 3, exercise: bench },
    { order: 2, targetSets: 3, exercise: fly },
  ],
}

const twoSessions: ProgressPoint[] = [
  { date: '2026-10-10', bestSet: { reps: 6, weightKg: 80 }, estimatedOneRepMax: 96 },
  { date: '2026-11-07', bestSet: { reps: 6, weightKg: 90 }, estimatedOneRepMax: 108 },
]

function renderPage() {
  return render(
    <MemoryRouter>
      <ProgressionPage />
    </MemoryRouter>,
  )
}

const WEDNESDAY = new Date(2026, 10, 4, 12)
const SUNDAY = new Date(2026, 10, 8, 12)

function splitWithToday(workoutId: string | null, today = 3) {
  return Array.from({ length: 7 }, (_, index) => ({
    day: index + 1,
    workout:
      workoutId && index + 1 === today ? { id: workoutId, name: 'Push day', exerciseCount: 2, mainMuscles: ['Chest'] } : null,
  }))
}

describe('ProgressionPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'], now: WEDNESDAY })
    vi.mocked(getSplit).mockResolvedValue(splitWithToday('w1'))
    vi.mocked(getWorkout).mockResolvedValue(workout)
    vi.mocked(searchExercises).mockResolvedValue({ items: [bench, fly], page: 1, pageSize: 6, totalCount: 2 })
    vi.mocked(getExerciseProgress).mockResolvedValue(twoSessions)
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.resetAllMocks()
  })

  it('treats Sunday as the seventh split day', async () => {
    vi.setSystemTime(SUNDAY)
    vi.mocked(getSplit).mockResolvedValue(splitWithToday('w1', 7))

    renderPage()

    expect(await screen.findByRole('button', { name: 'Bench Press' })).toBeInTheDocument()
  })

  it("suggests exercises from today's workout and charts the chosen one", async () => {
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'Bench Press' }))

    expect(getExerciseProgress).toHaveBeenCalledWith('e1', expect.any(AbortSignal))
    expect(await screen.findByRole('img', { name: /Estimated one rep max of Bench Press/ })).toBeInTheDocument()
    expect(screen.getByText('108 kg')).toBeInTheDocument()
    expect(screen.getByText('+12 kg')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Bench Press' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows no suggestions when nothing is planned for today', async () => {
    vi.mocked(getSplit).mockResolvedValue(splitWithToday(null))

    renderPage()

    await waitFor(() => expect(getSplit).toHaveBeenCalled())
    expect(screen.queryByText(/today.s workout/i)).not.toBeInTheDocument()
    expect(getWorkout).not.toHaveBeenCalled()
  })

  it("ignores a failing split lookup", async () => {
    vi.mocked(getSplit).mockRejectedValue(new Error('down'))

    renderPage()

    await waitFor(() => expect(getSplit).toHaveBeenCalled())
    expect(screen.queryByText(/today.s workout/i)).not.toBeInTheDocument()
  })

  it('searches by name and picks a suggestion', async () => {
    renderPage()

    await userEvent.type(screen.getByLabelText('Exercise'), 'fly')

    expect(searchExercises).toHaveBeenLastCalledWith({ term: 'fly', pageSize: 6 }, expect.any(AbortSignal))
    await userEvent.click(await screen.findByRole('button', { name: /Cable Fly\s*Chest/ }))

    expect(screen.getByLabelText('Exercise')).toHaveValue('Cable Fly')
    expect(await screen.findByRole('img', { name: /Cable Fly/ })).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Matching exercises' })).not.toBeInTheDocument()
  })

  it('reports when no exercise matches', async () => {
    vi.mocked(searchExercises).mockResolvedValue({ items: [], page: 1, pageSize: 6, totalCount: 0 })
    renderPage()

    await userEvent.type(screen.getByLabelText('Exercise'), 'zzz')

    expect(await screen.findByText('No exercises found.')).toBeInTheDocument()
  })

  it('reports a failing search', async () => {
    vi.mocked(searchExercises).mockRejectedValue(new Error('down'))
    renderPage()

    await userEvent.type(screen.getByLabelText('Exercise'), 'a')

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not search exercises')
  })

  it('clears the selection when the search text changes', async () => {
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Bench Press' }))
    await screen.findByRole('img', { name: /Bench Press/ })

    await userEvent.type(screen.getByLabelText('Exercise'), 's')

    expect(screen.queryByRole('img', { name: /Bench Press/ })).not.toBeInTheDocument()
    expect(await screen.findByRole('list', { name: 'Matching exercises' })).toBeInTheDocument()
  })

  it('explains a single session', async () => {
    vi.mocked(getExerciseProgress).mockResolvedValue([twoSessions[0]])
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'Bench Press' }))

    expect(await screen.findByText('Not enough data')).toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('explains when there is no data', async () => {
    vi.mocked(getExerciseProgress).mockResolvedValue([])
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'Bench Press' }))

    expect(await screen.findByText('No data yet')).toBeInTheDocument()
  })

  it('shows a negative change without a plus sign', async () => {
    vi.mocked(getExerciseProgress).mockResolvedValue([twoSessions[1], twoSessions[0]])
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'Bench Press' }))

    expect(await screen.findByText('-12 kg')).toBeInTheDocument()
  })

  it('offers a retry when the progress fails to load', async () => {
    vi.mocked(getExerciseProgress).mockRejectedValueOnce(new Error('down'))
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Bench Press' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('img', { name: /Bench Press/ })).toBeInTheDocument()
    expect(getExerciseProgress).toHaveBeenCalledTimes(2)
  })
})
