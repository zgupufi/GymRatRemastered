import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import { searchExercises, type Exercise } from '../../api/exercises'
import { getExerciseProgress, getSplit, type ProgressPoint } from '../../api/progress'
import { getWorkout } from '../../api/workouts'
import { ProgressChart } from '../../components/Progression/ProgressChart'
import { BackButton } from '../../components/ui/BackButton'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Input } from '../../components/ui/Input'
import { Spinner } from '../../components/ui/Spinner'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import './ProgressionPage.css'

const SEARCH_DELAY_MS = 300
const SUGGESTION_COUNT = 6
const TODAY_COUNT = 5

interface Choice {
  id: string
  name: string
}

type SuggestionState = { status: 'loading' } | { status: 'error' } | { status: 'loaded'; items: Exercise[] }

type ProgressState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'loaded'; points: ProgressPoint[] }

function todaySplitDay(): number {
  return ((new Date().getDay() + 6) % 7) + 1
}

export function ProgressionPage() {
  const [term, setTerm] = useState('')
  const [selected, setSelected] = useState<Choice | null>(null)
  const [today, setToday] = useState<Choice[]>([])
  const [suggestions, setSuggestions] = useState<SuggestionState>({ status: 'loading' })
  const [progress, setProgress] = useState<ProgressState>({ status: 'loading' })
  const [reloadKey, setReloadKey] = useState(0)
  const debouncedTerm = useDebouncedValue(term.trim(), SEARCH_DELAY_MS)
  const selectedId = selected?.id

  useEffect(() => {
    const controller = new AbortController()
    getSplit(controller.signal)
      .then((split) => {
        const workout = split.find((entry) => entry.day === todaySplitDay())?.workout
        return workout ? getWorkout(workout.id, controller.signal) : null
      })
      .then((workout) => {
        if (workout) {
          setToday(
            workout.exercises.slice(0, TODAY_COUNT).map((entry) => ({ id: entry.exercise.id, name: entry.exercise.name })),
          )
        }
      })
      .catch(() => undefined)
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (selectedId || !debouncedTerm) {
      return
    }
    const controller = new AbortController()
    searchExercises({ term: debouncedTerm, pageSize: SUGGESTION_COUNT }, controller.signal)
      .then((result) => setSuggestions({ status: 'loaded', items: result.items }))
      .catch(() => {
        if (!controller.signal.aborted) {
          setSuggestions({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [selectedId, debouncedTerm])

  useEffect(() => {
    if (!selectedId) {
      return
    }
    const controller = new AbortController()
    getExerciseProgress(selectedId, controller.signal)
      .then((points) => setProgress({ status: 'loaded', points }))
      .catch(() => {
        if (!controller.signal.aborted) {
          setProgress({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [selectedId, reloadKey])

  const changeTerm = (value: string) => {
    setTerm(value)
    setSelected(null)
    setSuggestions({ status: 'loading' })
  }

  const choose = (choice: Choice) => {
    setTerm(choice.name)
    setSelected(choice)
    setProgress({ status: 'loading' })
  }

  const retry = () => {
    setProgress({ status: 'loading' })
    setReloadKey((key) => key + 1)
  }

  const showSuggestions = !selected && debouncedTerm !== ''

  return (
    <main className="page progression-page">
      <BackButton />
      <header className="page__header">
        <h1>Progression</h1>
        <p>Track your evolution and break plateaus.</p>
      </header>

      <Card tone="material" className="progression-page__panel">
        <Input
          label="Exercise"
          placeholder="Search exercises…"
          icon={<Search size={18} />}
          value={term}
          onChange={(event) => changeTerm(event.target.value)}
          autoComplete="off"
        />
        {showSuggestions ? (
          <div className="progression-page__suggestions">
            {suggestions.status === 'loading' ? <Spinner label="Searching…" /> : null}
            {suggestions.status === 'error' ? <p role="alert">Could not search exercises. Try again.</p> : null}
            {suggestions.status === 'loaded' && suggestions.items.length === 0 ? <p>No exercises found.</p> : null}
            {suggestions.status === 'loaded' && suggestions.items.length > 0 ? (
              <ul aria-label="Matching exercises">
                {suggestions.items.map((exercise) => (
                  <li key={exercise.id}>
                    <button type="button" onClick={() => choose({ id: exercise.id, name: exercise.name })}>
                      <span>{exercise.name}</span>
                      <span className="progression-page__muscle">{exercise.mainMuscle}</span>
                    </button>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
        ) : null}
        {today.length > 0 ? (
          <section className="progression-page__today" aria-label="From today's workout">
            <h2>From today&apos;s workout</h2>
            <div>
              {today.map((choice) => (
                <button
                  key={choice.id}
                  type="button"
                  className="progression-page__pill"
                  aria-pressed={choice.id === selectedId}
                  onClick={() => choose(choice)}
                >
                  {choice.name}
                </button>
              ))}
            </div>
          </section>
        ) : null}
      </Card>

      {selected ? (
        <Card tone="material" className="progression-page__panel">
          <header className="progression-page__result-header">
            <div>
              <h2>{selected.name}</h2>
              <p>Estimated 1 rep max, kg</p>
            </div>
            {progress.status === 'loaded' && progress.points.length > 1 ? <Summary points={progress.points} /> : null}
          </header>
          {progress.status === 'loading' ? <Spinner label="Loading progress…" /> : null}
          {progress.status === 'error' ? (
            <div className="progression-page__empty" role="alert">
              <strong>Could not load your progress</strong>
              <p>Check your connection and try again.</p>
              <Button size="sm" variant="secondary" onClick={retry}>
                Try again
              </Button>
            </div>
          ) : null}
          {progress.status === 'loaded' && progress.points.length === 0 ? (
            <div className="progression-page__empty">
              <strong>No data yet</strong>
              <p>Complete this exercise in a workout and its progression will appear here.</p>
            </div>
          ) : null}
          {progress.status === 'loaded' && progress.points.length === 1 ? (
            <div className="progression-page__empty">
              <strong>Not enough data</strong>
              <p>You need at least two sessions with this exercise to see a progression chart.</p>
            </div>
          ) : null}
          {progress.status === 'loaded' && progress.points.length > 1 ? (
            <ProgressChart key={selected.id} exerciseName={selected.name} points={progress.points} />
          ) : null}
        </Card>
      ) : null}
    </main>
  )
}

function Summary({ points }: { points: ProgressPoint[] }) {
  const first = Math.round(points[0].estimatedOneRepMax)
  const last = Math.round(points[points.length - 1].estimatedOneRepMax)
  const change = last - first
  return (
    <dl className="progression-page__summary">
      <div>
        <dd>{last} kg</dd>
        <dt>Current</dt>
      </div>
      <div>
        <dd className={change >= 0 ? 'progression-page__gain' : undefined}>
          {change > 0 ? '+' : ''}
          {change} kg
        </dd>
        <dt>Since first session</dt>
      </div>
    </dl>
  )
}
