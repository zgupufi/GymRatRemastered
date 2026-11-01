import { useEffect, useMemo, useRef, useState } from 'react'
import { Check } from 'lucide-react'
import { assignSplitDay, getSplit } from '../../api/split'
import { getWorkoutSummaries, type WorkoutSummary } from '../../api/workouts'
import { DayCard } from '../../components/Split/DayCard'
import { BackButton } from '../../components/ui/BackButton'
import { Button } from '../../components/ui/Button'
import { Spinner } from '../../components/ui/Spinner'
import { useToast } from '../../components/ui/ToastContext'
import './SplitPage.css'

const DAY_NAMES = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']
const MAX_WORKOUTS = 100

type Selection = Record<number, string | null>

type State =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'loaded'; workouts: WorkoutSummary[]; saved: Selection }

function todayNumber(): number {
  const jsDay = new Date().getDay()
  return jsDay === 0 ? 7 : jsDay
}

export function SplitPage() {
  const { showToast } = useToast()
  const [state, setState] = useState<State>({ status: 'loading' })
  const [selection, setSelection] = useState<Selection>({})
  const [reloadKey, setReloadKey] = useState(0)
  const [saving, setSaving] = useState(false)
  const todayRef = useRef<HTMLLIElement>(null)
  const today = todayNumber()

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([
      getSplit(controller.signal),
      getWorkoutSummaries({ sortBy: 'Name', direction: 'Ascending', page: 1, pageSize: MAX_WORKOUTS }, controller.signal),
    ])
      .then(([days, workouts]) => {
        const saved: Selection = {}
        for (const day of days) {
          saved[day.day] = day.workout?.id ?? null
        }
        setSelection(saved)
        setState({ status: 'loaded', workouts: workouts.items, saved })
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [reloadKey])

  const loaded = state.status === 'loaded'

  useEffect(() => {
    if (loaded) {
      const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
      todayRef.current?.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'center' })
    }
  }, [loaded])

  const changedDays = useMemo(() => {
    if (state.status !== 'loaded') {
      return []
    }
    return DAY_NAMES.map((_, index) => index + 1).filter((day) => (selection[day] ?? null) !== (state.saved[day] ?? null))
  }, [state, selection])

  const save = async () => {
    if (state.status !== 'loaded') {
      return
    }
    setSaving(true)
    try {
      await Promise.all(changedDays.map((day) => assignSplitDay(day, selection[day] ?? null)))
      setState({ ...state, saved: { ...selection } })
      showToast('success', 'Split saved')
    } catch {
      showToast('error', 'Could not save your split. Try again.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <main className="page split-page">
      <BackButton />
      <header className="page__header split-page__header">
        <div>
          <h1>My split</h1>
          <p>Assign a workout to each day of the week, or leave it as a rest day.</p>
        </div>
        <Button onClick={save} loading={saving} disabled={state.status !== 'loaded' || changedDays.length === 0}>
          <Check size={18} aria-hidden="true" />
          Save split
        </Button>
      </header>

      {state.status === 'loading' ? <Spinner label="Loading your split…" /> : null}
      {state.status === 'error' ? (
        <div className="split-page__empty" role="alert">
          <strong>Could not load your split</strong>
          <p>Check your connection and try again.</p>
          <Button size="sm" variant="secondary" onClick={() => setReloadKey((key) => key + 1)}>
            Try again
          </Button>
        </div>
      ) : null}
      {state.status === 'loaded' ? (
        <ul className="split-page__grid">
          {DAY_NAMES.map((name, index) => {
            const day = index + 1
            return (
              <DayCard
                key={day}
                ref={day === today ? todayRef : undefined}
                name={name}
                isToday={day === today}
                workouts={state.workouts}
                workoutId={selection[day] ?? null}
                onChange={(workoutId) => setSelection((current) => ({ ...current, [day]: workoutId }))}
              />
            )
          })}
        </ul>
      ) : null}
    </main>
  )
}
