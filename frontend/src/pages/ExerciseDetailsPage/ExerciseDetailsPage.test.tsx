import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { getExercise, type Exercise } from '../../api/exercises'
import { getWorkoutSummaries } from '../../api/workouts'
import { ToastProvider } from '../../components/ui/Toast'
import { ExerciseDetailsPage } from './ExerciseDetailsPage'

vi.mock('../../api/exercises')
vi.mock('../../api/workouts')

const benchPress: Exercise = {
  id: 'e1',
  name: 'Bench Press',
  mainMuscle: 'Chest',
  subMuscles: 'Triceps',
  equipment: 'Barbell',
  difficulty: 'Intermediate',
  description: 'Keep your feet planted.',
  youtubeLink: 'https://www.youtube.com/watch?v=abc123',
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/exercises/e1']}>
      <ToastProvider>
        <Routes>
          <Route path="exercises/:id" element={<ExerciseDetailsPage />} />
        </Routes>
      </ToastProvider>
    </MemoryRouter>,
  )
}

describe('ExerciseDetailsPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterEach(() => {
    vi.resetAllMocks()
  })

  it('shows the exercise with its video, facts and steps', async () => {
    vi.mocked(getExercise).mockResolvedValue(benchPress)

    renderPage()

    expect(screen.getByText('Loading exercise…')).toBeInTheDocument()
    expect(await screen.findByRole('heading', { level: 1, name: 'Bench Press' })).toBeInTheDocument()
    expect(screen.getByText('Intermediate · Chest')).toBeInTheDocument()
    expect(screen.getByTitle('Bench Press demo')).toHaveAttribute('src', 'https://www.youtube.com/embed/abc123')
    expect(screen.getByText('Triceps')).toBeInTheDocument()
    expect(screen.getByText('Barbell')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Setup' })).toBeInTheDocument()
    expect(screen.getByText(/Keep your feet planted\./)).toBeInTheDocument()
  })

  it('falls back when there is no video, secondary muscle or equipment', async () => {
    vi.mocked(getExercise).mockResolvedValue({ ...benchPress, youtubeLink: null, subMuscles: null, equipment: null, difficulty: null })

    renderPage()

    expect(await screen.findByText('Demo video unavailable')).toBeInTheDocument()
    expect(screen.getByText('Bodyweight')).toBeInTheDocument()
    expect(screen.getByText('—')).toBeInTheDocument()
    expect(screen.getByText('Chest', { selector: '.exercise-details__kicker' })).toBeInTheDocument()
  })

  it('shows a not-found message', async () => {
    vi.mocked(getExercise).mockRejectedValue(new ApiError(404, 'missing'))

    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: 'Exercise not found.' })).toBeInTheDocument()
  })

  it('shows an error for other failures', async () => {
    vi.mocked(getExercise).mockRejectedValue(new ApiError(500, 'boom'))

    renderPage()

    expect(await screen.findByRole('heading', { level: 1, name: 'Could not load exercise' })).toBeInTheDocument()
    expect(screen.getByText(/The server ran into an issue/)).toBeInTheDocument()
  })

  it('opens the workout chooser', async () => {
    vi.mocked(getExercise).mockResolvedValue(benchPress)
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0 })

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: '+ Add to workout' }))

    expect(await screen.findByRole('dialog', { name: 'Choose workout' })).toBeInTheDocument()
    expect(await screen.findByText('No workouts available. Create one in Workouts page!')).toBeInTheDocument()
  })

  it('ignores an aborted request after unmounting', async () => {
    let rejectRequest: (reason: unknown) => void = () => {}
    vi.mocked(getExercise).mockReturnValue(
      new Promise<Exercise>((_, reject) => {
        rejectRequest = reject
      }),
    )

    const { unmount } = renderPage()
    unmount()
    rejectRequest(new DOMException('aborted', 'AbortError'))
    await Promise.resolve()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
