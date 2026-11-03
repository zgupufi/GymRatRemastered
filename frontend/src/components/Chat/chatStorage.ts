import type { ChatEntry } from './useChat'

const STORAGE_KEY = 'gymrat_chat_history'
const KINDS: ChatEntry['kind'][] = ['user', 'assistant', 'error']

function isChatEntry(value: unknown): value is ChatEntry {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const entry = value as ChatEntry
  return typeof entry.id === 'number' && KINDS.includes(entry.kind) && typeof entry.content === 'string'
}

export function loadChatEntries(): ChatEntry[] {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) {
      return []
    }
    const parsed: unknown = JSON.parse(raw)
    return Array.isArray(parsed) && parsed.every(isChatEntry) ? parsed : []
  } catch {
    return []
  }
}

export function saveChatEntries(entries: ChatEntry[]): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(entries))
  } catch {
    return
  }
}

export function clearChatEntries(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    return
  }
}
