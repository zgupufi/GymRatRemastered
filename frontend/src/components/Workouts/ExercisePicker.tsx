import { useEffect, useMemo, useRef, useState } from 'react'
import { Check, Plus, Search, X } from 'lucide-react'
import { searchExercises, type Exercise } from '../../api/exercises'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { Input } from '../ui/Input'

const SEARCH_DELAY_MS = 250
const RESULT_LIMIT = 20

interface ExercisePickerProps {
  selectedIds: ReadonlySet<string>
  onAdd: (exercise: Exercise) => void
}

type Results =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'loaded'; exercises: Exercise[] }

function groupByMuscle(exercises: Exercise[]): [string, Exercise[]][] {
  const groups = new Map<string, Exercise[]>()
  for (const exercise of exercises) {
    groups.set(exercise.mainMuscle, [...(groups.get(exercise.mainMuscle) ?? []), exercise])
  }
  return [...groups.entries()]
}

export function ExercisePicker({ selectedIds, onAdd }: ExercisePickerProps) {
  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const [results, setResults] = useState<Results>({ status: 'idle' })
  const containerRef = useRef<HTMLDivElement>(null)
  const debouncedTerm = useDebouncedValue(term.trim(), SEARCH_DELAY_MS)

  useEffect(() => {
    if (!debouncedTerm) {
      return
    }
    const controller = new AbortController()
    searchExercises({ term: debouncedTerm, sortBy: 'MainMuscle', pageSize: RESULT_LIMIT }, controller.signal)
      .then((page) => setResults({ status: 'loaded', exercises: page.items }))
      .catch(() => {
        if (!controller.signal.aborted) {
          setResults({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [debouncedTerm])

  useEffect(() => {
    if (!open) {
      return
    }
    const close = (event: MouseEvent | KeyboardEvent) => {
      if (event instanceof KeyboardEvent) {
        if (event.key === 'Escape') {
          setOpen(false)
        }
        return
      }
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', close)
    }
  }, [open])

  const groups = useMemo(() => (results.status === 'loaded' ? groupByMuscle(results.exercises) : []), [results])
  const showPanel = open && term.trim().length > 0

  const changeTerm = (value: string) => {
    setTerm(value)
    setOpen(true)
    if (value.trim()) {
      setResults((previous) => (previous.status === 'loaded' ? previous : { status: 'loading' }))
    } else {
      setResults({ status: 'idle' })
    }
  }

  return (
    <div className="exercise-picker" ref={containerRef}>
      <Input
        aria-label="Search exercises"
        placeholder="Search exercises by name or muscle…"
        icon={<Search size={18} />}
        value={term}
        onChange={(event) => changeTerm(event.target.value)}
        onFocus={() => setOpen(true)}
        autoComplete="off"
      />
      {term ? (
        <button type="button" className="exercise-picker__clear" aria-label="Clear search" onClick={() => changeTerm('')}>
          <X size={16} aria-hidden="true" />
        </button>
      ) : null}
      {showPanel ? (
        <div className="exercise-picker__panel">
          {results.status === 'loading' ? <p className="exercise-picker__note">Searching…</p> : null}
          {results.status === 'error' ? (
            <p className="exercise-picker__note" role="alert">
              Could not load exercises.
            </p>
          ) : null}
          {results.status === 'loaded' && groups.length === 0 ? (
            <p className="exercise-picker__note">No exercises match “{debouncedTerm}”.</p>
          ) : null}
          {groups.map(([muscle, exercises]) => (
            <div key={muscle}>
              <div className="exercise-picker__group">{muscle}</div>
              {exercises.map((exercise) => {
                const added = selectedIds.has(exercise.id)
                return (
                  <button
                    key={exercise.id}
                    type="button"
                    className="exercise-picker__row"
                    disabled={added}
                    aria-label={`${added ? 'Already added' : 'Add'} ${exercise.name}`}
                    onClick={() => onAdd(exercise)}
                  >
                    <span>
                      {exercise.name}
                      <small>{[exercise.subMuscles, exercise.equipment].filter(Boolean).join(' · ')}</small>
                    </span>
                    {added ? (
                      <Check size={18} className="exercise-picker__added" aria-hidden="true" />
                    ) : (
                      <Plus size={18} aria-hidden="true" />
                    )}
                  </button>
                )
              })}
            </div>
          ))}
        </div>
      ) : null}
    </div>
  )
}
