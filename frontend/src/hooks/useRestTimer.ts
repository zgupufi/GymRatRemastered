import { useCallback, useEffect, useRef, useState } from 'react'
import { prepareRestSignal, readSoundEnabled, signalRestOver, writeSoundEnabled } from '../lib/restSignal'

const EXTEND_SECONDS = 30
const REDUCE_SECONDS = 10
const TICK_MS = 250
const FINISHED_VISIBLE_MS = 4000

function secondsLeft(endAt: number, now: number) {
  return Math.max(0, Math.ceil((endAt - now) / 1000))
}

export function useRestTimer() {
  const [endAt, setEndAt] = useState<number | null>(null)
  const [now, setNow] = useState(() => Date.now())
  const [finished, setFinished] = useState(false)
  const [soundEnabled, setSoundEnabled] = useState(readSoundEnabled)
  const endAtRef = useRef<number | null>(null)
  const soundRef = useRef(soundEnabled)
  const active = endAt !== null

  const setEnd = useCallback((value: number | null) => {
    endAtRef.current = value
    setEndAt(value)
    setNow(Date.now())
  }, [])

  const check = useCallback(() => {
    const end = endAtRef.current
    if (end === null) {
      return
    }
    const current = Date.now()
    if (current >= end) {
      setEnd(null)
      setFinished(true)
      signalRestOver(soundRef.current)
    } else {
      setNow(current)
    }
  }, [setEnd])

  useEffect(() => {
    if (!active) {
      return
    }
    const id = window.setInterval(check, TICK_MS)
    document.addEventListener('visibilitychange', check)
    return () => {
      window.clearInterval(id)
      document.removeEventListener('visibilitychange', check)
    }
  }, [active, check])

  useEffect(() => {
    if (!finished) {
      return
    }
    const id = window.setTimeout(() => setFinished(false), FINISHED_VISIBLE_MS)
    return () => window.clearTimeout(id)
  }, [finished])

  const start = useCallback(
    (seconds: number) => {
      setFinished(false)
      if (seconds > 0) {
        prepareRestSignal()
        setEnd(Date.now() + seconds * 1000)
      } else {
        setEnd(null)
      }
    },
    [setEnd],
  )

  const extend = useCallback(() => {
    if (endAtRef.current !== null) {
      setEnd(endAtRef.current + EXTEND_SECONDS * 1000)
    }
  }, [setEnd])

  const reduce = useCallback(() => {
    if (endAtRef.current !== null) {
      const next = endAtRef.current - REDUCE_SECONDS * 1000
      setEnd(next - Date.now() <= 0 ? null : next)
    }
  }, [setEnd])

  const skip = useCallback(() => {
    setFinished(false)
    setEnd(null)
  }, [setEnd])

  const toggleSound = useCallback(() => {
    const next = !soundRef.current
    soundRef.current = next
    setSoundEnabled(next)
    writeSoundEnabled(next)
  }, [])

  const remaining = endAt === null ? null : secondsLeft(endAt, now)

  return { remaining, finished, soundEnabled, start, extend, reduce, skip, toggleSound }
}
