import { apiClient } from './client'
import type { Schema } from './schema'

export type ChatRole = Schema<'ChatRole'>

export type ChatHistoryItem = Schema<'ChatHistoryItem'>

export type ChatReply = Schema<'ChatMessageResponse'>

export const MAX_CHAT_MESSAGE_LENGTH = 1000

export function sendChatMessage(message: string, history: ChatHistoryItem[]): Promise<ChatReply> {
  return apiClient.post<ChatReply>('/api/chat/messages', { message, history } satisfies Schema<'ChatMessageRequest'>)
}
