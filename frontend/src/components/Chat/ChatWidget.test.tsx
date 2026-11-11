import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { sendChatMessage } from '../../api/chat'
import { ApiError } from '../../api/client'
import { announceWorkoutsChanged } from '../../lib/workoutsChanged'
import { ChatWidget } from './ChatWidget'

vi.mock('../../api/chat', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/chat')>()),
  sendChatMessage: vi.fn(),
}))
vi.mock('../../lib/workoutsChanged', () => ({ announceWorkoutsChanged: vi.fn() }))

async function openChat() {
  const user = userEvent.setup()
  render(<ChatWidget />)
  await user.click(screen.getByRole('button', { name: 'Open GymRat Coach' }))
  return user
}

describe('ChatWidget', () => {
  beforeEach(() => {
    vi.mocked(sendChatMessage).mockResolvedValue({ reply: 'Done. Created **Leg Day**', workoutsChanged: false, suggestions: [] })
  })

  afterEach(() => {
    sessionStorage.clear()
    vi.resetAllMocks()
  })

  it('starts closed, opens with suggestions and closes with Escape', async () => {
    const user = await openChat()

    expect(screen.getByRole('dialog', { name: 'GymRat Coach' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Build a push day' })).toBeInTheDocument()

    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('closes from the close button', async () => {
    const user = await openChat()

    await user.click(screen.getByRole('button', { name: 'Close chat' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('sends a typed message with the previous history and announces workout changes', async () => {
    vi.mocked(sendChatMessage)
      .mockResolvedValueOnce({ reply: 'First reply', workoutsChanged: false, suggestions: [] })
      .mockResolvedValueOnce({ reply: 'Created it', workoutsChanged: true, suggestions: [] })
    const user = await openChat()
    const input = screen.getByLabelText('Message the coach')

    await user.type(input, 'Hello{Enter}')
    expect(await screen.findByText('First reply')).toBeInTheDocument()
    await user.type(input, 'Build a leg day{Enter}')

    expect(await screen.findByText('Workouts updated')).toBeInTheDocument()
    expect(sendChatMessage).toHaveBeenLastCalledWith('Build a leg day', [
      { role: 'User', content: 'Hello' },
      { role: 'Assistant', content: 'First reply' },
    ])
    expect(announceWorkoutsChanged).toHaveBeenCalledTimes(1)
    expect(input).toHaveValue('')
  })

  it('sends a suggestion when it is tapped and renders the formatted reply', async () => {
    const user = await openChat()

    await user.click(screen.getByRole('button', { name: 'Build a push day' }))

    expect(await screen.findByText('Leg Day')).toBeInTheDocument()
    expect(sendChatMessage).toHaveBeenCalledWith('Build a push day', [])
    expect(announceWorkoutsChanged).not.toHaveBeenCalled()
  })

  it('shows the typing indicator while waiting and ignores suggestions meanwhile', async () => {
    let resolve: (value: { reply: string; workoutsChanged: boolean; suggestions: never[] }) => void = () => {}
    vi.mocked(sendChatMessage).mockReturnValue(new Promise((done) => (resolve = done)))
    const user = await openChat()

    await user.click(screen.getByRole('button', { name: 'Build a push day' }))
    expect(screen.getByRole('status', { name: 'The coach is typing' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()

    resolve({ reply: 'Ready', workoutsChanged: false, suggestions: [] })
    expect(await screen.findByText('Ready')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('does not send when the message is blank', async () => {
    const user = await openChat()

    await user.type(screen.getByLabelText('Message the coach'), '   {Enter}')

    expect(sendChatMessage).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
  })

  it('shows the character counter near the limit and blocks sending above it', async () => {
    const user = await openChat()
    const input = screen.getByLabelText('Message the coach')

    await user.click(input)
    await user.paste('a'.repeat(849))
    expect(screen.queryByText(/\/ 1000/)).not.toBeInTheDocument()

    await user.paste('bb')
    expect(screen.getByText('851 / 1000')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Send message' })).toBeEnabled()

    await user.paste('c'.repeat(150))
    expect(screen.getByText('1001 / 1000')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Send message' })).toBeDisabled()
  })

  it('shows the server message for a rejected request and retries it', async () => {
    vi.mocked(sendChatMessage)
      .mockRejectedValueOnce(new ApiError(429, 'You are sending messages too fast.', 'BLException_ChatService_RateLimited'))
      .mockResolvedValueOnce({ reply: 'Back again', workoutsChanged: false, suggestions: [] })
    const user = await openChat()

    await user.type(screen.getByLabelText('Message the coach'), 'Plan my week{Enter}')
    expect(await screen.findByRole('alert')).toHaveTextContent('You are sending messages too fast.')

    await user.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByText('Back again')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getAllByText('Plan my week')).toHaveLength(1)
    expect(sendChatMessage).toHaveBeenLastCalledWith('Plan my week', [])
  })

  it('shows a friendly message for server failures', async () => {
    vi.mocked(sendChatMessage).mockRejectedValue(new ApiError(500, 'boom'))
    const user = await openChat()

    await user.type(screen.getByLabelText('Message the coach'), 'Hi{Enter}')

    expect(await screen.findByRole('alert')).toHaveTextContent('The server ran into an issue')
  })

  it('clears the conversation', async () => {
    const user = await openChat()
    await user.click(screen.getByRole('button', { name: 'Build a push day' }))
    await screen.findByText('Leg Day')

    await user.click(screen.getByRole('button', { name: 'Clear chat' }))

    expect(screen.queryByText('Leg Day')).not.toBeInTheDocument()
    expect(screen.getByText('Your digital trainer')).toBeInTheDocument()
  })

  it('opens the conversation with a greeting from the coach', async () => {
    const user = await openChat()
    expect(screen.queryByText(/I'm your GymRat Coach/)).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Build a push day' }))

    expect(await screen.findByText(/I'm your GymRat Coach/)).toBeInTheDocument()
    expect(sendChatMessage).toHaveBeenCalledWith('Build a push day', [])
  })

  it('inserts a new line on Shift+Enter and sends on Enter', async () => {
    const user = await openChat()
    const input = screen.getByLabelText('Message the coach')

    await user.type(input, 'Line one{Shift>}{Enter}{/Shift}Line two')
    expect(input).toHaveValue('Line one\nLine two')
    expect(sendChatMessage).not.toHaveBeenCalled()

    await user.keyboard('{Enter}')
    await waitFor(() => expect(sendChatMessage).toHaveBeenCalledWith('Line one\nLine two', []))
  })

  it('restores the conversation when the widget mounts again', async () => {
    const user = await openChat()
    await user.click(screen.getByRole('button', { name: 'Build a push day' }))
    await screen.findByText('Leg Day')
    cleanup()

    await openChat()

    expect(screen.getByText('Leg Day')).toBeInTheDocument()
    expect(screen.getByText('Build a push day')).toBeInTheDocument()
  })
})
