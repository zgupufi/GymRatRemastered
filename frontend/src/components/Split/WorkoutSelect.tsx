import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import { ChevronDown } from 'lucide-react'
import { motion } from 'motion/react'
import { spring } from '../../lib/motion'
import type { WorkoutSummary } from '../../api/workouts'

interface WorkoutSelectProps {
  label: string
  workouts: WorkoutSummary[]
  value: string | null
  onChange: (workoutId: string | null) => void
}

export function WorkoutSelect({ label, workouts, value, onChange }: WorkoutSelectProps) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const listId = useId()
  const current = workouts.find((workout) => workout.id === value)

  useEffect(() => {
    if (!open) {
      return
    }
    const close = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('pointerdown', close)
    return () => document.removeEventListener('pointerdown', close)
  }, [open])

  const choose = (workoutId: string | null) => {
    onChange(workoutId)
    setOpen(false)
  }

  const options = () => Array.from(rootRef.current?.querySelectorAll<HTMLElement>('[role="option"]') ?? [])

  const focusOption = (index: number) => {
    const items = options()
    items[Math.max(0, Math.min(items.length - 1, index))]?.focus()
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      setOpen(false)
      rootRef.current?.querySelector<HTMLElement>('button')?.focus()
      return
    }
    const step = event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0
    if (step === 0 && event.key !== 'Home' && event.key !== 'End') {
      return
    }
    event.preventDefault()
    if (!open) {
      setOpen(true)
      return
    }
    const items = options()
    const index = items.indexOf(document.activeElement as HTMLElement)
    if (event.key === 'Home') {
      focusOption(0)
    } else if (event.key === 'End') {
      focusOption(items.length - 1)
    } else {
      focusOption(index === -1 ? (step === 1 ? 0 : items.length - 1) : index + step)
    }
  }

  const optionKeyDown = (workoutId: string | null) => (event: KeyboardEvent<HTMLLIElement>) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      choose(workoutId)
    }
  }

  return (
    <div ref={rootRef} className="workout-select" onKeyDown={handleKeyDown}>
      <button
        type="button"
        className="workout-select__button"
        aria-label={label}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listId : undefined}
        onClick={() => setOpen((state) => !state)}
      >
        <span className={current ? undefined : 'workout-select__rest'}>{current ? current.name : 'Rest day'}</span>
        <motion.span animate={{ rotate: open ? 180 : 0 }} transition={spring} className="workout-select__chevron">
          <ChevronDown size={18} aria-hidden="true" />
        </motion.span>
      </button>
      {open ? (
        <motion.ul
          id={listId}
          role="listbox"
          aria-label={label}
          className="workout-select__menu"
          initial={{ opacity: 0, scale: 0.97, y: -4 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          transition={spring}
        >
          <li
            role="option"
            aria-selected={value === null}
            className="workout-select__option workout-select__option--rest"
            tabIndex={0}
            onClick={() => choose(null)}
            onKeyDown={optionKeyDown(null)}
          >
            Rest day
          </li>
          {workouts.map((workout) => (
            <li
              key={workout.id}
              role="option"
              aria-selected={workout.id === value}
              className="workout-select__option"
              tabIndex={0}
              onClick={() => choose(workout.id)}
              onKeyDown={optionKeyDown(workout.id)}
            >
              {workout.name}
            </li>
          ))}
        </motion.ul>
      ) : null}
    </div>
  )
}
