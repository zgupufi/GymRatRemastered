import { useEffect, useRef, useState } from 'react'

export function useStopwatch(running: boolean, since?: number) {
  const [elapsed, setElapsed] = useState(() => (since === undefined ? 0 : Math.max(0, Math.floor((Date.now() - since) / 1000))))
  const startedAt = useRef<number | null>(since ?? null)

  useEffect(() => {
    if (!running) {
      return
    }
    const base = startedAt.current ?? Date.now()
    startedAt.current = base
    const tick = () => setElapsed(Math.max(0, Math.floor((Date.now() - base) / 1000)))
    const id = window.setInterval(tick, 1000)
    return () => window.clearInterval(id)
  }, [running])

  return elapsed
}
