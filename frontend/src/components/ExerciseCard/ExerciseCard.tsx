import { Dumbbell } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { Exercise } from '../../api/exercises'
import { DifficultyChip } from '../ui/DifficultyChip'
import './ExerciseCard.css'

export function ExerciseCard({ exercise }: { exercise: Exercise }) {
  const muscles = [exercise.mainMuscle, exercise.subMuscles].filter(Boolean).join(' · ')
  return (
    <article className="exercise-card">
      <div className="exercise-card__top">
        <span className="exercise-card__icon" aria-hidden="true">
          <Dumbbell size={18} />
        </span>
        <DifficultyChip difficulty={exercise.difficulty} />
      </div>
      <h4 className="exercise-card__name">{exercise.name}</h4>
      <p className="exercise-card__muscles">{muscles}</p>
      <Link className="exercise-card__link" to={`/exercises/${exercise.id}`} aria-label={`View details for ${exercise.name}`}>
        View details
      </Link>
    </article>
  )
}
