import { useEffect, useMemo, useState, type KeyboardEvent } from 'react'
import { Reorder, useDragControls } from 'motion/react'
import { GripVertical, Trash2 } from 'lucide-react'
import { replaceWorkoutExercises, getWorkout } from '../../api/workouts'
import type { Exercise } from '../../api/exercises'
import { Button } from '../ui/Button'
import { NumberInput } from '../ui/NumberInput'
import { Spinner } from '../ui/Spinner'
import { useToast } from '../ui/ToastContext'
import { ExercisePicker } from './ExercisePicker'

const DEFAULT_SETS = 3
const MIN_SETS = 1
const MAX_SETS = 20

interface EditItem {
  exercise: Exercise
  targetSets: string
}

type State =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; items: EditItem[] }

interface WorkoutEditorProps {
  workoutId: string
  onSaved: () => void
}

function isValidSets(value: string): boolean {
  const sets = Number(value)
  return Number.isInteger(sets) && sets >= MIN_SETS && sets <= MAX_SETS
}

interface RowProps {
  item: EditItem
  index: number
  count: number
  onSetsChange: (value: string) => void
  onRemove: () => void
  onMove: (direction: -1 | 1) => void
}

function ExerciseRow({ item, index, count, onSetsChange, onRemove, onMove }: RowProps) {
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
      <button type="button" className="icon-button icon-button--danger" aria-label={`Remove ${exercise.name}`} onClick={onRemove}>
        <Trash2 size={16} aria-hidden="true" />
      </button>
    </Reorder.Item>
  )
}

export function WorkoutEditor({ workoutId, onSaved }: WorkoutEditorProps) {
  const { showToast } = useToast()
  const [state, setState] = useState<State>({ status: 'loading' })
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    getWorkout(workoutId, controller.signal)
      .then((workout) =>
        setState({
          status: 'ready',
          items: [...workout.exercises]
            .sort((a, b) => a.order - b.order)
            .map((entry) => ({ exercise: entry.exercise, targetSets: String(entry.targetSets) })),
        }),
      )
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [workoutId])

  const items = useMemo(() => (state.status === 'ready' ? state.items : []), [state])
  const selectedIds = useMemo(() => new Set(items.map((item) => item.exercise.id)), [items])
  const allValid = items.every((item) => isValidSets(item.targetSets))

  const setItems = (next: EditItem[]) => setState({ status: 'ready', items: next })

  const move = (index: number, direction: -1 | 1) => {
    const next = [...items]
    const target = index + direction
    ;[next[index], next[target]] = [next[target], next[index]]
    setItems(next)
  }

  const save = async () => {
    setSaving(true)
    try {
      await replaceWorkoutExercises(
        workoutId,
        items.map((item) => ({ exerciseId: item.exercise.id, targetSets: Number(item.targetSets) })),
      )
      showToast('success', 'Workout saved successfully!')
      onSaved()
    } catch {
      showToast('error', 'Save failed. Try again.')
    } finally {
      setSaving(false)
    }
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
        onAdd={(exercise) => setItems([...items, { exercise, targetSets: String(DEFAULT_SETS) }])}
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
              onRemove={() => setItems(items.filter((_, i) => i !== index))}
              onMove={(direction) => move(index, direction)}
            />
          ))}
        </Reorder.Group>
      )}
      <div className="workout-editor__footer">
        <span>Drag the handle to reorder · sets {MIN_SETS}–{MAX_SETS}</span>
        <Button size="sm" loading={saving} disabled={!allValid} onClick={save}>
          {saving ? 'Saving…' : 'Save changes'}
        </Button>
      </div>
    </div>
  )
}
