import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { Check, ChevronDown, Pencil, Trash2 } from 'lucide-react'
import { deleteWorkout, renameWorkout, type WorkoutSummary } from '../../api/workouts'
import { spring } from '../../lib/motion'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { useToast } from '../ui/ToastContext'
import { WorkoutEditor } from './WorkoutEditor'

const MAX_NAME_LENGTH = 100

interface WorkoutCardProps {
  workout: WorkoutSummary
  startExpanded?: boolean
  startRenaming?: boolean
  onChanged: () => void
  onDeleted: () => void
}

export function WorkoutCard({ workout, startExpanded = false, startRenaming = false, onChanged, onDeleted }: WorkoutCardProps) {
  const { showToast } = useToast()
  const [expanded, setExpanded] = useState(startExpanded)
  const [renaming, setRenaming] = useState(startRenaming)
  const [name, setName] = useState(workout.name)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [busy, setBusy] = useState(false)
  const cardRef = useRef<HTMLLIElement>(null)

  useEffect(() => {
    if (startExpanded) {
      const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
      cardRef.current?.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'center' })
    }
  }, [startExpanded])

  const trimmed = name.trim()
  const canRename = trimmed.length > 0 && trimmed.length <= MAX_NAME_LENGTH

  const saveName = async () => {
    if (!canRename || busy) {
      return
    }
    if (trimmed === workout.name) {
      setRenaming(false)
      return
    }
    setBusy(true)
    try {
      await renameWorkout(workout.id, trimmed)
      setRenaming(false)
      onChanged()
    } catch {
      showToast('error', 'Could not rename the workout. Try again.')
    } finally {
      setBusy(false)
    }
  }

  const cancelRename = () => {
    setName(workout.name)
    setRenaming(false)
  }

  const handleNameKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault()
      void saveName()
    } else if (event.key === 'Escape') {
      cancelRename()
    }
  }

  const remove = async () => {
    setBusy(true)
    try {
      await deleteWorkout(workout.id)
      onDeleted()
    } catch {
      showToast('error', 'Could not delete the workout. Try again.')
      setBusy(false)
    }
  }

  return (
    <li ref={cardRef} className={`workout-card${confirmingDelete ? ' workout-card--danger' : ''}`}>
      <div className="workout-card__row">
        {renaming ? (
          <div className="workout-card__rename">
            <Input
              aria-label="Workout name"
              autoFocus
              maxLength={MAX_NAME_LENGTH}
              value={name}
              onChange={(event) => setName(event.target.value)}
              onKeyDown={handleNameKeyDown}
            />
            <button type="button" className="icon-button icon-button--accent" aria-label="Save name" disabled={!canRename || busy} onClick={saveName}>
              <Check size={18} aria-hidden="true" />
            </button>
          </div>
        ) : (
          <>
            <button
              type="button"
              className="workout-card__toggle"
              aria-expanded={expanded}
              aria-label={`${workout.name}, ${workout.exerciseCount} ${workout.exerciseCount === 1 ? 'exercise' : 'exercises'}`}
              onClick={() => setExpanded((value) => !value)}
            >
              <span className="workout-card__title">
                <span className="workout-card__name">{workout.name}</span>
                {workout.mainMuscles.length > 0 ? (
                  <span className="workout-card__chips">
                    {workout.mainMuscles.map((muscle) => (
                      <span key={muscle} className="chip">
                        {muscle}
                      </span>
                    ))}
                  </span>
                ) : null}
              </span>
              <span className="pill">
                {workout.exerciseCount} {workout.exerciseCount === 1 ? 'exercise' : 'exercises'}
              </span>
              <motion.span animate={{ rotate: expanded ? 180 : 0 }} transition={spring} className="workout-card__chevron">
                <ChevronDown size={18} aria-hidden="true" />
              </motion.span>
            </button>
            <button type="button" className="icon-button" aria-label={`Rename ${workout.name}`} onClick={() => setRenaming(true)}>
              <Pencil size={16} aria-hidden="true" />
            </button>
            <button
              type="button"
              className="icon-button icon-button--danger"
              aria-label={`Delete ${workout.name}`}
              onClick={() => setConfirmingDelete(true)}
            >
              <Trash2 size={16} aria-hidden="true" />
            </button>
          </>
        )}
      </div>
      {confirmingDelete ? (
        <div className="workout-card__confirm" role="alertdialog" aria-label={`Delete ${workout.name}?`}>
          <p>
            <strong>Delete “{workout.name}”?</strong>
            <span>This can’t be undone.</span>
          </p>
          <div>
            <Button size="sm" variant="secondary" onClick={() => setConfirmingDelete(false)} disabled={busy}>
              Cancel
            </Button>
            <Button size="sm" variant="danger" onClick={remove} loading={busy}>
              Delete
            </Button>
          </div>
        </div>
      ) : null}
      <AnimatePresence initial={false}>
        {expanded && !renaming ? (
          <motion.div
            key="editor"
            className="workout-card__body"
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={spring}
          >
            <WorkoutEditor
              workoutId={workout.id}
              onSaved={() => {
                setExpanded(false)
                onChanged()
              }}
            />
          </motion.div>
        ) : null}
      </AnimatePresence>
    </li>
  )
}
