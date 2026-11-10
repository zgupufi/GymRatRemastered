import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { Check, ChevronDown, Ellipsis, Pencil, Trash2 } from 'lucide-react'
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
  onChanged: () => void
  onDeleted: () => void
}

export function WorkoutCard({ workout, startExpanded = false, onChanged, onDeleted }: WorkoutCardProps) {
  const { showToast } = useToast()
  const [expanded, setExpanded] = useState(startExpanded)
  const [renaming, setRenaming] = useState(false)
  const [name, setName] = useState(workout.name)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [busy, setBusy] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const cardRef = useRef<HTMLLIElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!menuOpen) {
      return
    }
    const close = (event: MouseEvent | globalThis.KeyboardEvent) => {
      if (event instanceof KeyboardEvent) {
        if (event.key === 'Escape') {
          setMenuOpen(false)
        }
        return
      }
      if (!menuRef.current?.contains(event.target as Node)) {
        setMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', close)
    }
  }, [menuOpen])

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
              onClick={() => setExpanded((value) => !value)}
            >
              <span className="workout-card__title">
                <span className="workout-card__name">{workout.name}</span>
              </span>
              <motion.span animate={{ rotate: expanded ? 180 : 0 }} transition={spring} className="workout-card__chevron">
                <ChevronDown size={18} aria-hidden="true" />
              </motion.span>
            </button>
            <div className="workout-card__menu" ref={menuRef}>
              <button
                type="button"
                className="icon-button"
                aria-label={`More actions for ${workout.name}`}
                aria-haspopup="menu"
                aria-expanded={menuOpen}
                onClick={() => setMenuOpen((value) => !value)}
              >
                <Ellipsis size={18} aria-hidden="true" />
              </button>
              {menuOpen ? (
                <div className="workout-card__popover" role="menu">
                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => {
                      setMenuOpen(false)
                      setRenaming(true)
                    }}
                  >
                    <Pencil size={16} aria-hidden="true" />
                    Rename
                  </button>
                  <button
                    type="button"
                    role="menuitem"
                    className="workout-card__popover-danger"
                    onClick={() => {
                      setMenuOpen(false)
                      setConfirmingDelete(true)
                    }}
                  >
                    <Trash2 size={16} aria-hidden="true" />
                    Delete
                  </button>
                </div>
              ) : null}
            </div>
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
            <WorkoutEditor workoutId={workout.id} />
          </motion.div>
        ) : null}
      </AnimatePresence>
    </li>
  )
}
