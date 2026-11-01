import { useCallback, useEffect, useState } from 'react'
import { Plus, Search } from 'lucide-react'
import { createWorkout, getWorkoutSummaries, type WorkoutSortField, type WorkoutSummary } from '../../api/workouts'
import type { PagedResult, SortDirection } from '../../api/pagination'
import { WorkoutCard } from '../../components/Workouts/WorkoutCard'
import { BackButton } from '../../components/ui/BackButton'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { SegmentedControl } from '../../components/ui/SegmentedControl'
import { Spinner } from '../../components/ui/Spinner'
import { useToast } from '../../components/ui/ToastContext'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import './WorkoutsPage.css'

const PAGE_SIZE = 10
const SEARCH_DELAY_MS = 300

type SortOption = 'newest' | 'name'

const SORTS: Record<SortOption, { sortBy: WorkoutSortField; direction: SortDirection }> = {
  newest: { sortBy: 'CreatedAt', direction: 'Descending' },
  name: { sortBy: 'Name', direction: 'Ascending' },
}

type State =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'loaded'; result: PagedResult<WorkoutSummary> }

function newWorkoutName(): string {
  const day = new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
  return `New workout - ${day}`
}

export function WorkoutsPage() {
  const { showToast } = useToast()
  const [term, setTerm] = useState('')
  const [sort, setSort] = useState<SortOption>('newest')
  const [page, setPage] = useState(1)
  const [reloadKey, setReloadKey] = useState(0)
  const [state, setState] = useState<State>({ status: 'loading' })
  const [creating, setCreating] = useState(false)
  const [createdId, setCreatedId] = useState<string | null>(null)
  const debouncedTerm = useDebouncedValue(term.trim(), SEARCH_DELAY_MS)

  const reload = useCallback(() => setReloadKey((key) => key + 1), [])

  useEffect(() => {
    const controller = new AbortController()
    getWorkoutSummaries(
      { term: debouncedTerm, ...SORTS[sort], page, pageSize: PAGE_SIZE },
      controller.signal,
    )
      .then((result) => {
        if (result.items.length === 0 && result.page > 1) {
          setPage(result.page - 1)
          return
        }
        setState({ status: 'loaded', result })
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [debouncedTerm, sort, page, reloadKey])

  const changeTerm = (value: string) => {
    setTerm(value)
    setPage(1)
  }

  const changeSort = (value: SortOption) => {
    setSort(value)
    setPage(1)
  }

  const create = async () => {
    setCreating(true)
    try {
      const { id } = await createWorkout(newWorkoutName())
      setCreatedId(id)
      setTerm('')
      setSort('newest')
      setPage(1)
      reload()
    } catch {
      showToast('error', 'Could not create the workout. Try again.')
    } finally {
      setCreating(false)
    }
  }

  const result = state.status === 'loaded' ? state.result : null
  const firstShown = result && result.items.length > 0 ? (result.page - 1) * result.pageSize + 1 : 0
  const lastShown = result ? firstShown + result.items.length - 1 : 0
  const hasNext = result ? result.page * result.pageSize < result.totalCount : false

  return (
    <main className="page workouts-page">
      <BackButton />
      <header className="page__header workouts-page__header">
        <div>
          <h1>My workouts</h1>
          <p>Create, edit, and organize your routines.</p>
        </div>
        <Button onClick={create} loading={creating}>
          <Plus size={18} aria-hidden="true" />
          Create workout
        </Button>
      </header>

      <div className="workouts-page__tools">
        <Input
          aria-label="Search workouts"
          placeholder="Search workouts by name…"
          icon={<Search size={18} />}
          value={term}
          onChange={(event) => changeTerm(event.target.value)}
        />
        <SegmentedControl
          label="Sort workouts"
          value={sort}
          onChange={changeSort}
          options={[
            { value: 'newest', label: 'Newest' },
            { value: 'name', label: 'Name A–Z' },
          ]}
        />
      </div>

      {state.status === 'loading' ? <Spinner label="Loading workouts…" /> : null}
      {state.status === 'error' ? (
        <div className="workouts-page__empty" role="alert">
          <strong>Could not load your workouts</strong>
          <p>Check your connection and try again.</p>
          <Button size="sm" variant="secondary" onClick={reload}>
            Try again
          </Button>
        </div>
      ) : null}
      {result && result.items.length === 0 ? (
        debouncedTerm ? (
          <div className="workouts-page__empty">
            <strong>No workouts match “{debouncedTerm}”</strong>
            <p>Try a different name.</p>
          </div>
        ) : (
          <div className="workouts-page__empty">
            <span className="workouts-page__plus" aria-hidden="true">
              <Plus size={28} />
            </span>
            <strong>No workouts</strong>
            <p>Start by creating your first workout using the button above.</p>
          </div>
        )
      ) : null}
      {result && result.items.length > 0 ? (
        <>
          <ul className="workouts-page__list">
            {result.items.map((workout) => (
              <WorkoutCard
                key={workout.id}
                workout={workout}
                startExpanded={workout.id === createdId}
                startRenaming={workout.id === createdId}
                onChanged={reload}
                onDeleted={reload}
              />
            ))}
          </ul>
          <nav className="workouts-page__pager" aria-label="Workouts pages">
            <span>
              Showing {firstShown}–{lastShown} of {result.totalCount} {result.totalCount === 1 ? 'workout' : 'workouts'}
            </span>
            <div>
              <Button size="sm" variant="secondary" disabled={result.page <= 1} onClick={() => setPage((value) => value - 1)}>
                Previous
              </Button>
              <Button size="sm" variant="secondary" disabled={!hasNext} onClick={() => setPage((value) => value + 1)}>
                Next
              </Button>
            </div>
          </nav>
        </>
      ) : null}
    </main>
  )
}
