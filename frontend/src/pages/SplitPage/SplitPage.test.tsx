import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { assignSplitDay, getSplit } from '../../api/split'
import { getWorkoutSummaries, type WorkoutSummary } from '../../api/workouts'
import { ToastProvider } from '../../components/ui/Toast'
import { SplitPage } from './SplitPage'

vi.mock('../../api/split')
vi.mock('../../api/workouts')

const push: WorkoutSummary = { id: 'w1', name: 'Push day', exerciseCount: 6, mainMuscles: ['Chest', 'Triceps', 'Chest'] }
const legs: WorkoutSummary = { id: 'w2', name: 'Leg day', exerciseCount: 5, mainMuscles: ['Quads'] }
const everything: WorkoutSummary = {
  id: 'w3',
  name: 'Everything',
  exerciseCount: 9,
  mainMuscles: ['Chest', 'Back', 'Quads', 'Hamstrings', 'Shoulders', 'Biceps'],
}

function days(assigned: Record<number, WorkoutSummary> = {}) {
  return [1, 2, 3, 4, 5, 6, 7].map((day) => ({ day, workout: assigned[day] ?? null }))
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ToastProvider>
        <SplitPage />
      </ToastProvider>
    </MemoryRouter>,
  )
}

describe('SplitPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
    Element.prototype.scrollIntoView = vi.fn()
  })

  beforeEach(() => {
    vi.mocked(getSplit).mockResolvedValue(days({ 1: push }))
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [push, legs, everything], page: 1, pageSize: 100, totalCount: 3 })
    vi.mocked(assignSplitDay).mockResolvedValue(undefined)
  })

  afterEach(() => {
    vi.resetAllMocks()
    vi.useRealTimers()
  })

  it('shows seven days with the assigned workout and unique muscles', async () => {
    renderPage()

    const monday = await screen.findByRole('listitem', { name: 'Monday' })
    expect(within(monday).getByRole('button', { name: 'Monday workout' })).toHaveTextContent('Push day')
    expect(within(monday).getAllByText('Chest')).toHaveLength(1)
    expect(within(monday).getByText('Triceps')).toBeInTheDocument()
    expect(screen.getAllByRole('listitem').filter((item) => item.classList.contains('day-card'))).toHaveLength(7)
    expect(within(screen.getByRole('listitem', { name: 'Tuesday' })).getByRole('button', { name: 'Tuesday workout' })).toHaveTextContent('Rest day')
  })

  it.each([
    [new Date(2026, 10, 1), 'Sunday'],
    [new Date(2026, 10, 2), 'Monday'],
  ])('highlights today on %s', async (date, name) => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(date)
    renderPage()

    const card = await screen.findByRole('listitem', { name })
    expect(within(card).getByText('Today')).toBeInTheDocument()
    expect(screen.getAllByText('Today')).toHaveLength(1)
    expect(Element.prototype.scrollIntoView).toHaveBeenCalled()
  })

  it('shows a full body chip from six muscle groups', async () => {
    vi.mocked(getSplit).mockResolvedValue(days({ 5: everything }))
    renderPage()

    const friday = await screen.findByRole('listitem', { name: 'Friday' })
    expect(within(friday).getByText('Full body')).toBeInTheDocument()
    expect(within(friday).queryByText('Chest')).not.toBeInTheDocument()
  })

  it('saves only the changed days', async () => {
    const user = userEvent.setup()
    renderPage()
    const save = await screen.findByRole('button', { name: 'Save split' })
    expect(save).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Monday workout' }))
    await user.click(screen.getByRole('option', { name: 'Rest day' }))
    await user.click(screen.getByRole('button', { name: 'Wednesday workout' }))
    await user.click(screen.getByRole('option', { name: 'Leg day' }))
    expect(within(screen.getByRole('listitem', { name: 'Wednesday' })).getByText('Quads')).toBeInTheDocument()
    await user.click(save)

    expect(await screen.findByText('Split saved')).toBeInTheDocument()
    expect(assignSplitDay).toHaveBeenCalledTimes(2)
    expect(assignSplitDay).toHaveBeenCalledWith(1, null)
    expect(assignSplitDay).toHaveBeenCalledWith(3, 'w2')
    expect(save).toBeDisabled()
  })

  it('reports a failed save', async () => {
    vi.mocked(assignSplitDay).mockRejectedValue(new Error('boom'))
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Tuesday workout' }))
    await user.click(screen.getByRole('option', { name: 'Push day' }))
    await user.click(screen.getByRole('button', { name: 'Save split' }))

    expect(await screen.findByText('Could not save your split. Try again.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save split' })).toBeEnabled()
  })

  it('closes the menu on escape and outside click, and supports keyboard choice', async () => {
    const user = userEvent.setup()
    renderPage()
    const trigger = await screen.findByRole('button', { name: 'Tuesday workout' })

    await user.click(trigger)
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()

    await user.click(trigger)
    await user.click(document.body)
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()

    await user.click(trigger)
    screen.getByRole('option', { name: 'Leg day' }).focus()
    await user.keyboard('{Enter}')
    expect(trigger).toHaveTextContent('Leg day')

    await user.click(trigger)
    screen.getByRole('option', { name: 'Rest day' }).focus()
    await user.keyboard('{Enter}')
    expect(trigger).toHaveTextContent('Rest day')

    await user.click(trigger)
    screen.getByRole('option', { name: 'Leg day' }).focus()
    await user.keyboard('a')
    expect(screen.getByRole('listbox')).toBeInTheDocument()
  })

  it('navigates the menu with arrows, home, end and space', async () => {
    const user = userEvent.setup()
    renderPage()
    const trigger = await screen.findByRole('button', { name: 'Tuesday workout' })

    trigger.focus()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('option', { name: 'Rest day' })).toHaveFocus()
    await user.keyboard('{ArrowDown}{ArrowDown}')
    expect(screen.getByRole('option', { name: 'Leg day' })).toHaveFocus()
    await user.keyboard('{End}')
    expect(screen.getByRole('option', { name: 'Everything' })).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('option', { name: 'Everything' })).toHaveFocus()
    await user.keyboard('{Home}')
    expect(screen.getByRole('option', { name: 'Rest day' })).toHaveFocus()
    await user.keyboard('{ArrowDown}{ }')

    expect(trigger).toHaveTextContent('Push day')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()

    await user.click(trigger)
    await user.keyboard('{ArrowUp}')
    expect(screen.getByRole('option', { name: 'Everything' })).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(trigger).toHaveFocus()
  })

  it('retries after a load failure', async () => {
    vi.mocked(getSplit).mockRejectedValueOnce(new Error('down'))
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByText('Could not load your split')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    await waitFor(() => expect(screen.getByRole('listitem', { name: 'Monday' })).toBeInTheDocument())
  })
})
