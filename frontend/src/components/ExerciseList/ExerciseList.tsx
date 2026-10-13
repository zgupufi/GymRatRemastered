import { useEffect, useState } from 'react'
import { getExercises, type Exercise } from '../../api/exercises'
import './ExerciseList.css'

type State =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'loaded'; exercises: Exercise[] }

export function ExerciseList() {
  const [state, setState] = useState<State>({ status: 'loading' })

  useEffect(() => {
    const controller = new AbortController()
    getExercises(controller.signal)
      .then((exercises) => setState({ status: 'loaded', exercises }))
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'error' })
        }
      })
    return () => controller.abort()
  }, [])

  if (state.status === 'loading') {
    return <p>Loading exercises…</p>
  }
  if (state.status === 'error') {
    return <p role="alert">Could not load exercises.</p>
  }
  if (state.exercises.length === 0) {
    return <p>No exercises yet.</p>
  }
  return (
    <ul className="exercise-list">
      {state.exercises.map((exercise) => (
        <li key={exercise.id}>
          <span className="exercise-list__name">{exercise.name}</span>
          <span className="exercise-list__muscle">{exercise.mainMuscle}</span>
        </li>
      ))}
    </ul>
  )
}
