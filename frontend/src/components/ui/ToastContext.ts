import { createContext, useContext } from 'react'

export type ToastKind = 'success' | 'error'

export interface ToastApi {
  showToast: (kind: ToastKind, message: string) => void
}

export const ToastContext = createContext<ToastApi | null>(null)

export function useToast(): ToastApi {
  const context = useContext(ToastContext)
  if (!context) {
    throw new Error('useToast must be used inside a ToastProvider')
  }
  return context
}
