import { useCallback, useEffect, useRef, useState } from 'react'
import { ArrowRight, Check, Play, Square } from 'lucide-react'
import { Link } from 'react-router-dom'
import { WeightUnit } from '../../api/auth'
import { finishSession, getLastSession, saveExerciseSets, startSession, type LastSession } from '../../api/sessions'
import { getSplit } from '../../api/split'
import { getWorkout, getWorkoutSummaries, type Workout, type WorkoutSummary } from '../../api/workouts'
import { useAuth } from '../../auth/AuthContext'
import { RestTimerPill } from '../../components/Execution/RestTimerPill'
import { emptyDrafts, toEntries, type SetDraft } from '../../components/Execution/sets'
import { SetsCard } from '../../components/Execution/SetsCard'
import { WorkoutPickerModal } from '../../components/Execution/WorkoutPickerModal'
import { BackButton } from '../../components/ui/BackButton'
import { Button } from '../../components/ui/Button'
import { ButtonLink } from '../../components/ui/ButtonLink'
import { Spinner } from '../../components/ui/Spinner'
import { useToast } from '../../components/ui/ToastContext'
import { useRestTimer } from '../../hooks/useRestTimer'
import { useStopwatch } from '../../hooks/useStopwatch'
import { formatClock } from '../../lib/clock'
import './WorkoutExecutionPage.css'

const MAX_WORKOUTS = 100
const ADVANCE_DELAY_MS = 500
const DEFAULT_REST_SECONDS = 90

type Phase =
  | { name: 'loading' }
  | { name: 'error' }
  | { name: 'idle'; workout: WorkoutSummary | null }
  | { name: 'running'; workout: Workout; sessionId: string; last: LastSession }
  | { name: 'finished'; seconds: number }

function todayNumber(): number {
  const jsDay = new Date().getDay()
  return jsDay === 0 ? 7 : jsDay
}

export function WorkoutExecutionPage() {
  const { user } = useAuth()
  const { showToast } = useToast()
  const unit = user?.weightUnit ?? WeightUnit.Kilograms
  const restSeconds = user?.defaultRestTimeSeconds ?? DEFAULT_REST_SECONDS

  const [phase, setPhase] = useState<Phase>({ name: 'loading' })
  const [workouts, setWorkouts] = useState<WorkoutSummary[]>([])
  const [pickerOpen, setPickerOpen] = useState(false)
  const [starting, setStarting] = useState(false)
  const [busy, setBusy] = useState(false)
  const [index, setIndex] = useState(0)
  const [drafts, setDrafts] = useState<Record<string, SetDraft[]>>({})
  const [reloadKey, setReloadKey] = useState(0)
  const advanceTimer = useRef<number | undefined>(undefined)
  const rest = useRestTimer()
  const elapsed = useStopwatch(phase.name === 'running')

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([
      getSplit(controller.signal),
      getWorkoutSummaries({ sortBy: 'Name', direction: 'Ascending', page: 1, pageSize: MAX_WORKOUTS }, controller.signal),
    ])
      .then(([days, summaries]) => {
        const today = days.find((day) => day.day === todayNumber())
        setWorkouts(summaries.items)
        setPhase({ name: 'idle', workout: today?.workout ?? null })
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setPhase({ name: 'error' })
        }
      })
    return () => controller.abort()
  }, [reloadKey])

  useEffect(() => () => window.clearTimeout(advanceTimer.current), [])

  const begin = async (summary: WorkoutSummary) => {
    setStarting(true)
    try {
      const [workout, last, session] = await Promise.all([getWorkout(summary.id), getLastSession(summary.id), startSession(summary.id)])
      const initial: Record<string, SetDraft[]> = {}
      for (const entry of workout.exercises) {
        initial[entry.exercise.id] = emptyDrafts(entry.targetSets)
      }
      setDrafts(initial)
      setIndex(0)
      setPhase({ name: 'running', workout, sessionId: session.id, last })
    } catch {
      showToast('error', 'Could not start the workout. Try again.')
    } finally {
      setStarting(false)
    }
  }

  const saveCurrent = useCallback(
    async (workout: Workout, sessionId: string, position: number, current: Record<string, SetDraft[]>) => {
      const exerciseId = workout.exercises[position].exercise.id
      const entries = toEntries(current[exerciseId] ?? [], unit)
      if (entries.length > 0) {
        await saveExerciseSets(sessionId, exerciseId, entries)
      }
    },
    [unit],
  )

  const move = async (target: number) => {
    if (phase.name !== 'running') {
      return
    }
    window.clearTimeout(advanceTimer.current)
    setBusy(true)
    try {
      await saveCurrent(phase.workout, phase.sessionId, index, drafts)
      setIndex(target)
    } catch {
      showToast('error', 'Could not save your sets. Try again.')
    } finally {
      setBusy(false)
    }
  }

  const finish = async () => {
    if (phase.name !== 'running') {
      return
    }
    window.clearTimeout(advanceTimer.current)
    setBusy(true)
    try {
      await saveCurrent(phase.workout, phase.sessionId, index, drafts)
      await finishSession(phase.sessionId)
      rest.skip()
      setPhase({ name: 'finished', seconds: elapsed })
    } catch {
      showToast('error', 'Could not finish the workout. Try again.')
    } finally {
      setBusy(false)
    }
  }

  const patchSet = (exerciseId: string, row: number, patch: Partial<SetDraft>) => {
    setDrafts((current) => ({
      ...current,
      [exerciseId]: current[exerciseId].map((draft, position) => (position === row ? { ...draft, ...patch } : draft)),
    }))
  }

  const toggleSet = (exerciseId: string, row: number) => {
    if (phase.name !== 'running') {
      return
    }
    const current = drafts[exerciseId]
    const completing = !current[row].done
    patchSet(exerciseId, row, { done: completing })
    if (!completing) {
      return
    }
    rest.start(restSeconds)
    const allDone = current.every((draft, position) => position === row || draft.done)
    if (allDone && index < phase.workout.exercises.length - 1) {
      const next = index + 1
      const nextDrafts = {
        ...drafts,
        [exerciseId]: current.map((draft, position) => (position === row ? { ...draft, done: true } : draft)),
      }
      window.clearTimeout(advanceTimer.current)
      advanceTimer.current = window.setTimeout(async () => {
        try {
          await saveCurrent(phase.workout, phase.sessionId, index, nextDrafts)
          setIndex(next)
        } catch {
          showToast('error', 'Could not save your sets. Try again.')
        }
      }, ADVANCE_DELAY_MS)
    }
  }

  if (phase.name === 'loading') {
    return (
      <main className="page execution" aria-busy="true">
        <Spinner label="Loading your workout…" />
      </main>
    )
  }

  if (phase.name === 'error') {
    return (
      <main className="page execution">
        <BackButton />
        <div className="execution__card" role="alert">
          <h1>Could not load your workout</h1>
          <p className="execution__muted">Check your connection and try again.</p>
          <Button onClick={() => setReloadKey((key) => key + 1)}>Try again</Button>
        </div>
      </main>
    )
  }

  if (phase.name === 'finished') {
    return (
      <main className="page execution">
        <div className="execution__card">
          <span className="execution__badge" aria-hidden="true">
            <Check size={32} />
          </span>
          <h1>Workout finished</h1>
          <p className="execution__muted">Great work today. Total time {formatClock(phase.seconds)}</p>
          <ButtonLink to="/" size="lg" fullWidth>
            Return to menu
          </ButtonLink>
        </div>
      </main>
    )
  }

  if (phase.name === 'idle') {
    const selected = phase.workout
    return (
      <main className="page execution">
        <BackButton />
        <div className="execution__card">
          {selected ? (
            <>
              <p className="execution__eyebrow">Today</p>
              <h1>{selected.name}</h1>
              <p className="execution__muted">{selected.exerciseCount} exercises planned</p>
              <Button size="lg" fullWidth loading={starting} onClick={() => begin(selected)}>
                {starting ? null : <Play size={18} aria-hidden="true" />}
                {starting ? 'Loading' : 'Start workout'}
              </Button>
              <Button variant="secondary" fullWidth onClick={() => setPickerOpen(true)}>
                Switch workout
              </Button>
            </>
          ) : (
            <>
              <h1>Rest day</h1>
              <p className="execution__muted">Nothing is scheduled for today in your training split.</p>
              <Button size="lg" fullWidth onClick={() => setPickerOpen(true)}>
                Start a workout
              </Button>
              <ButtonLink to="/split" variant="secondary" fullWidth>
                View split
              </ButtonLink>
            </>
          )}
        </div>
        <WorkoutPickerModal
          open={pickerOpen}
          workouts={workouts}
          selectedId={selected?.id ?? null}
          onClose={() => setPickerOpen(false)}
          onSelect={(workout) => {
            setPickerOpen(false)
            setPhase({ name: 'idle', workout })
          }}
        />
      </main>
    )
  }

  const { workout, last } = phase
  const total = workout.exercises.length
  const exercise = workout.exercises[index].exercise
  const isLast = index === total - 1
  const previous = last.exercises.find((item) => item.exerciseId === exercise.id)?.sets

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
      <SetsCard
        drafts={drafts[exercise.id] ?? []}
        previous={previous}
        unit={unit}
        onChange={(row, patch) => patchSet(exercise.id, row, patch)}
        onToggle={(row) => toggleSet(exercise.id, row)}
      />
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
