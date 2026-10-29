import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getExercises, type Exercise } from '../../api/exercises'
import { ExerciseList } from './ExerciseList'

vi.mock('../../api/exercises')

const benchPress: Exercise = {
  id: '0199e0a1-7c3e-7b5a-9d2f-3f6f0c1a2b3c',
  name: 'Bench Press',
  mainMuscle: 'Chest',
  subMuscles: 'Triceps',
  equipment: 'Barbell',
  difficulty: 'Intermediate',
  description: null,
  youtubeLink: null,
}

describe('ExerciseList', () => {
  afterEach(() => {
    vi.resetAllMocks()
  })

  it('shows the exercises returned by the API', async () => {
    vi.mocked(getExercises).mockResolvedValue([benchPress])

    render(<ExerciseList />)

    expect(screen.getByText('Loading exercises…')).toBeInTheDocument()
    expect(await screen.findByText('Bench Press')).toBeInTheDocument()
    expect(screen.getByText('Chest')).toBeInTheDocument()
  })

  it('shows an empty state when there are no exercises', async () => {
    vi.mocked(getExercises).mockResolvedValue([])

    render(<ExerciseList />)

    expect(await screen.findByText('No exercises yet.')).toBeInTheDocument()
  })

  it('shows an error when the request fails', async () => {
    vi.mocked(getExercises).mockRejectedValue(new Error('network'))

    render(<ExerciseList />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load exercises.')
  })

  it('shows no error when the request is aborted by unmounting', async () => {
    let rejectRequest: (reason: unknown) => void = () => {}
    vi.mocked(getExercises).mockReturnValue(
      new Promise<Exercise[]>((_, reject) => {
        rejectRequest = reject
      }),
    )

    const { unmount } = render(<ExerciseList />)
    unmount()
    rejectRequest(new DOMException('aborted', 'AbortError'))
    await Promise.resolve()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
