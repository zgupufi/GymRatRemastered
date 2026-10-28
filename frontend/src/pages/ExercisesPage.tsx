import { BackButton } from '../components/ui/BackButton'
import { ExerciseList } from '../components/ExerciseList/ExerciseList'

export function ExercisesPage() {
  return (
    <main className="page">
      <BackButton />
      <header className="page__header">
        <h1>Exercise database</h1>
        <p>Explore and choose exercises for your workout, organized by muscle groups.</p>
      </header>
      <ExerciseList />
    </main>
  )
}
