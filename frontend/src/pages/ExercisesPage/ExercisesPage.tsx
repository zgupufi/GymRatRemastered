import { ExerciseList } from '../../components/ExerciseList/ExerciseList'
import './ExercisesPage.css'

export function ExercisesPage() {
  return (
    <main className="page exercises-page">
      <header className="page__header">
        <h1>Exercise library</h1>
        <p>Browse exercises by muscle group and add them to your workouts.</p>
      </header>
      <ExerciseList />
    </main>
  )
}
