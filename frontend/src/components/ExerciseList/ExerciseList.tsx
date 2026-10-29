import { useCallback, useEffect, useRef, useState } from 'react'
import { ChevronDown, Search } from 'lucide-react'
import { friendlyErrorMessage } from '../../api/errors'
import { getExercises, MUSCLE_GROUPS, type Exercise } from '../../api/exercises'
import { ExerciseCard } from '../ExerciseCard/ExerciseCard'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { Spinner } from '../ui/Spinner'
import './ExerciseList.css'

const PAGE_SIZE = 24
const SEARCH_DELAY_MS = 300

type Status = 'loading' | 'ready' | 'error'

interface Group {
  muscle: string
  exercises: Exercise[]
}

function groupByMuscle(exercises: Exercise[]): Group[] {
  const groups: Group[] = []
  for (const exercise of exercises) {
    const last = groups[groups.length - 1]
    if (last && last.muscle === exercise.mainMuscle) {
      last.exercises.push(exercise)
    } else {
      groups.push({ muscle: exercise.mainMuscle, exercises: [exercise] })
    }
  }
  return groups
}

interface Loaded {
  key: string
  items: Exercise[]
  total: number
  page: number
}

interface Failure {
  key: string
  message: string
}

interface MoreState {
  key: string
  failed: boolean
}

export function ExerciseList() {
  const [input, setInput] = useState('')
  const [term, setTerm] = useState('')
  const [muscle, setMuscle] = useState('')
  const [reloadKey, setReloadKey] = useState(0)
  const [loaded, setLoaded] = useState<Loaded | null>(null)
  const [failure, setFailure] = useState<Failure | null>(null)
  const [more, setMore] = useState<MoreState | null>(null)
  const controller = useRef<AbortController | null>(null)

  const queryKey = [term, muscle, reloadKey].join('|')
  const status: Status = failure?.key === queryKey ? 'error' : loaded?.key === queryKey ? 'ready' : 'loading'
  const items = status === 'ready' && loaded ? loaded.items : []
  const total = loaded?.total ?? 0
  const loadingMore = more !== null && more.key === queryKey && !more.failed
  const moreFailed = more !== null && more.key === queryKey && more.failed
  const errorMessage = failure?.message ?? ''

  useEffect(() => {
    const timer = setTimeout(() => setTerm(input), SEARCH_DELAY_MS)
    return () => clearTimeout(timer)
  }, [input])

  useEffect(() => {
    const request = new AbortController()
    controller.current = request
    getExercises({ term, muscle, page: 1, pageSize: PAGE_SIZE }, request.signal)
      .then((result) => setLoaded({ key: queryKey, items: result.items, total: result.totalCount, page: 1 }))
      .catch((error: unknown) => {
        if (!request.signal.aborted) {
          setFailure({ key: queryKey, message: friendlyErrorMessage(error) })
        }
      })
    return () => controller.current?.abort()
  }, [term, muscle, queryKey])

  const loadMore = useCallback(() => {
    if (!loaded) {
      return
    }
    const request = new AbortController()
    controller.current = request
    setMore({ key: queryKey, failed: false })
    getExercises({ term, muscle, page: loaded.page + 1, pageSize: PAGE_SIZE }, request.signal)
      .then((result) => {
        setLoaded({ key: queryKey, items: [...loaded.items, ...result.items], total: result.totalCount, page: result.page })
        setMore(null)
      })
      .catch(() => {
        if (!request.signal.aborted) {
          setMore({ key: queryKey, failed: true })
        }
      })
  }, [loaded, term, muscle, queryKey])

  const clearSearch = () => {
    setInput('')
    setTerm('')
    setMuscle('')
  }

  const searching = term.trim() !== '' || muscle !== ''
  const groups = groupByMuscle(items)

  return (
    <section className="exercise-list">
      <div className="exercise-list__controls">
        <Input
          type="search"
          aria-label="Search exercises"
          placeholder="Search by name or sub-muscle…"
          icon={<Search size={18} />}
          value={input}
          onChange={(event) => setInput(event.target.value)}
        />
        <div className="exercise-list__select">
          <select aria-label="Muscle group" value={muscle} onChange={(event) => setMuscle(event.target.value)}>
            <option value="">All muscles</option>
            {MUSCLE_GROUPS.map((group) => (
              <option key={group} value={group}>
                {group}
              </option>
            ))}
          </select>
          <ChevronDown size={16} aria-hidden="true" />
        </div>
      </div>

      {status === 'loading' ? <Spinner label="Loading exercises…" /> : null}

      {status === 'error' ? (
        <div className="exercise-list__state" role="alert">
          <h3>Could not load exercises</h3>
          <p>{errorMessage}</p>
          <Button variant="danger" onClick={() => setReloadKey((key) => key + 1)}>
            Try again
          </Button>
        </div>
      ) : null}

      {status === 'ready' && items.length === 0 ? (
        <div className="exercise-list__state">
          <h3>{term.trim() ? `No exercises match “${term.trim()}”` : 'No exercises found'}</h3>
          {searching ? (
            <Button variant="secondary" onClick={clearSearch}>
              Clear search
            </Button>
          ) : null}
        </div>
      ) : null}

      {status === 'ready' && items.length > 0 ? (
        <>
          <p className="exercise-list__count" aria-live="polite">
            Showing {items.length} of {total} exercises
          </p>
          {groups.map((group) => (
            <section key={group.muscle} className="exercise-list__group" aria-label={group.muscle}>
              <div className="exercise-list__heading">
                <h3>{group.muscle}</h3>
                <span aria-hidden="true" />
              </div>
              <div className="exercise-list__grid">
                {group.exercises.map((exercise) => (
                  <ExerciseCard key={exercise.id} exercise={exercise} />
                ))}
              </div>
            </section>
          ))}
          {items.length < total ? (
            <div className="exercise-list__more">
              {moreFailed ? <p role="alert">Could not load more exercises.</p> : null}
              <Button variant="secondary" loading={loadingMore} onClick={loadMore}>
                {moreFailed ? 'Try again' : 'Load more'}
              </Button>
            </div>
          ) : null}
        </>
      ) : null}
    </section>
  )
}
