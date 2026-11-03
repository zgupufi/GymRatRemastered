import { apiClient } from './client'

export type ChatRole = 'User' | 'Assistant'

export interface ChatHistoryItem {
  role: ChatRole
  content: string
}

export interface ChatReply {
  reply: string
  workoutsChanged: boolean
}

export const MAX_CHAT_MESSAGE_LENGTH = 1000

export function sendChatMessage(message: string, history: ChatHistoryItem[]): Promise<ChatReply> {
  return apiClient.post<ChatReply>('/api/chat/messages', { message, history })
}
