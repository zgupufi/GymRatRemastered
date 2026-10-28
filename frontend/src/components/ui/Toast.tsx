import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { Check, TriangleAlert } from 'lucide-react'
import { spring } from '../../lib/motion'
import { ToastContext, type ToastKind } from './ToastContext'
import './Toast.css'

interface ToastItem {
  id: number
  kind: ToastKind
  message: string
}

const TOAST_DURATION_MS = 3000

function ToastBubble({ toast }: { toast: ToastItem | null }) {
  return (
    <AnimatePresence>
      {toast ? (
        <motion.div
          key={toast.id}
          className={`toast toast--${toast.kind}`}
          initial={{ opacity: 0, y: -32, scale: 0.96 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={{ opacity: 0, y: -32, scale: 0.96 }}
          transition={spring}
        >
          <span className="toast__icon" aria-hidden="true">
            {toast.kind === 'success' ? <Check size={14} strokeWidth={3} /> : <TriangleAlert size={14} strokeWidth={3} />}
          </span>
          {toast.message}
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<ToastItem | null>(null)
  const nextId = useRef(0)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  const showToast = useCallback((kind: ToastKind, message: string) => {
    clearTimeout(timer.current)
    nextId.current += 1
    setToast({ id: nextId.current, kind, message })
    timer.current = setTimeout(() => setToast(null), TOAST_DURATION_MS)
  }, [])

  useEffect(() => () => clearTimeout(timer.current), [])

  const api = useMemo(() => ({ showToast }), [showToast])

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toast-region">
        <div role="status" aria-live="polite">
          <ToastBubble toast={toast?.kind === 'success' ? toast : null} />
        </div>
        <div role="alert">
          <ToastBubble toast={toast?.kind === 'error' ? toast : null} />
        </div>
      </div>
    </ToastContext.Provider>
  )
}
