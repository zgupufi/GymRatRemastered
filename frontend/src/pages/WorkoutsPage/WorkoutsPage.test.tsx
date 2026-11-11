import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
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
import { announceWorkoutsChanged } from '../../lib/workoutsChanged'
import { WorkoutsPage } from './WorkoutsPage'

vi.mock('../../api/workouts')
vi.mock('../../api/exercises')
vi.mock('../../hooks/useDebouncedValue', () => ({ useDebouncedValue: <T,>(value: T) => value }))

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

async function openMenu(name: string, item: 'Rename' | 'Delete') {
  await userEvent.click(await screen.findByRole('button', { name: 'More actions for ' + name }))
  await userEvent.click(screen.getByRole('menuitem', { name: item }))
}

async function expand(name = 'Push day') {
  await userEvent.click(await screen.findByRole('button', { name }))
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
        { order: 2, targetSets: 3, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: row },
        { order: 1, targetSets: 4, repsMin: 8, repsMax: 12, weightStepKg: 2.5, exercise: bench },
      ],
    })
    vi.mocked(searchExercises).mockResolvedValue({ items: [bench, row, squat], page: 1, pageSize: 20, totalCount: 3 })
  })

  afterEach(() => {
    vi.resetAllMocks()
  })

  it('reloads the list when the coach changes workouts', async () => {
    renderPage()
    await screen.findByText('Push day')
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([push, pull, { ...push, id: 'w3', name: 'Leg day' }]))

    act(() => announceWorkoutsChanged())

    expect(await screen.findByText('Leg day')).toBeInTheDocument()
    expect(getWorkoutSummaries).toHaveBeenCalledTimes(2)
  })

  it('lists workout names without counts, muscles or a pager', async () => {
    renderPage()

    expect(await screen.findByText('Push day')).toBeInTheDocument()
    expect(screen.getByText('Pull day')).toBeInTheDocument()
    expect(screen.queryByText('Shoulders')).not.toBeInTheDocument()
    expect(screen.queryByText(/[0-9]+ exercises?/)).not.toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Workouts pages' })).not.toBeInTheDocument()
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
    expect(screen.getByText('Showing 1–1 of 12 workouts')).toBeInTheDocument()

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

  it('creates a workout and opens its editor', async () => {
    const created: WorkoutSummary = { id: 'w3', name: 'New workout - Oct 29', exerciseCount: 0, mainMuscles: [] }
    vi.mocked(createWorkout).mockResolvedValue({ id: 'w3' })
    vi.mocked(getWorkout).mockResolvedValue({ id: 'w3', name: created.name, exercises: [] })
    renderPage()
    await screen.findByText('Push day')
    vi.mocked(getWorkoutSummaries).mockResolvedValue(page([created, push, pull]))

    await userEvent.click(screen.getByRole('button', { name: 'Create workout' }))

    expect(await screen.findByRole('textbox', { name: 'Search exercises' })).toBeInTheDocument()
    expect(screen.queryByRole('textbox', { name: 'Workout name' })).not.toBeInTheDocument()
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
    await openMenu('Push day', 'Rename')

    const input = screen.getByRole('textbox', { name: 'Workout name' })
    await userEvent.clear(input)
    await userEvent.type(input, 'Chest day{Enter}')

    await waitFor(() => expect(renameWorkout).toHaveBeenCalledWith('w1', 'Chest day'))
    expect(screen.queryByRole('textbox', { name: 'Workout name' })).not.toBeInTheDocument()
  })

  it('leaves the name alone when it is unchanged or the rename is cancelled', async () => {
    renderPage()
    await openMenu('Push day', 'Rename')
    await userEvent.click(screen.getByRole('button', { name: 'Save name' }))
    await openMenu('Push day', 'Rename')
    await userEvent.type(screen.getByRole('textbox', { name: 'Workout name' }), 'x{Escape}')

    expect(renameWorkout).not.toHaveBeenCalled()
    expect(screen.getByText('Push day')).toBeInTheDocument()
  })

  it('does not rename to an empty name and reports a failed rename', async () => {
    vi.mocked(renameWorkout).mockRejectedValue(new Error('boom'))
    renderPage()
    await openMenu('Push day', 'Rename')
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
    await openMenu('Push day', 'Delete')

    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(deleteWorkout).not.toHaveBeenCalled()

    await openMenu('Push day', 'Delete')
    await userEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(deleteWorkout).toHaveBeenCalledWith('w1'))
  })

  it('reports a failed deletion', async () => {
    vi.mocked(deleteWorkout).mockRejectedValue(new Error('boom'))
    renderPage()
    await openMenu('Push day', 'Delete')

    await userEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText('Could not delete the workout. Try again.')).toBeInTheDocument()
  })

  it('loads the exercises in order when a card expands and collapses again', async () => {
    renderPage()
    const toggle = await screen.findByRole('button', { name: 'Push day' })

    await userEvent.click(toggle)

    const sets = await screen.findAllByRole('spinbutton')
    expect(sets.map((input) => input.getAttribute('aria-label'))).toEqual([
      'Sets for Bench Press',
      'Minimum reps for Bench Press',
      'Maximum reps for Bench Press',
      'Sets for Barbell Row',
      'Minimum reps for Barbell Row',
      'Maximum reps for Barbell Row',
    ])
    expect(sets.map((input) => (input as HTMLInputElement).value)).toEqual(['4', '8', '12', '3', '8', '12'])

    await userEvent.click(toggle)
    await waitFor(() => expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument())
  })

  it('shows an error when the workout cannot be loaded', async () => {
    vi.mocked(getWorkout).mockRejectedValue(new Error('boom'))
    renderPage()

    await expand()

    expect(await screen.findByText('Could not load this workout.')).toBeInTheDocument()
  })

  it('autosaves an edited rep range but never an inverted one', async () => {
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    renderPage()
    await expand()
    const minimum = await screen.findByRole('spinbutton', { name: 'Minimum reps for Bench Press' })
    const maximum = screen.getByRole('spinbutton', { name: 'Maximum reps for Bench Press' })

    await userEvent.clear(minimum)
    await userEvent.type(minimum, '14')
    await new Promise((resolve) => setTimeout(resolve, 800))

    expect(minimum).toHaveAttribute('aria-invalid', 'true')
    expect(replaceWorkoutExercises).not.toHaveBeenCalled()

    await userEvent.clear(minimum)
    await userEvent.type(minimum, '6')
    await userEvent.clear(maximum)
    await userEvent.type(maximum, '10')

    await waitFor(
      () =>
        expect(replaceWorkoutExercises).toHaveBeenLastCalledWith('w1', [
          { exerciseId: 'e1', targetSets: 4, repsMin: 6, repsMax: 10 },
          { exerciseId: 'e2', targetSets: 3, repsMin: 8, repsMax: 12 },
        ]),
      { timeout: 3000 },
    )
  })

  it('adds, edits, removes and saves exercises', async () => {
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    renderPage()
    await expand()
    await screen.findAllByRole('spinbutton')

    await userEvent.type(screen.getByRole('textbox', { name: 'Search exercises' }), 'sq')
    expect(await screen.findByRole('button', { name: 'Already added Bench Press' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Add Squat' }))
    expect(screen.getByRole('textbox', { name: 'Search exercises' })).toHaveValue('')

    const squatSets = screen.getByRole('spinbutton', { name: 'Sets for Squat' })
    expect(squatSets).toHaveValue(3)
    await userEvent.clear(squatSets)
    await userEvent.type(squatSets, '5')
    await userEvent.click(screen.getByRole('button', { name: 'Remove Barbell Row' }))

    await waitFor(
      () =>
        expect(replaceWorkoutExercises).toHaveBeenLastCalledWith('w1', [
          { exerciseId: 'e1', targetSets: 4, repsMin: 8, repsMax: 12 },
          { exerciseId: 'e3', targetSets: 5, repsMin: 8, repsMax: 12 },
        ]),
      { timeout: 3000 },
    )
    expect(await screen.findByText('Saved')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument()
  })

  it('does not save while a set count is out of range', async () => {
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    renderPage()
    await expand()
    const [first] = await screen.findAllByRole('spinbutton')

    await userEvent.clear(first)
    await userEvent.type(first, '21')
    await new Promise((resolve) => setTimeout(resolve, 900))

    expect(replaceWorkoutExercises).not.toHaveBeenCalled()
  })

  it('reports a failed save', async () => {
    vi.mocked(replaceWorkoutExercises).mockRejectedValue(new Error('boom'))
    renderPage()
    await expand()
    await screen.findAllByRole('spinbutton')

    await userEvent.click(screen.getByRole('button', { name: 'Remove Barbell Row' }))

    expect(await screen.findByText('Could not save', undefined, { timeout: 3000 })).toBeInTheDocument()
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('Saved')).toBeInTheDocument()
  })

  it('saves pending edits when the editor closes', async () => {
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    renderPage()
    await expand()
    await screen.findAllByRole('spinbutton')
    await userEvent.click(screen.getByRole('button', { name: 'Remove Barbell Row' }))

    await expand()

    await waitFor(() => expect(replaceWorkoutExercises).toHaveBeenCalledWith('w1', [{ exerciseId: 'e1', targetSets: 4, repsMin: 8, repsMax: 12 }]))
  })

  it('reorders with the keyboard', async () => {
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    renderPage()
    await expand()
    await screen.findAllByRole('spinbutton')

    const handle = screen.getByRole('button', { name: /Reorder Bench Press/ })
    fireEvent.pointerDown(handle)
    fireEvent.keyDown(handle, { key: 'Enter' })
    fireEvent.keyDown(handle, { key: 'ArrowUp' })
    fireEvent.keyDown(handle, { key: 'ArrowDown' })
    fireEvent.keyDown(screen.getByRole('button', { name: /Reorder Barbell Row/ }), { key: 'ArrowUp' })
    fireEvent.keyDown(screen.getByRole('button', { name: /Reorder Bench Press/ }), { key: 'ArrowUp' })
    fireEvent.keyDown(screen.getByRole('button', { name: /Reorder Bench Press/ }), { key: 'ArrowDown' })

    await waitFor(
      () =>
        expect(replaceWorkoutExercises).toHaveBeenLastCalledWith('w1', [
          { exerciseId: 'e2', targetSets: 3, repsMin: 8, repsMax: 12 },
          { exerciseId: 'e1', targetSets: 4, repsMin: 8, repsMax: 12 },
        ]),
      { timeout: 3000 },
    )
  })

  it('shows an empty list message and exercise search states', async () => {
    vi.mocked(getWorkout).mockResolvedValue({ id: 'w1', name: 'Push day', exercises: [] })
    renderPage()
    await expand()
    expect(await screen.findByText('You haven’t added any exercises yet.')).toBeInTheDocument()
    const search = screen.getByRole('textbox', { name: 'Search exercises' })

    vi.mocked(searchExercises).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    await userEvent.type(search, 'zz')
    expect(await screen.findByText('No exercises match “zz”.')).toBeInTheDocument()

    vi.mocked(searchExercises).mockRejectedValue(new Error('boom'))
    await userEvent.type(search, 'x')
    expect(await screen.findByText('Could not load exercises.')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Clear search' }))
    expect(search).toHaveValue('')
    vi.mocked(searchExercises).mockResolvedValue({ items: [bench, row, squat], page: 1, pageSize: 20, totalCount: 3 })
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
