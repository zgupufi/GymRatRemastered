import { useEffect, useRef } from 'react'

const EVENT_NAME = 'gymrat:workouts-changed'

export function announceWorkoutsChanged(): void {
  window.dispatchEvent(new Event(EVENT_NAME))
}

export function useWorkoutsChanged(onChanged: () => void): void {
  const callbackRef = useRef(onChanged)

  useEffect(() => {
    callbackRef.current = onChanged
  })

  useEffect(() => {
    const listener = () => callbackRef.current()
    window.addEventListener(EVENT_NAME, listener)
    return () => window.removeEventListener(EVENT_NAME, listener)
  }, [])
}
