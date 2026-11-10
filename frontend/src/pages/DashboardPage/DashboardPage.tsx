import { useEffect, useState } from 'react'
import { motion } from 'motion/react'
import { BookOpen, CalendarDays, ChevronRight, Dumbbell, LineChart, Play, type LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'
import { getLastSession } from '../../api/sessions'
import { getSplit, type SplitDay } from '../../api/split'
import { getWorkout, getWorkoutSummaries, type Workout, type WorkoutSummary } from '../../api/workouts'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/ui/Button'
import { ButtonLink } from '../../components/ui/ButtonLink'
import { Spinner } from '../../components/ui/Spinner'
import { spring } from '../../lib/motion'
import './DashboardPage.css'

interface JumpLink {
  to: string
  title: string
  icon: LucideIcon
}

const jumpLinks: JumpLink[] = [
  { to: '/workouts', title: 'Workouts', icon: Dumbbell },
  { to: '/split', title: 'Training split', icon: CalendarDays },
  { to: '/progress', title: 'Progression', icon: LineChart },
  { to: '/exercises', title: 'Exercise library', icon: BookOpen },
]

const DAY_NAMES = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

const setupSteps = [
  { title: 'Create a workout', description: 'Pick exercises and sets' },
  { title: 'Assign it to days', description: 'Optional, but it powers “today”' },
  { title: 'Log your first session', description: 'Start from today’s card' },
]

type Plan =
  | { kind: 'empty' }
  | { kind: 'today'; workout: Workout; lastSessionAt: string | null }
  | { kind: 'rest'; next: { dayName: string; workout: WorkoutSummary } | null }

type State = { status: 'loading' } | { status: 'error' } | { status: 'ready'; plan: Plan }

function todayNumber(): number {
  const jsDay = new Date().getDay()
  return jsDay === 0 ? 7 : jsDay
}

function nextScheduled(days: SplitDay[], today: number): { dayName: string; workout: WorkoutSummary } | null {
  for (let offset = 1; offset <= 7; offset++) {
    const number = ((today - 1 + offset) % 7) + 1
    const entry = days.find((day) => day.day === number)
    if (entry?.workout) {
      return { dayName: DAY_NAMES[number - 1], workout: entry.workout }
    }
  }
  return null
}

function formatSessionDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })
}

async function loadPlan(signal: AbortSignal): Promise<Plan> {
  const [days, summaries] = await Promise.all([
    getSplit(signal),
    getWorkoutSummaries({ sortBy: 'Name', direction: 'Ascending', page: 1, pageSize: 1 }, signal),
  ])
  if (summaries.totalCount === 0) {
    return { kind: 'empty' }
  }
  const today = todayNumber()
  const scheduled = days.find((day) => day.day === today)?.workout
  if (!scheduled) {
    return { kind: 'rest', next: nextScheduled(days, today) }
  }
  const [workout, last] = await Promise.all([
    getWorkout(scheduled.id, signal),
    getLastSession(scheduled.id, signal).catch(() => null),
  ])
  return { kind: 'today', workout, lastSessionAt: last?.startedAt ?? null }
}

export function DashboardPage() {
  const { user } = useAuth()
  const [state, setState] = useState<State>({ status: 'loading' })
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    loadPlan(controller.signal)
      .then((plan) => setState({ status: 'ready', plan }))
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [reloadKey])

  const dayName = DAY_NAMES[todayNumber() - 1]
  const retry = () => {
    setState({ status: 'loading' })
    setReloadKey((key) => key + 1)
  }

  return (
    <main className="page dashboard">
      {state.status === 'loading' ? <Spinner label="Loading your day…" /> : null}
      {state.status === 'error' ? (
        <section className="dashboard__hero" role="alert">
          <h1>Could not load your day</h1>
          <p>Check your connection and try again.</p>
          <Button variant="secondary" onClick={retry}>
            Try again
          </Button>
        </section>
      ) : null}
      {state.status === 'ready' ? (
        <motion.section
          className="dashboard__hero"
          aria-labelledby="dashboard-title"
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={spring}
        >
          {state.plan.kind === 'empty' ? (
            <>
              <p className="dashboard__eyebrow">Welcome, {user?.firstName}</p>
              <h1 id="dashboard-title">Let’s set up your first week</h1>
              <ol className="dashboard__steps">
                {setupSteps.map((step, index) => (
                  <li key={step.title}>
                    <span className="dashboard__step-number" aria-hidden="true">
                      {index + 1}
                    </span>
                    <div>
                      <strong>{step.title}</strong>
                      <span>{step.description}</span>
                    </div>
                  </li>
                ))}
              </ol>
              <ButtonLink to="/workouts" size="lg" fullWidth>
                Create your first workout
              </ButtonLink>
            </>
          ) : null}
          {state.plan.kind === 'today' ? (
            <>
              <p className="dashboard__eyebrow">{dayName}</p>
              <h1 id="dashboard-title">{state.plan.workout.name}</h1>
              <ul className="dashboard__exercises" aria-label="Exercises in today’s workout">
                {[...state.plan.workout.exercises]
                  .sort((a, b) => a.order - b.order)
                  .map((entry) => (
                    <li key={entry.exercise.id}>
                      <span>{entry.exercise.name}</span>
                      <small>
                        {entry.targetSets} {entry.targetSets === 1 ? 'set' : 'sets'}
                      </small>
                    </li>
                  ))}
              </ul>
              <ButtonLink to="/workout-execution" size="lg" fullWidth>
                <Play size={18} fill="currentColor" aria-hidden="true" />
                Start workout
              </ButtonLink>
              {state.plan.lastSessionAt ? (
                <p className="dashboard__hint">Last session: {formatSessionDate(state.plan.lastSessionAt)}</p>
              ) : null}
            </>
          ) : null}
          {state.plan.kind === 'rest' ? (
            <>
              <p className="dashboard__eyebrow">{dayName}</p>
              <h1 id="dashboard-title">Rest day</h1>
              {state.plan.next ? (
                <div className="dashboard__next">
                  <span>Next up</span>
                  <strong>
                    {state.plan.next.dayName} · {state.plan.next.workout.name}
                  </strong>
                </div>
              ) : (
                <p className="dashboard__hint">Nothing is scheduled yet. Assign your workouts to days in the split.</p>
              )}
              <ButtonLink to="/workout-execution" variant="secondary" size="lg" fullWidth>
                Start a workout anyway
              </ButtonLink>
              {!state.plan.next ? (
                <ButtonLink to="/split" variant="ghost" fullWidth>
                  Plan your week
                </ButtonLink>
              ) : null}
            </>
          ) : null}
        </motion.section>
      ) : null}

      <section aria-labelledby="jump-title">
        <h2 id="jump-title" className="dashboard__jump-title">
          Jump to
        </h2>
        <ul className="dashboard__jump">
          {jumpLinks.map((link) => {
            const Icon = link.icon
            return (
              <li key={link.to}>
                <Link to={link.to}>
                  <Icon size={20} aria-hidden="true" />
                  <span>{link.title}</span>
                  <ChevronRight size={18} aria-hidden="true" />
                </Link>
              </li>
            )
          })}
        </ul>
      </section>
    </main>
  )
}
