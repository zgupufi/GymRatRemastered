import { useEffect, useRef, useState } from 'react'
import { ArrowRight, Check, ChevronLeft } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { WeightUnit } from '../../api/auth'
import { finishSession, saveExerciseSets, type LastSession } from '../../api/sessions'
import type { Workout } from '../../api/workouts'
import { useRestTimer } from '../../hooks/useRestTimer'
import { useStopwatch } from '../../hooks/useStopwatch'
import { formatClock } from '../../lib/clock'
import { fromKilograms, unitLabel } from '../../lib/weight'
import { Button } from '../ui/Button'
import { Modal } from '../ui/Modal'
import { useToast } from '../ui/ToastContext'
import { RestTimerPill } from './RestTimerPill'
import { emptyDrafts, summarize, toEntries, type SetDraft, type WorkoutResult } from './sets'
import { SetsCard } from './SetsCard'

const ADVANCE_DELAY_MS = 500

interface RunningWorkoutProps {
  workout: Workout
  sessionId: string
  last: LastSession
  unit: WeightUnit
  restSeconds: number
  onFinished: (seconds: number, result: WorkoutResult) => void
}

function initialDrafts(workout: Workout): Record<string, SetDraft[]> {
  const drafts: Record<string, SetDraft[]> = {}
  for (const entry of workout.exercises) {
    drafts[entry.exercise.id] = emptyDrafts(entry.targetSets)
  }
  return drafts
}

export function RunningWorkout({ workout, sessionId, last, unit, restSeconds, onFinished }: RunningWorkoutProps) {
  const { showToast } = useToast()
  const [index, setIndex] = useState(0)
  const [drafts, setDrafts] = useState(() => initialDrafts(workout))
  const [busy, setBusy] = useState(false)
  const [confirmingEnd, setConfirmingEnd] = useState(false)
  const advanceTimer = useRef<number | undefined>(undefined)
  const saveQueue = useRef<Promise<void>>(Promise.resolve())
  const savedExercises = useRef(new Set<string>())
  const finished = useRef(false)
  const closeTimer = useRef<number | undefined>(undefined)
  const rest = useRestTimer()
  const elapsed = useStopwatch(true)

  useEffect(() => () => window.clearTimeout(advanceTimer.current), [])

  useEffect(() => {
    window.clearTimeout(closeTimer.current)
    return () => {
      closeTimer.current = window.setTimeout(() => {
        if (finished.current) {
          return
        }
        finished.current = true
        void saveQueue.current
          .then(() => (savedExercises.current.size > 0 ? finishSession(sessionId) : undefined))
          .catch(() => undefined)
      }, 0)
    }
  }, [sessionId])

  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [])

  const total = workout.exercises.length
  const exercise = workout.exercises[index].exercise
  const isLast = index === total - 1
  const previous = last.exercises.find((item) => item.exerciseId === exercise.id)?.sets
  const startedCount = workout.exercises.filter((entry) => drafts[entry.exercise.id].some((draft) => draft.done)).length
  const lastTime = previous && previous.length > 0 ? [...previous].sort((a, b) => a.setNumber - b.setNumber) : null
  const detail = [exercise.mainMuscle, exercise.equipment].filter(Boolean).join(' · ')

  const saveExercise = async (position: number, current: Record<string, SetDraft[]>) => {
    const exerciseId = workout.exercises[position].exercise.id
    const entries = toEntries(current[exerciseId], unit)
    if (entries.length > 0 || savedExercises.current.has(exerciseId)) {
      await saveExerciseSets(sessionId, exerciseId, entries)
      if (entries.length > 0) {
        savedExercises.current.add(exerciseId)
      } else {
        savedExercises.current.delete(exerciseId)
      }
    }
  }

  const move = async (target: number) => {
    window.clearTimeout(advanceTimer.current)
    setBusy(true)
    try {
      await saveExercise(index, drafts)
      setIndex(target)
    } catch {
      showToast('error', 'Could not save your sets. Try again.')
    } finally {
      setBusy(false)
    }
  }

  const finish = async () => {
    window.clearTimeout(advanceTimer.current)
    setBusy(true)
    try {
      await saveExercise(index, drafts)
      finished.current = true
      await finishSession(sessionId)
      rest.skip()
      onFinished(
        elapsed,
        summarize(
          workout.exercises.map((entry) => entry.exercise),
          drafts,
          unit,
        ),
      )
    } catch {
      finished.current = false
      showToast('error', 'Could not finish the workout. Try again.')
      setBusy(false)
    }
  }

  const persistSets = (position: number, current: Record<string, SetDraft[]>) => {
    saveQueue.current = saveQueue.current
      .then(() => saveExercise(position, current))
      .catch(() => showToast('error', 'Could not save your sets. Try again.'))
  }

  const patchSet = (row: number, patch: Partial<SetDraft>) => {
    setDrafts((current) => ({
      ...current,
      [exercise.id]: current[exercise.id].map((draft, position) => (position === row ? { ...draft, ...patch } : draft)),
    }))
  }

  const toggleSet = (row: number) => {
    const current = drafts[exercise.id]
    const completing = !current[row].done
    window.clearTimeout(advanceTimer.current)
    patchSet(row, { done: completing })
    if (!completing) {
      persistSets(
        index,
        { ...drafts, [exercise.id]: current.map((draft, position) => (position === row ? { ...draft, done: false } : draft)) },
      )
      return
    }
    rest.start(restSeconds)
    const updated = current.map((draft, position) => (position === row ? { ...draft, done: true } : draft))
    persistSets(index, { ...drafts, [exercise.id]: updated })
    if (updated.every((draft) => draft.done) && !isLast) {
      const nextDrafts = { ...drafts, [exercise.id]: updated }
      advanceTimer.current = window.setTimeout(async () => {
        try {
          await saveExercise(index, nextDrafts)
          setIndex(index + 1)
        } catch {
          showToast('error', 'Could not save your sets. Try again.')
        }
      }, ADVANCE_DELAY_MS)
    }
  }

  return (
    <main className="page execution">
      <div
        className="execution__progress"
        role="progressbar"
        aria-label="Workout progress"
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={index + 1}
      >
        <i style={{ width: `${((index + 1) / total) * 100}%` }} />
      </div>
      <header className="execution__top">
        <button type="button" className="execution__back" aria-label="Leave workout" onClick={() => setConfirmingEnd(true)}>
          <ChevronLeft size={20} aria-hidden="true" />
        </button>
        <span className="execution__clock" aria-label="Elapsed time">
          {formatClock(elapsed)}
        </span>
      </header>
      <p className="execution__eyebrow">
        Exercise {index + 1} of {total}
      </p>
      <h2 className="execution__exercise">
        <Link to={`/exercises/${exercise.id}`}>{exercise.name}</Link>
      </h2>
      {detail ? <p className="execution__detail">{detail}</p> : null}
      {lastTime ? (
        <p className="execution__last">
          Last time ({unitLabel(unit)}): {lastTime.map((set) => `${fromKilograms(set.weightKg, unit)} × ${set.reps}`).join(' · ')}
        </p>
      ) : null}
      <SetsCard drafts={drafts[exercise.id]} previous={previous} unit={unit} onChange={patchSet} onToggle={toggleSet} />
      <RestTimerPill remaining={rest.remaining} onExtend={rest.extend} onReduce={rest.reduce} onSkip={rest.skip} />
      <div className="execution__controls">
        <Button variant="danger" disabled={busy} onClick={() => setConfirmingEnd(true)}>
          End
        </Button>
        {index > 0 ? (
          <Button variant="secondary" disabled={busy} onClick={() => move(index - 1)}>
            Previous
          </Button>
        ) : null}
        {isLast ? (
          <Button className="execution__next" loading={busy} onClick={finish}>
            Finish
            <Check size={18} aria-hidden="true" />
          </Button>
        ) : (
          <Button className="execution__next" disabled={busy} onClick={() => move(index + 1)}>
            Next
            <ArrowRight size={18} aria-hidden="true" />
          </Button>
        )}
      </div>
      <Modal open={confirmingEnd} title="End this workout?" onClose={() => setConfirmingEnd(false)}>
        <p className="execution__muted">
          {startedCount} of {total} {total === 1 ? 'exercise' : 'exercises'} started. Sets you logged will be saved.
        </p>
        <div className="execution__confirm">
          <Button size="lg" fullWidth onClick={() => setConfirmingEnd(false)}>
            Keep going
          </Button>
          <Button
            variant="danger"
            size="lg"
            fullWidth
            loading={busy}
            onClick={() => {
              setConfirmingEnd(false)
              void finish()
            }}
          >
            End workout
          </Button>
        </div>
      </Modal>
    </main>
  )
}
