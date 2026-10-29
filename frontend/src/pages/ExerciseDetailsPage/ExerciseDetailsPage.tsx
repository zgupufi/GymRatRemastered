import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { friendlyErrorMessage } from '../../api/errors'
import { getExercise, type Exercise } from '../../api/exercises'
import { AddToWorkoutModal } from '../../components/AddToWorkoutModal/AddToWorkoutModal'
import { BackButton } from '../../components/ui/BackButton'
import { Button } from '../../components/ui/Button'
import { Spinner } from '../../components/ui/Spinner'
import { howToPerform } from '../../lib/howToPerform'
import { youtubeEmbedUrl } from '../../lib/youtube'
import './ExerciseDetailsPage.css'

type State =
  | { status: 'loading' }
  | { status: 'missing' }
  | { status: 'error'; message: string }
  | { status: 'ready'; exercise: Exercise }

export function ExerciseDetailsPage() {
  const { id = '' } = useParams()
  return <ExerciseDetails key={id} id={id} />
}

function ExerciseDetails({ id }: { id: string }) {
  const [state, setState] = useState<State>({ status: 'loading' })
  const [chooserOpen, setChooserOpen] = useState(false)

  useEffect(() => {
    const request = new AbortController()
    getExercise(id, request.signal)
      .then((exercise) => setState({ status: 'ready', exercise }))
      .catch((error: unknown) => {
        if (request.signal.aborted) {
          return
        }
        if (error instanceof ApiError && (error.status === 404 || error.status === 400)) {
          setState({ status: 'missing' })
        } else {
          setState({ status: 'error', message: friendlyErrorMessage(error) })
        }
      })
    return () => request.abort()
  }, [id])

  if (state.status === 'loading') {
    return (
      <main className="page">
        <BackButton fallback="/exercises" />
        <Spinner label="Loading exercise…" />
      </main>
    )
  }

  if (state.status !== 'ready') {
    return (
      <main className="page">
        <BackButton fallback="/exercises" />
        <header className="page__header exercise-details__missing">
          <h1>{state.status === 'missing' ? 'Exercise not found.' : 'Could not load exercise'}</h1>
          {state.status === 'error' ? <p role="alert">{state.message}</p> : null}
        </header>
      </main>
    )
  }

  const { exercise } = state
  const embedUrl = youtubeEmbedUrl(exercise.youtubeLink)
  const steps = howToPerform(exercise.equipment, exercise.mainMuscle, exercise.description)
  const kicker = [exercise.difficulty, exercise.mainMuscle].filter(Boolean).join(' · ')

  return (
    <main className="page exercise-details">
      <BackButton fallback="/exercises" />
      <header className="exercise-details__header">
        <div>
          <p className="exercise-details__kicker">{kicker}</p>
          <h1>{exercise.name}</h1>
        </div>
        <Button size="lg" onClick={() => setChooserOpen(true)}>
          + Add to workout
        </Button>
      </header>

      <div className="exercise-details__body">
        <div className="exercise-details__video">
          {embedUrl ? (
            <iframe
              src={embedUrl}
              title={`${exercise.name} demo`}
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
              allowFullScreen
            />
          ) : (
            <p>Demo video unavailable</p>
          )}
        </div>

        <div className="exercise-details__side">
          <div className="exercise-details__facts">
            <div className="exercise-details__fact">
              <span className="eyebrow">Main muscle</span>
              <strong>{exercise.mainMuscle}</strong>
            </div>
            <div className="exercise-details__fact">
              <span className="eyebrow">Secondary</span>
              <strong>{exercise.subMuscles ?? '—'}</strong>
            </div>
            <div className="exercise-details__fact exercise-details__fact--wide">
              <span className="eyebrow">Equipment</span>
              <strong>{exercise.equipment ?? 'Bodyweight'}</strong>
            </div>
          </div>

          <section className="exercise-details__how" aria-labelledby="how-to-perform">
            <h2 id="how-to-perform" className="eyebrow">
              How to perform
            </h2>
            <ol className="exercise-details__steps">
              <li>
                <h3>Setup</h3>
                <p>{steps.setup}</p>
              </li>
              <li>
                <h3>Execution</h3>
                <p>{steps.execution}</p>
              </li>
            </ol>
          </section>
        </div>
      </div>

      <AddToWorkoutModal open={chooserOpen} exerciseId={exercise.id} onClose={() => setChooserOpen(false)} />
    </main>
  )
}
