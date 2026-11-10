import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { sendChatMessage, type ChatHistoryItem } from '../../api/chat'
import { applySuggestion, dismissSuggestion, getPendingSuggestions, type CoachSuggestion, type SuggestionStatus } from '../../api/coach'
import { friendlyErrorMessage } from '../../api/errors'
import { announceWorkoutsChanged } from '../../lib/workoutsChanged'
import { clearChatEntries, loadChatEntries, saveChatEntries } from './chatStorage'

export const HISTORY_LIMIT = 10

const UNUSABLE_REPLY_MESSAGE = 'The coach could not complete that request. Try again.'

function isUnusableReply(reply: string): boolean {
  return /<tool_call|<function=/i.test(reply)
}

const CHANGED_EVENT = 'gymrat:chat-changed'

export type SuggestionAction = 'apply' | 'dismiss'

export interface ChatEntry {
  id: number
  kind: 'user' | 'assistant' | 'error'
  content: string
  workoutsChanged?: boolean
  retryText?: string
  suggestions?: CoachSuggestion[]
}

function withSuggestionStatus(entries: ChatEntry[], statusFor: (suggestion: CoachSuggestion) => SuggestionStatus): ChatEntry[] {
  return entries.map((entry) =>
    entry.suggestions
      ? { ...entry, suggestions: entry.suggestions.map((suggestion) => ({ ...suggestion, status: statusFor(suggestion) })) }
      : entry,
  )
}

function nextIdAfter(entries: ChatEntry[]): number {
  return entries.reduce((highest, entry) => Math.max(highest, entry.id + 1), 0)
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
  const nextId = useRef(nextIdAfter(initial))
  const entriesRef = useRef<ChatEntry[]>(initial)

  const instance = useRef(Symbol('chat'))

  const commit = useCallback((next: ChatEntry[]) => {
    entriesRef.current = next
    setEntries(next)
    if (next.length > 0) {
      saveChatEntries(next)
    } else {
      clearChatEntries()
    }
    window.dispatchEvent(new CustomEvent<symbol>(CHANGED_EVENT, { detail: instance.current }))
  }, [])

  useEffect(() => {
    const onChanged = (event: Event) => {
      if ((event as CustomEvent<symbol>).detail === instance.current) {
        return
      }
      const stored = loadChatEntries()
      entriesRef.current = stored
      nextId.current = Math.max(nextId.current, nextIdAfter(stored))
      setEntries(stored)
    }
    window.addEventListener(CHANGED_EVENT, onChanged)
    return () => window.removeEventListener(CHANGED_EVENT, onChanged)
  }, [])

  const send = useCallback(
    async (text: string) => {
      const history = toHistory(entriesRef.current)
      commit([...entriesRef.current, { id: nextId.current++, kind: 'user', content: text }])
      setPending(true)
      try {
        const result = await sendChatMessage(text, history)
        if (isUnusableReply(result.reply)) {
          commit([...entriesRef.current, { id: nextId.current++, kind: 'error', content: UNUSABLE_REPLY_MESSAGE, retryText: text }])
          return
        }
        commit([
          ...entriesRef.current,
          {
            id: nextId.current++,
            kind: 'assistant',
            content: result.reply,
            workoutsChanged: result.workoutsChanged,
            ...(result.suggestions?.length ? { suggestions: result.suggestions } : {}),
          },
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

  const resolveSuggestion = useCallback(
    async (suggestionId: string, action: SuggestionAction) => {
      if (action === 'apply') {
        await applySuggestion(suggestionId)
      } else {
        await dismissSuggestion(suggestionId)
      }
      const status: SuggestionStatus = action === 'apply' ? 'Applied' : 'Dismissed'
      commit(withSuggestionStatus(entriesRef.current, (suggestion) => (suggestion.id === suggestionId ? status : suggestion.status)))
      if (action === 'apply') {
        announceWorkoutsChanged()
      }
    },
    [commit],
  )

  const refreshSuggestions = useCallback(
    async (signal?: AbortSignal) => {
      if (!entriesRef.current.some((entry) => entry.suggestions?.some((suggestion) => suggestion.status === 'Pending'))) {
        return
      }
      const pending = await getPendingSuggestions(signal)
      const pendingIds = new Set(pending.items.map((suggestion) => suggestion.id))
      commit(
        withSuggestionStatus(entriesRef.current, (suggestion) =>
          suggestion.status === 'Pending' && !pendingIds.has(suggestion.id) ? 'Expired' : suggestion.status,
        ),
      )
    },
    [commit],
  )

  return { entries, pending, send, retry, clear, resolveSuggestion, refreshSuggestions }
}
