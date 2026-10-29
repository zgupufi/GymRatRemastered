import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { searchExercises, type Exercise } from '../../api/exercises'
import {
  createWorkout,
  deleteWorkout,
  getWorkout,
  getWorkoutSummaries,
  renameWorkout,
  replaceWorkoutExercises,
  type WorkoutSummary,
} from '../../api/workouts'
import { ToastProvider } from '../../components/ui/Toast'
import { WorkoutsPage } from './WorkoutsPage'

vi.mock('../../api/workouts')
vi.mock('../../api/exercises')

const bench: Exercise = {
  id: 'e1',
  name: 'Bench Press',
  mainMuscle: 'Chest',
  subMuscles: 'Triceps',
  equipment: 'Barbell',
  difficulty: null,
  description: null,
  youtubeLink: null,
}
const row: Exercise = { ...bench, id: 'e2', name: 'Barbell Row', mainMuscle: 'Back', subMuscles: 'Lats' }
const squat: Exercise = { ...bench, id: 'e3', name: 'Squat', mainMuscle: 'Quads', subMuscles: null, equipment: null }

const push: WorkoutSummary = { id: 'w1', name: 'Push day', exerciseCount: 2, mainMuscles: ['Chest', 'Shoulders'] }
const pull: WorkoutSummary = { id: 'w2', name: 'Pull day', exerciseCount: 1, mainMuscles: ['Back'] }

function page(items: WorkoutSummary[], overrides: Partial<{ page: number; totalCount: number }> = {}) {
  return { items, page: 1, pageSize: 10, totalCount: items.length, ...overrides }
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ToastProvider>
        <WorkoutsPage />
      </ToastProvider>
    </MemoryRouter>,
  )
}

describe('WorkoutsPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
    Element.prototype.scrollIntoView = vi.fn()
  })

  beforeEach(() => {
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([push, pull]))
    vi.mocked(getWorkout).mockResolvedValue({
      id: 'w1',
      name: 'Push day',
      exercises: [
        { order: 2, targetSets: 3, exercise: row },
        { order: 1, targetSets: 4, exercise: bench },
      ],
    })
    vi.mocked(searchExercises).mockResolvedValue({ items: [bench, row, squat], page: 1, pageSize: 20, totalCount: 3 })
  })

  afterEach(() => {
    vi.resetAllMocks()
  })

  it('lists workouts with their muscles and exercise count', async () => {
    renderPage()

    expect(await screen.findByText('Push day')).toBeInTheDocument()
    expect(screen.getByText('Shoulders')).toBeInTheDocument()
    expect(screen.getByText('2 exercises')).toBeInTheDocument()
    expect(screen.getByText('1 exercise')).toBeInTheDocument()
    expect(screen.getByText('Showing 1–2 of 2 workouts')).toBeInTheDocument()
    expect(getWorkoutSummaries).toHaveBeenCalledWith(
      { term: '', sortBy: 'CreatedAt', direction: 'Descending', page: 1, pageSize: 10 },
      expect.any(AbortSignal),
    )
  })

  it('shows the empty state without workouts', async () => {
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([]))

    renderPage()

    expect(await screen.findByText('No workouts')).toBeInTheDocument()
  })

  it('shows an error with a retry', async () => {
    vi.mocked(getWorkoutSummaries).mockRejectedValueOnce(new Error('boom'))

    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Push day')).toBeInTheDocument()
  })

  it('searches by name and shows when nothing matches', async () => {
    renderPage()
    await screen.findByText('Push day')
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([]))

    await userEvent.type(screen.getByRole('textbox', { name: 'Search workouts' }), 'zzz')

    expect(await screen.findByText('No workouts match “zzz”')).toBeInTheDocument()
    expect(getWorkoutSummaries).toHaveBeenLastCalledWith(expect.objectContaining({ term: 'zzz', page: 1 }), expect.any(AbortSignal))
  })

  it('sorts by name', async () => {
    renderPage()
    await screen.findByText('Push day')

    await userEvent.click(screen.getByRole('radio', { name: 'Name A–Z' }))

    await waitFor(() =>
      expect(getWorkoutSummaries).toHaveBeenLastCalledWith(
        expect.objectContaining({ sortBy: 'Name', direction: 'Ascending' }),
        expect.any(AbortSignal),
      ),
    )
  })

  it('pages forward and back', async () => {
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([push], { totalCount: 12 }))
    renderPage()
    await screen.findByText('Push day')

    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([push], { page: 2, totalCount: 12 }))
    await userEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => expect(getWorkoutSummaries).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }), expect.any(AbortSignal)))
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([push], { page: 1, totalCount: 12 }))
    await userEvent.click(await screen.findByRole('button', { name: 'Previous' }))

    await waitFor(() => expect(getWorkoutSummaries).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1 }), expect.any(AbortSignal)))
  })

  it('steps back a page when the current one becomes empty', async () => {
    vi.mocked(getWorkoutSummaries).mockResolvedValueOnce(page([push], { totalCount: 11 }))
    renderPage()
    await screen.findByText('Push day')
    vi.mocked(getWorkoutSummaries).mockResolvedValueOnce(page([], { page: 2, totalCount: 10 }))
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([push], { totalCount: 10 }))

    await userEvent.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => expect(getWorkoutSummaries).toHaveBeenCalledTimes(3))
    expect(getWorkoutSummaries).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1 }), expect.any(AbortSignal))
  })

  it('creates a workout and opens it for renaming', async () => {
    const created: WorkoutSummary = { id: 'w3', name: 'New workout - Oct 29', exerciseCount: 0, mainMuscles: [] }
    vi.mocked(createWorkout).mockResolvedValue({ id: 'w3' })
    vi.mocked(getWorkout).mockResolvedValue({ id: 'w3', name: created.name, exercises: [] })
    renderPage()
    await screen.findByText('Push day')
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([created, push, pull]))

    await userEvent.click(screen.getByRole('button', { name: 'Create workout' }))

    expect(await screen.findByRole('textbox', { name: 'Workout name' })).toHaveValue('New workout - Oct 29')
    expect(createWorkout).toHaveBeenCalledWith(expect.stringMatching(/^New workout - /))
    expect(Element.prototype.scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'center' })
  })

  it('reports a failed creation', async () => {
    vi.mocked(createWorkout).mockRejectedValue(new Error('boom'))
    renderPage()
    await screen.findByText('Push day')

    await userEvent.click(screen.getByRole('button', { name: 'Create workout' }))

    expect(await screen.findByText('Could not create the workout. Try again.')).toBeInTheDocument()
  })

  it('renames a workout with Enter', async () => {
    vi.mocked(renameWorkout).mockResolvedValue()
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Rename Push day' }))

    const input = screen.getByRole('textbox', { name: 'Workout name' })
    await userEvent.clear(input)
    await userEvent.type(input, 'Chest day{Enter}')

    await waitFor(() => expect(renameWorkout).toHaveBeenCalledWith('w1', 'Chest day'))
    expect(screen.queryByRole('textbox', { name: 'Workout name' })).not.toBeInTheDocument()
  })

  it('leaves the name alone when it is unchanged or the rename is cancelled', async () => {
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Rename Push day' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save name' }))
    await userEvent.click(screen.getByRole('button', { name: 'Rename Push day' }))
    await userEvent.type(screen.getByRole('textbox', { name: 'Workout name' }), 'x{Escape}')

    expect(renameWorkout).not.toHaveBeenCalled()
    expect(screen.getByText('Push day')).toBeInTheDocument()
  })

  it('does not rename to an empty name and reports a failed rename', async () => {
    vi.mocked(renameWorkout).mockRejectedValue(new Error('boom'))
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Rename Push day' }))
    const input = screen.getByRole('textbox', { name: 'Workout name' })

    await userEvent.clear(input)
    expect(screen.getByRole('button', { name: 'Save name' })).toBeDisabled()
    await userEvent.type(input, '{Enter}')
    expect(renameWorkout).not.toHaveBeenCalled()
    await userEvent.type(input, 'Legs')
    await userEvent.click(screen.getByRole('button', { name: 'Save name' }))

    expect(await screen.findByText('Could not rename the workout. Try again.')).toBeInTheDocument()
  })

  it('asks for confirmation before deleting', async () => {
    vi.mocked(deleteWorkout).mockResolvedValue()
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Push day' }))

    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(deleteWorkout).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Delete Push day' }))
    await userEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteWorkout).toHaveBeenCalledWith('w1'))
  })

  it('reports a failed deletion', async () => {
    vi.mocked(deleteWorkout).mockRejectedValue(new Error('boom'))
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Delete Push day' }))

    await userEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText('Could not delete the workout. Try again.')).toBeInTheDocument()
  })

  it('loads the exercises in order when a card expands and collapses again', async () => {
    renderPage()
    const toggle = await screen.findByRole('button', { name: 'Push day, 2 exercises' })

    await userEvent.click(toggle)

    const sets = await screen.findAllByRole('spinbutton')
    expect(sets.map((input) => input.getAttribute('aria-label'))).toEqual(['Sets for Bench Press', 'Sets for Barbell Row'])
    expect(sets.map((input) => (input as HTMLInputElement).value)).toEqual(['4', '3'])

    await userEvent.click(toggle)
    await waitFor(() => expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument())
  })

  it('shows an error when the workout cannot be loaded', async () => {
    vi.mocked(getWorkout).mockRejectedValue(new Error('boom'))
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'Push day, 2 exercises' }))

    expect(await screen.findByText('Could not load this workout.')).toBeInTheDocument()
  })

  it('adds, edits, removes and saves exercises', async () => {
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Push day, 2 exercises' }))
    await screen.findAllByRole('spinbutton')

    await userEvent.type(screen.getByRole('textbox', { name: 'Search exercises' }), 'sq')
    expect(await screen.findByRole('button', { name: 'Already added Bench Press' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Add Squat' }))

    const squatSets = screen.getByRole('spinbutton', { name: 'Sets for Squat' })
    expect(squatSets).toHaveValue(3)
    await userEvent.clear(squatSets)
    await userEvent.type(squatSets, '5')
    await userEvent.click(screen.getByRole('button', { name: 'Remove Barbell Row' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() =>
      expect(replaceWorkoutExercises).toHaveBeenCalledWith('w1', [
        { exerciseId: 'e1', targetSets: 4 },
        { exerciseId: 'e3', targetSets: 5 },
      ]),
    )
    expect(await screen.findByText('Workout saved successfully!')).toBeInTheDocument()
  })

  it('blocks saving while a set count is out of range', async () => {
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Push day, 2 exercises' }))
    const [first] = await screen.findAllByRole('spinbutton')

    await userEvent.clear(first)
    await userEvent.type(first, '21')

    expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled()
  })

  it('reports a failed save', async () => {
    vi.mocked(replaceWorkoutExercises).mockRejectedValue(new Error('boom'))
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Push day, 2 exercises' }))
    await screen.findAllByRole('spinbutton')

    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Save failed. Try again.')).toBeInTheDocument()
  })

  it('reorders with the keyboard', async () => {
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Push day, 2 exercises' }))
    await screen.findAllByRole('spinbutton')

    const handle = screen.getByRole('button', { name: /Reorder Bench Press/ })
    fireEvent.pointerDown(handle)
    fireEvent.keyDown(handle, { key: 'Enter' })
    fireEvent.keyDown(handle, { key: 'ArrowUp' })
    fireEvent.keyDown(handle, { key: 'ArrowDown' })
    fireEvent.keyDown(screen.getByRole('button', { name: /Reorder Barbell Row/ }), { key: 'ArrowUp' })
    fireEvent.keyDown(screen.getByRole('button', { name: /Reorder Bench Press/ }), { key: 'ArrowUp' })
    fireEvent.keyDown(screen.getByRole('button', { name: /Reorder Bench Press/ }), { key: 'ArrowDown' })
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() =>
      expect(replaceWorkoutExercises).toHaveBeenCalledWith('w1', [
        { exerciseId: 'e2', targetSets: 3 },
        { exerciseId: 'e1', targetSets: 4 },
      ]),
    )
  })

  it('shows an empty list message and exercise search states', async () => {
    vi.mocked(getWorkout).mockResolvedValue({ id: 'w1', name: 'Push day', exercises: [] })
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Push day, 2 exercises' }))
    expect(await screen.findByText('You haven’t added any exercises yet.')).toBeInTheDocument()
    const search = screen.getByRole('textbox', { name: 'Search exercises' })

    vi.mocked(searchExercises).mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    await userEvent.type(search, 'zz')
    expect(await screen.findByText('No exercises match “zz”.')).toBeInTheDocument()

    vi.mocked(searchExercises).mockRejectedValueOnce(new Error('boom'))
    await userEvent.type(search, 'x')
    expect(await screen.findByText('Could not load exercises.')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Clear search' }))
    expect(search).toHaveValue('')
    await userEvent.type(search, 'sq')
    expect(await screen.findByRole('button', { name: 'Add Squat' })).toBeInTheDocument()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Add Squat' })).not.toBeInTheDocument())
    await userEvent.type(search, 'a')
    expect(await screen.findByRole('button', { name: 'Add Squat' })).toBeInTheDocument()
    await userEvent.click(document.body)
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Add Squat' })).not.toBeInTheDocument())
  })
})
