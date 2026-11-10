import { Plus } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { Exercise } from '../../api/exercises'
import { DifficultyChip } from '../ui/DifficultyChip'
import './ExerciseCard.css'

interface ExerciseCardProps {
  exercise: Exercise
  onAdd: (exercise: Exercise) => void
}

export function ExerciseCard({ exercise, onAdd }: ExerciseCardProps) {
  const muscles = [exercise.mainMuscle, exercise.subMuscles].filter(Boolean).join(' · ')
  return (
    <article className="exercise-card">
      <div className="exercise-card__text">
        <h4 className="exercise-card__name">
          <Link className="exercise-card__link" to={`/exercises/${exercise.id}`}>
            {exercise.name}
          </Link>
        </h4>
        <p className="exercise-card__muscles">{muscles}</p>
        <DifficultyChip difficulty={exercise.difficulty} />
      </div>
      <button type="button" className="exercise-card__add" aria-label={`Add ${exercise.name} to a workout`} onClick={() => onAdd(exercise)}>
        <Plus size={20} aria-hidden="true" />
      </button>
    </article>
  )
}
