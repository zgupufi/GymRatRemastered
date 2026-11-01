import { useCallback, useEffect, useState } from 'react'

const EXTEND_SECONDS = 30
const REDUCE_SECONDS = 10

export function useRestTimer() {
  const [remaining, setRemaining] = useState<number | null>(null)
  const active = remaining !== null

  useEffect(() => {
    if (!active) {
      return
    }
    const id = window.setInterval(() => {
      setRemaining((current) => (current === null || current <= 1 ? null : current - 1))
    }, 1000)
    return () => window.clearInterval(id)
  }, [active])

  const start = useCallback((seconds: number) => setRemaining(seconds > 0 ? seconds : null), [])
  const extend = useCallback(() => setRemaining((current) => (current === null ? null : current + EXTEND_SECONDS)), [])
  const reduce = useCallback(
    () => setRemaining((current) => (current === null ? null : current - REDUCE_SECONDS <= 0 ? null : current - REDUCE_SECONDS)),
    [],
  )
  const skip = useCallback(() => setRemaining(null), [])

  return { remaining, start, extend, reduce, skip }
}
