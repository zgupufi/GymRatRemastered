import { afterEach, describe, expect, it, vi } from 'vitest'
import { sendChatMessage } from './chat'

describe('sendChatMessage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts the message with its history and returns the reply', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(new Response('{"reply":"Done","workoutsChanged":true}', { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await sendChatMessage('Build a leg day', [{ role: 'User', content: 'Hi' }])

    expect(fetchMock.mock.calls[0][0]).toBe('/api/chat/messages')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({
      method: 'POST',
      body: '{"message":"Build a leg day","history":[{"role":"User","content":"Hi"}]}',
    })
    expect(result).toEqual({ reply: 'Done', workoutsChanged: true })
  })
})
