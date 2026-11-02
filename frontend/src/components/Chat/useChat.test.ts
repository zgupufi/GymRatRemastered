import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { sendChatMessage } from '../../api/chat'
import { type ChatEntry, toHistory, useChat } from './useChat'

vi.mock('../../api/chat')

function entry(id: number, kind: ChatEntry['kind'], content = `m${id}`): ChatEntry {
  return { id, kind, content }
}

describe('toHistory', () => {
  it('keeps only the last 10 messages', () => {
    const entries = Array.from({ length: 16 }, (_, index) => entry(index, index % 2 === 0 ? 'user' : 'assistant'))

    const history = toHistory(entries)

    expect(history).toHaveLength(10)
    expect(history[0]).toEqual({ role: 'User', content: 'm6' })
    expect(history[9]).toEqual({ role: 'Assistant', content: 'm15' })
  })

  it('never starts with an assistant message', () => {
    const entries = [entry(0, 'user'), ...Array.from({ length: 10 }, (_, index) => entry(index + 1, index % 2 === 0 ? 'assistant' : 'user'))]

    const history = toHistory(entries)

    expect(history[0].role).toBe('User')
    expect(history).toHaveLength(9)
  })

  it('drops errors and the user message of a failed send', () => {
    const entries = [entry(0, 'user'), entry(1, 'assistant'), entry(2, 'user', 'failed'), entry(3, 'error'), entry(4, 'user', 'next')]

    expect(toHistory(entries)).toEqual([
      { role: 'User', content: 'm0' },
      { role: 'Assistant', content: 'm1' },
      { role: 'User', content: 'next' },
    ])
  })

  it('is empty when there is no user message', () => {
    expect(toHistory([entry(0, 'assistant')])).toEqual([])
  })
})

describe('useChat persistence', () => {
  afterEach(() => {
    sessionStorage.clear()
    vi.resetAllMocks()
  })

  it('restores the conversation after a remount and keeps ids unique', async () => {
    vi.mocked(sendChatMessage).mockResolvedValue({ reply: 'Hello', workoutsChanged: false })
    const first = renderHook(() => useChat())
    await act(() => first.result.current.send('Hi'))
    first.unmount()

    const second = renderHook(() => useChat())
    expect(second.result.current.entries.map((item) => item.content)).toEqual(['Hi', 'Hello'])

    await act(() => second.result.current.send('Again'))
    await waitFor(() => expect(second.result.current.entries).toHaveLength(4))
    expect(new Set(second.result.current.entries.map((item) => item.id)).size).toBe(4)
  })

  it('removes the stored conversation when it is cleared', async () => {
    vi.mocked(sendChatMessage).mockResolvedValue({ reply: 'Hello', workoutsChanged: false })
    const { result } = renderHook(() => useChat())
    await act(() => result.current.send('Hi'))
    expect(sessionStorage.getItem('gymrat_chat_history')).not.toBeNull()

    act(() => result.current.clear())

    expect(result.current.entries).toEqual([])
    expect(sessionStorage.getItem('gymrat_chat_history')).toBeNull()
  })
})
