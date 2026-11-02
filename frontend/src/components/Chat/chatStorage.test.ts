import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearChatEntries, loadChatEntries, saveChatEntries } from './chatStorage'

const entries = [
  { id: 0, kind: 'user' as const, content: 'Hi' },
  { id: 1, kind: 'assistant' as const, content: 'Hello', workoutsChanged: true },
]

describe('chatStorage', () => {
  afterEach(() => {
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('round-trips the saved entries and clears them', () => {
    saveChatEntries(entries)
    expect(loadChatEntries()).toEqual(entries)

    clearChatEntries()
    expect(loadChatEntries()).toEqual([])
  })

  it.each(['not json', '{"a":1}', '[{"id":"x","kind":"user","content":"Hi"}]', '[null]'])(
    'ignores invalid stored data %s',
    (raw) => {
      sessionStorage.setItem('gymrat_chat_history', raw)

      expect(loadChatEntries()).toEqual([])
    },
  )

  it('does not throw when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(loadChatEntries()).toEqual([])
    expect(() => saveChatEntries(entries)).not.toThrow()
    expect(() => clearChatEntries()).not.toThrow()
  })
})
