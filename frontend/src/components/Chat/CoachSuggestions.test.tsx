import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { sendChatMessage } from '../../api/chat'
import { ApiError } from '../../api/client'
import { applySuggestion, dismissSuggestion, getPendingSuggestions, type CoachSuggestion } from '../../api/coach'
import { announceWorkoutsChanged } from '../../lib/workoutsChanged'
import { ChatWidget } from './ChatWidget'
import { saveChatEntries } from './chatStorage'

vi.mock('../../api/chat', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/chat')>()),
  sendChatMessage: vi.fn(),
}))
vi.mock('../../api/coach')
vi.mock('../../lib/workoutsChanged', () => ({ announceWorkoutsChanged: vi.fn() }))

const suggestion: CoachSuggestion = {
  id: 'c1',
  workoutId: 'w1',
  workoutName: 'Push Day',
  exerciseId: 'e1',
  exerciseName: 'Bench Press',
  targetSets: 3,
  repsMin: 6,
  repsMax: 10,
  weightStepKg: 1.25,
  currentTargetSets: 4,
  currentRepsMin: 8,
  currentRepsMax: 12,
  currentWeightStepKg: 2.5,
  rationale: 'Three stalled sessions at 80 kg.',
  status: 'Pending',
}

async function askCoach() {
  const user = userEvent.setup()
  render(<ChatWidget />)
  await user.click(screen.getByRole('button', { name: 'Open GymRat Coach' }))
  await user.type(screen.getByRole('textbox'), 'Why is my bench stuck?{Enter}')
  return { user, card: await screen.findByRole('region', { name: 'Suggested change for Bench Press · Push Day' }) }
}

describe('Coach suggestions in the chat', () => {
  beforeEach(() => {
    vi.mocked(sendChatMessage).mockResolvedValue({ reply: 'You stalled.', workoutsChanged: false, suggestions: [suggestion] })
    vi.mocked(getPendingSuggestions).mockResolvedValue({ items: [suggestion], page: 1, pageSize: 100, totalCount: 1 })
    vi.mocked(applySuggestion).mockResolvedValue(undefined)
    vi.mocked(dismissSuggestion).mockResolvedValue(undefined)
  })

  afterEach(() => {
    sessionStorage.clear()
    vi.resetAllMocks()
  })

  it('treats an unknown status as no longer available instead of crashing', async () => {
    vi.mocked(sendChatMessage).mockResolvedValue({
      reply: 'Old card.',
      workoutsChanged: false,
      suggestions: [{ ...suggestion, status: 2 as unknown as CoachSuggestion['status'] }],
    })

    const { card } = await askCoach()

    expect(within(card).getByText('No longer available')).toBeInTheDocument()
  })

  it('shows the change with before and after values and applies it', async () => {
    const { user, card } = await askCoach()

    expect(within(card).getByText('Suggested change')).toBeInTheDocument()
    expect(within(card).getByText('Sets').nextElementSibling).toHaveTextContent('4 → 3')
    expect(within(card).getByText('Reps').nextElementSibling).toHaveTextContent('8–12 → 6–10')
    expect(within(card).getByText('Step').nextElementSibling).toHaveTextContent('2.5 kg → 1.25 kg')
    expect(within(card).getByText('Three stalled sessions at 80 kg.')).toBeInTheDocument()

    await user.click(within(card).getByRole('button', { name: 'Apply' }))

    expect(await within(card).findByText('Applied')).toBeInTheDocument()
    expect(applySuggestion).toHaveBeenCalledWith('c1')
    expect(announceWorkoutsChanged).toHaveBeenCalledTimes(1)
  })

  it('dismisses a suggestion without announcing a change', async () => {
    const { user, card } = await askCoach()

    await user.click(within(card).getByRole('button', { name: 'Not now' }))

    expect(await within(card).findByText('Dismissed')).toBeInTheDocument()
    expect(dismissSuggestion).toHaveBeenCalledWith('c1')
    expect(announceWorkoutsChanged).not.toHaveBeenCalled()
  })

  it('shows the server message when applying fails and keeps the buttons', async () => {
    vi.mocked(applySuggestion).mockRejectedValue(new ApiError(400, 'The workout changed after suggestion c1 was made.', 'BLException_CoachService_SuggestionStale'))
    const { user, card } = await askCoach()

    await user.click(within(card).getByRole('button', { name: 'Apply' }))

    expect(await within(card).findByRole('alert')).toHaveTextContent('The workout changed after suggestion c1 was made.')
    expect(within(card).getByRole('button', { name: 'Apply' })).toBeEnabled()
  })

  it('marks stored suggestions that are no longer pending when the chat opens', async () => {
    saveChatEntries([
      { id: 0, kind: 'user', content: 'Help' },
      { id: 1, kind: 'assistant', content: 'Try this.', suggestions: [suggestion] },
    ])
    vi.mocked(getPendingSuggestions).mockResolvedValue({ items: [], page: 1, pageSize: 100, totalCount: 0 })
    const user = userEvent.setup()
    render(<ChatWidget />)

    await user.click(screen.getByRole('button', { name: 'Open GymRat Coach' }))

    expect(await screen.findByText('No longer available')).toBeInTheDocument()
  })

  it('does not ask the server when nothing is pending', async () => {
    saveChatEntries([
      { id: 0, kind: 'user', content: 'Help' },
      { id: 1, kind: 'assistant', content: 'Done.', suggestions: [{ ...suggestion, status: 'Applied' }] },
    ])
    const user = userEvent.setup()
    render(<ChatWidget />)

    await user.click(screen.getByRole('button', { name: 'Open GymRat Coach' }))

    await waitFor(() => expect(screen.getByText('Applied')).toBeInTheDocument())
    expect(getPendingSuggestions).not.toHaveBeenCalled()
  })

  it('shows only the reps change when the range alone changes', async () => {
    vi.mocked(sendChatMessage).mockResolvedValue({
      reply: 'Lower the range.',
      workoutsChanged: false,
      suggestions: [{ ...suggestion, targetSets: null, weightStepKg: null, repsMin: null, repsMax: 10 }],
    })
    const { card } = await askCoach()

    expect(within(card).getByText('Reps').nextElementSibling).toHaveTextContent('8–12 → 8–10')
    expect(within(card).queryByText('Sets')).not.toBeInTheDocument()
    expect(within(card).queryByText('Step')).not.toBeInTheDocument()
  })
})
