import { useEffect, useState } from 'react'
import { Check, Play, Target } from 'lucide-react'
import { WeightUnit } from '../../api/auth'
import { getLastSession, startSession, type LastSession } from '../../api/sessions'
import { getSplit } from '../../api/split'
import { getWorkout, getWorkoutSummaries, type Workout, type WorkoutSummary } from '../../api/workouts'
import { useAuth } from '../../auth/AuthContext'
import { RunningWorkout } from '../../components/Execution/RunningWorkout'
import { WorkoutPickerModal } from '../../components/Execution/WorkoutPickerModal'
import { BackButton } from '../../components/ui/BackButton'
import { Button } from '../../components/ui/Button'
import { ButtonLink } from '../../components/ui/ButtonLink'
import { Spinner } from '../../components/ui/Spinner'
import { useToast } from '../../components/ui/ToastContext'
import { formatClock } from '../../lib/clock'
import './WorkoutExecutionPage.css'

const MAX_WORKOUTS = 100
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
  const [reloadKey, setReloadKey] = useState(0)

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

  const begin = async (summary: WorkoutSummary) => {
    setStarting(true)
    try {
      const [workout, last, session] = await Promise.all([getWorkout(summary.id), getLastSession(summary.id), startSession(summary.id)])
      setPhase({ name: 'running', workout, sessionId: session.id, last })
    } catch {
      showToast('error', 'Could not start the workout. Try again.')
    } finally {
      setStarting(false)
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

  if (phase.name === 'running') {
    return (
      <RunningWorkout
        workout={phase.workout}
        sessionId={phase.sessionId}
        last={phase.last}
        unit={unit}
        restSeconds={restSeconds}
        onFinished={(seconds) => setPhase({ name: 'finished', seconds })}
      />
    )
  }

  if (phase.name === 'finished') {
    return (
      <main className="page execution">
        <BackButton />
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
            <span className="execution__badge execution__badge--neutral" aria-hidden="true">
              <Target size={32} />
            </span>
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
