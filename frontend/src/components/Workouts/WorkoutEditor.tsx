import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { Reorder, useDragControls } from 'motion/react'
import { GripVertical, Trash2 } from 'lucide-react'
import { replaceWorkoutExercises, getWorkout } from '../../api/workouts'
import type { Exercise } from '../../api/exercises'
import { Button } from '../ui/Button'
import { NumberInput } from '../ui/NumberInput'
import { Spinner } from '../ui/Spinner'
import { ExercisePicker } from './ExercisePicker'

const DEFAULT_SETS = 3
const MIN_SETS = 1
const MAX_SETS = 20
const DEFAULT_REPS_MIN = 8
const DEFAULT_REPS_MAX = 12
const MIN_REPS = 1
const MAX_REPS = 30

interface EditItem {
  exercise: Exercise
  targetSets: string
  repsMin: string
  repsMax: string
}

type State =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; items: EditItem[] }

interface WorkoutEditorProps {
  workoutId: string
}

type SaveStatus = 'idle' | 'saving' | 'saved' | 'error'

const AUTOSAVE_DELAY_MS = 600

const STATUS_TEXT: Record<SaveStatus, string> = {
  idle: '',
  saving: 'Saving…',
  saved: 'Saved',
  error: 'Could not save',
}

function isValidSets(value: string): boolean {
  const sets = Number(value)
  return Number.isInteger(sets) && sets >= MIN_SETS && sets <= MAX_SETS
}

function isValidReps(value: string): boolean {
  const reps = Number(value)
  return value !== '' && Number.isInteger(reps) && reps >= MIN_REPS && reps <= MAX_REPS
}

function isValidRange(item: Pick<EditItem, 'repsMin' | 'repsMax'>): boolean {
  return isValidReps(item.repsMin) && isValidReps(item.repsMax) && Number(item.repsMin) <= Number(item.repsMax)
}

function isValidItem(item: EditItem): boolean {
  return isValidSets(item.targetSets) && isValidRange(item)
}

function toEntries(items: EditItem[]) {
  return items.map((item) => ({
    exerciseId: item.exercise.id,
    targetSets: Number(item.targetSets),
    repsMin: Number(item.repsMin),
    repsMax: Number(item.repsMax),
  }))
}

interface RowProps {
  item: EditItem
  index: number
  count: number
  onSetsChange: (value: string) => void
  onRangeChange: (patch: Partial<Pick<EditItem, 'repsMin' | 'repsMax'>>) => void
  onRemove: () => void
  onMove: (direction: -1 | 1) => void
}

function ExerciseRow({ item, index, count, onSetsChange, onRangeChange, onRemove, onMove }: RowProps) {
  const controls = useDragControls()
  const { exercise } = item

  const handleKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'ArrowUp' && index > 0) {
      event.preventDefault()
      onMove(-1)
    } else if (event.key === 'ArrowDown' && index < count - 1) {
      event.preventDefault()
      onMove(1)
    }
  }

  return (
    <Reorder.Item
      value={item}
      as="li"
      className="workout-editor__row"
      dragListener={false}
      dragControls={controls}
      whileDrag={{ scale: 1.02 }}
    >
      <button
        type="button"
        className="workout-editor__grip"
        aria-label={`Reorder ${exercise.name}. Use the arrow keys to move it.`}
        onPointerDown={(event) => controls.start(event)}
        onKeyDown={handleKeyDown}
      >
        <GripVertical size={18} aria-hidden="true" />
      </button>
      <span className="workout-editor__index" aria-hidden="true">
        {index + 1}
      </span>
      <div className="workout-editor__name">
        <strong>{exercise.name}</strong>
        <small>{[exercise.mainMuscle, exercise.equipment].filter(Boolean).join(' · ')}</small>
      </div>
      <label className="workout-editor__sets">
        <span>Sets</span>
        <NumberInput
          aria-label={`Sets for ${exercise.name}`}
          min={MIN_SETS}
          max={MAX_SETS}
          value={item.targetSets}
          aria-invalid={isValidSets(item.targetSets) ? undefined : true}
          onValueChange={onSetsChange}
        />
      </label>
      <fieldset className="workout-editor__reps">
        <legend>Reps</legend>
        <NumberInput
          aria-label={`Minimum reps for ${exercise.name}`}
          min={MIN_REPS}
          max={MAX_REPS}
          value={item.repsMin}
          aria-invalid={isValidRange(item) ? undefined : true}
          onValueChange={(repsMin) => onRangeChange({ repsMin })}
        />
        <span aria-hidden="true">–</span>
        <NumberInput
          aria-label={`Maximum reps for ${exercise.name}`}
          min={MIN_REPS}
          max={MAX_REPS}
          value={item.repsMax}
          aria-invalid={isValidRange(item) ? undefined : true}
          onValueChange={(repsMax) => onRangeChange({ repsMax })}
        />
      </fieldset>
      <button type="button" className="icon-button icon-button--danger" aria-label={`Remove ${exercise.name}`} onClick={onRemove}>
        <Trash2 size={16} aria-hidden="true" />
      </button>
    </Reorder.Item>
  )
}

export function WorkoutEditor({ workoutId }: WorkoutEditorProps) {
  const [state, setState] = useState<State>({ status: 'loading' })
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle')
  const dirty = useRef(false)
  const latest = useRef<EditItem[]>([])
  const saveTimer = useRef<number | undefined>(undefined)
  const saveSequence = useRef(0)

  useEffect(() => {
    const controller = new AbortController()
    getWorkout(workoutId, controller.signal)
      .then((workout) => {
        const loaded = [...workout.exercises]
          .sort((a, b) => a.order - b.order)
          .map((entry) => ({
            exercise: entry.exercise,
            targetSets: String(entry.targetSets),
            repsMin: String(entry.repsMin),
            repsMax: String(entry.repsMax),
          }))
        latest.current = loaded
        setState({ status: 'ready', items: loaded })
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [workoutId])

  const items = useMemo(() => (state.status === 'ready' ? state.items : []), [state])
  const selectedIds = useMemo(() => new Set(items.map((item) => item.exercise.id)), [items])

  const persist = useCallback(
    async (entries: EditItem[]) => {
      dirty.current = false
      saveSequence.current += 1
      const sequence = saveSequence.current
      setSaveStatus('saving')
      try {
        await replaceWorkoutExercises(workoutId, toEntries(entries))
        if (sequence === saveSequence.current) {
          setSaveStatus(dirty.current ? 'saving' : 'saved')
        }
      } catch {
        if (sequence === saveSequence.current) {
          dirty.current = true
          setSaveStatus('error')
        }
      }
    },
    [workoutId],
  )

  const setItems = (next: EditItem[]) => {
    latest.current = next
    dirty.current = true
    window.clearTimeout(saveTimer.current)
    if (next.every(isValidItem)) {
      saveTimer.current = window.setTimeout(() => void persist(next), AUTOSAVE_DELAY_MS)
    }
    setState({ status: 'ready', items: next })
  }

  useEffect(
    () => () => {
      window.clearTimeout(saveTimer.current)
      if (dirty.current && latest.current.every(isValidItem)) {
        void replaceWorkoutExercises(workoutId, toEntries(latest.current)).catch(() => undefined)
      }
    },
    [workoutId],
  )

  const move = (index: number, direction: -1 | 1) => {
    const next = [...items]
    const target = index + direction
    ;[next[index], next[target]] = [next[target], next[index]]
    setItems(next)
  }

  if (state.status === 'loading') {
    return <Spinner label="Loading workout…" />
  }
  if (state.status === 'error') {
    return <p role="alert">Could not load this workout.</p>
  }

  return (
    <div className="workout-editor">
      <ExercisePicker
        selectedIds={selectedIds}
        onAdd={(exercise) =>
          setItems([
            ...items,
            { exercise, targetSets: String(DEFAULT_SETS), repsMin: String(DEFAULT_REPS_MIN), repsMax: String(DEFAULT_REPS_MAX) },
          ])
        }
      />
      {items.length === 0 ? (
        <p className="workout-editor__empty">You haven’t added any exercises yet.</p>
      ) : (
        <Reorder.Group as="ul" axis="y" values={items} onReorder={setItems} className="workout-editor__list">
          {items.map((item, index) => (
            <ExerciseRow
              key={item.exercise.id}
              item={item}
              index={index}
              count={items.length}
              onSetsChange={(value) => setItems(items.map((entry, i) => (i === index ? { ...entry, targetSets: value } : entry)))}
              onRangeChange={(patch) => setItems(items.map((entry, i) => (i === index ? { ...entry, ...patch } : entry)))}
              onRemove={() => setItems(items.filter((_, i) => i !== index))}
              onMove={(direction) => move(index, direction)}
            />
          ))}
        </Reorder.Group>
      )}
      <div className="workout-editor__footer">
        <span>
          Drag the handle to reorder · sets {MIN_SETS}–{MAX_SETS} · reps {MIN_REPS}–{MAX_REPS}
        </span>
        <span className="workout-editor__status" role="status" data-status={saveStatus}>
          {STATUS_TEXT[saveStatus]}
        </span>
        {saveStatus === 'error' ? (
          <Button size="sm" variant="secondary" onClick={() => void persist(latest.current)}>
            Try again
          </Button>
        ) : null}
      </div>
    </div>
  )
}
