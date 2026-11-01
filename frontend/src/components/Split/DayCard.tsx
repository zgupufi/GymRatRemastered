import { forwardRef } from 'react'
import type { WorkoutSummary } from '../../api/workouts'
import { WorkoutSelect } from './WorkoutSelect'

export const FULL_BODY_THRESHOLD = 6

interface DayCardProps {
  name: string
  isToday: boolean
  workouts: WorkoutSummary[]
  workoutId: string | null
  onChange: (workoutId: string | null) => void
}

export const DayCard = forwardRef<HTMLLIElement, DayCardProps>(function DayCard(
  { name, isToday, workouts, workoutId, onChange },
  ref,
) {
  const assigned = workouts.find((workout) => workout.id === workoutId)
  const muscles = assigned ? [...new Set(assigned.mainMuscles)] : []
  const classes = ['day-card', assigned ? 'day-card--assigned' : '', isToday ? 'day-card--today' : ''].filter(Boolean).join(' ')

  return (
    <li ref={ref} className={classes} aria-label={name}>
      <div className="day-card__head">
        <h2>{name}</h2>
        {isToday ? <span className="day-card__badge">Today</span> : null}
      </div>
      <WorkoutSelect label={`${name} workout`} workouts={workouts} value={workoutId} onChange={onChange} />
      {muscles.length > 0 ? (
        <div className="day-card__focus">
          <small>Main focus</small>
          <div className="day-card__chips">
            {muscles.length >= FULL_BODY_THRESHOLD ? (
              <span className="chip chip--blue">Full body</span>
            ) : (
              muscles.map((muscle) => (
                <span key={muscle} className="chip">
                  {muscle}
                </span>
              ))
            )}
          </div>
        </div>
      ) : null}
    </li>
  )
})
