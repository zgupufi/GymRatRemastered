import type { WorkoutSummary } from '../../api/workouts'
import { Modal } from '../ui/Modal'

interface WorkoutPickerModalProps {
  open: boolean
  workouts: WorkoutSummary[]
  selectedId: string | null
  onSelect: (workout: WorkoutSummary) => void
  onClose: () => void
}

export function WorkoutPickerModal({ open, workouts, selectedId, onSelect, onClose }: WorkoutPickerModalProps) {
  return (
    <Modal open={open} title="Select workout" onClose={onClose}>
      {workouts.length === 0 ? (
        <p className="execution__muted">You have no workouts yet. Create one first.</p>
      ) : (
        <ul className="picker">
          {workouts.map((workout) => (
            <li key={workout.id}>
              <button
                type="button"
                className={`picker__item${workout.id === selectedId ? ' picker__item--active' : ''}`}
                aria-current={workout.id === selectedId || undefined}
                onClick={() => onSelect(workout)}
              >
                <span>{workout.name}</span>
                <small>{workout.exerciseCount} exercises</small>
              </button>
            </li>
          ))}
        </ul>
      )}
    </Modal>
  )
}
