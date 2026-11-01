import { useEffect, useRef, useState } from 'react'
import { ArrowRight, Check, Square } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { WeightUnit } from '../../api/auth'
import { finishSession, saveExerciseSets, type LastSession } from '../../api/sessions'
import type { Workout } from '../../api/workouts'
import { useRestTimer } from '../../hooks/useRestTimer'
import { useStopwatch } from '../../hooks/useStopwatch'
import { formatClock } from '../../lib/clock'
import { BackButton } from '../ui/BackButton'
import { Button } from '../ui/Button'
import { useToast } from '../ui/ToastContext'
import { RestTimerPill } from './RestTimerPill'
import { emptyDrafts, toEntries, type SetDraft } from './sets'
import { SetsCard } from './SetsCard'

const ADVANCE_DELAY_MS = 500

interface RunningWorkoutProps {
  workout: Workout
  sessionId: string
  last: LastSession
  unit: WeightUnit
  restSeconds: number
  onFinished: (seconds: number) => void
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
  const advanceTimer = useRef<number | undefined>(undefined)
  const rest = useRestTimer()
  const elapsed = useStopwatch(true)

  useEffect(() => () => window.clearTimeout(advanceTimer.current), [])

  const total = workout.exercises.length
  const exercise = workout.exercises[index].exercise
  const isLast = index === total - 1
  const previous = last.exercises.find((item) => item.exerciseId === exercise.id)?.sets

  const saveExercise = async (position: number, current: Record<string, SetDraft[]>) => {
    const exerciseId = workout.exercises[position].exercise.id
    const entries = toEntries(current[exerciseId], unit)
    if (entries.length > 0) {
      await saveExerciseSets(sessionId, exerciseId, entries)
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
      await finishSession(sessionId)
      rest.skip()
      onFinished(elapsed)
    } catch {
      showToast('error', 'Could not finish the workout. Try again.')
      setBusy(false)
    }
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
      return
    }
    rest.start(restSeconds)
    const updated = current.map((draft, position) => (position === row ? { ...draft, done: true } : draft))
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
        <BackButton />
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
      <SetsCard drafts={drafts[exercise.id]} previous={previous} unit={unit} onChange={patchSet} onToggle={toggleSet} />
      <RestTimerPill remaining={rest.remaining} onExtend={rest.extend} onReduce={rest.reduce} onSkip={rest.skip} />
      <div className="execution__controls">
        <Button variant="danger" aria-label="Stop workout" disabled={busy} onClick={finish}>
          <Square size={16} aria-hidden="true" />
        </Button>
        {index > 0 ? (
          <Button variant="secondary" disabled={busy} onClick={() => move(index - 1)}>
            Previous
          </Button>
        ) : null}
        {isLast ? (
          <Button variant="blue" className="execution__next" loading={busy} onClick={finish}>
            Finish
            <Check size={18} aria-hidden="true" />
          </Button>
        ) : (
          <Button variant="blue" className="execution__next" disabled={busy} onClick={() => move(index + 1)}>
            Next
            <ArrowRight size={18} aria-hidden="true" />
          </Button>
        )}
      </div>
    </main>
  )
}
