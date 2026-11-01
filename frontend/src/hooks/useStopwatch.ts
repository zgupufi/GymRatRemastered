import { useEffect, useRef, useState } from 'react'

export function useStopwatch(running: boolean) {
  const [elapsed, setElapsed] = useState(0)
  const startedAt = useRef<number | null>(null)

  useEffect(() => {
    if (!running) {
      return
    }
    const base = startedAt.current ?? Date.now()
    startedAt.current = base
    const tick = () => setElapsed(Math.floor((Date.now() - base) / 1000))
    const id = window.setInterval(tick, 1000)
    return () => window.clearInterval(id)
  }, [running])

  return elapsed
}
