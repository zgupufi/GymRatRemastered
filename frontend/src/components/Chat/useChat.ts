import { useCallback, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { sendChatMessage, type ChatHistoryItem } from '../../api/chat'
import { friendlyErrorMessage } from '../../api/errors'
import { announceWorkoutsChanged } from '../../lib/workoutsChanged'
import { clearChatEntries, loadChatEntries, saveChatEntries } from './chatStorage'

export const HISTORY_LIMIT = 10

export interface ChatEntry {
  id: number
  kind: 'user' | 'assistant' | 'error'
  content: string
  workoutsChanged?: boolean
  retryText?: string
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError && error.code && error.status >= 400 && error.status < 500) {
    return error.message
  }
  return friendlyErrorMessage(error)
}

function withoutLeadingAssistant(entries: ChatEntry[]): ChatEntry[] {
  const firstUser = entries.findIndex((entry) => entry.kind === 'user')
  return firstUser === -1 ? [] : entries.slice(firstUser)
}

export function toHistory(entries: ChatEntry[]): ChatHistoryItem[] {
  const delivered = entries.filter((entry, index) => entry.kind !== 'error' && entries[index + 1]?.kind !== 'error')
  return withoutLeadingAssistant(withoutLeadingAssistant(delivered).slice(-HISTORY_LIMIT)).map((entry) => ({
    role: entry.kind === 'user' ? 'User' : 'Assistant',
    content: entry.content,
  }))
}

export function useChat() {
  const [initial] = useState(loadChatEntries)
  const [entries, setEntries] = useState<ChatEntry[]>(initial)
  const [pending, setPending] = useState(false)
  const nextId = useRef(initial.reduce((highest, entry) => Math.max(highest, entry.id + 1), 0))
  const entriesRef = useRef<ChatEntry[]>(initial)

  const commit = useCallback((next: ChatEntry[]) => {
    entriesRef.current = next
    setEntries(next)
    if (next.length > 0) {
      saveChatEntries(next)
    } else {
      clearChatEntries()
    }
  }, [])

  const send = useCallback(
    async (text: string) => {
      const history = toHistory(entriesRef.current)
      commit([...entriesRef.current, { id: nextId.current++, kind: 'user', content: text }])
      setPending(true)
      try {
        const result = await sendChatMessage(text, history)
        commit([
          ...entriesRef.current,
          { id: nextId.current++, kind: 'assistant', content: result.reply, workoutsChanged: result.workoutsChanged },
        ])
        if (result.workoutsChanged) {
          announceWorkoutsChanged()
        }
      } catch (error) {
        commit([
          ...entriesRef.current,
          { id: nextId.current++, kind: 'error', content: errorMessage(error), retryText: text },
        ])
      } finally {
        setPending(false)
      }
    },
    [commit],
  )

  const retry = useCallback(
    (errorId: number) => {
      const current = entriesRef.current
      const index = current.findIndex((entry) => entry.id === errorId)
      const failed = current[index]
      if (!failed?.retryText) {
        return
      }
      commit(current.slice(0, index - 1))
      return send(failed.retryText)
    },
    [commit, send],
  )

  const clear = useCallback(() => commit([]), [commit])

  return { entries, pending, send, retry, clear }
}
