import { useEffect, useRef, useState } from 'react'
import { Check } from 'lucide-react'
import { friendlyErrorMessage } from '../../api/errors'
import { getWorkout, getWorkoutSummaries, replaceWorkoutExercises, type WorkoutSummary } from '../../api/workouts'
import { Modal } from '../ui/Modal'
import { Spinner } from '../ui/Spinner'
import { useToast } from '../ui/ToastContext'
import './AddToWorkoutModal.css'

const DEFAULT_TARGET_SETS = 3
const CLOSE_DELAY_MS = 1500

interface AddToWorkoutModalProps {
  open: boolean
  exerciseId: string
  onClose: () => void
}

type Status = 'loading' | 'ready' | 'error'

function WorkoutChoices({ exerciseId, onClose }: Omit<AddToWorkoutModalProps, 'open'>) {
  const { showToast } = useToast()
  const [status, setStatus] = useState<Status>('loading')
  const [workouts, setWorkouts] = useState<WorkoutSummary[]>([])
  const [addedId, setAddedId] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)
  const closeTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => {
    const request = new AbortController()
    getWorkoutSummaries(request.signal)
      .then((result) => {
        setWorkouts(result.items)
        setStatus('ready')
      })
      .catch(() => {
        if (!request.signal.aborted) {
          setStatus('error')
        }
      })
    return () => request.abort()
  }, [])

  useEffect(() => () => clearTimeout(closeTimer.current), [])

  const add = async (workout: WorkoutSummary) => {
    setBusyId(workout.id)
    try {
      const current = await getWorkout(workout.id)
      const requests = current.exercises.map((entry) => ({
        exerciseId: entry.exercise.id,
        targetSets: entry.targetSets,
      }))
      if (!requests.some((entry) => entry.exerciseId === exerciseId)) {
        requests.push({ exerciseId, targetSets: DEFAULT_TARGET_SETS })
        await replaceWorkoutExercises(workout.id, requests)
      }
      setAddedId(workout.id)
      closeTimer.current = setTimeout(onClose, CLOSE_DELAY_MS)
    } catch (error) {
      showToast('error', friendlyErrorMessage(error))
    } finally {
      setBusyId(null)
    }
  }

  if (status === 'loading') {
    return <Spinner label="Loading workouts…" />
  }
  if (status === 'error') {
    return <p role="alert">Could not load your workouts.</p>
  }
  if (workouts.length === 0) {
    return <p className="add-to-workout__empty">No workouts available. Create one in Workouts page!</p>
  }
  return (
    <ul className="add-to-workout__list">
      {workouts.map((workout) => {
        const added = addedId === workout.id
        return (
          <li key={workout.id}>
            <button
              type="button"
              className={`add-to-workout__row${added ? ' add-to-workout__row--added' : ''}`}
              disabled={busyId !== null || addedId !== null}
              onClick={() => add(workout)}
            >
              <span>{workout.name}</span>
              {added ? (
                <span className="add-to-workout__added">
                  <Check size={16} aria-hidden="true" />
                  Added
                </span>
              ) : (
                <span className="add-to-workout__meta">
                  {workout.exerciseCount} {workout.exerciseCount === 1 ? 'exercise' : 'exercises'}
                </span>
              )}
            </button>
          </li>
        )
      })}
    </ul>
  )
}

export function AddToWorkoutModal({ open, exerciseId, onClose }: AddToWorkoutModalProps) {
  return (
    <Modal open={open} title="Choose workout" onClose={onClose}>
      <WorkoutChoices exerciseId={exerciseId} onClose={onClose} />
    </Modal>
  )
}
