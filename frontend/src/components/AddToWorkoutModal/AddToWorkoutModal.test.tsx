import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import type { Exercise } from '../../api/exercises'
import { getWorkout, getWorkoutSummaries, replaceWorkoutExercises, type Workout } from '../../api/workouts'
import { ToastProvider } from '../ui/Toast'
import { AddToWorkoutModal } from './AddToWorkoutModal'

vi.mock('../../api/workouts')

const summary = { id: 'w1', name: 'Push Day', exerciseCount: 1, mainMuscles: ['Chest'] }

function workoutWith(...exerciseIds: string[]): Workout {
  return {
    id: 'w1',
    name: 'Push Day',
    exercises: exerciseIds.map((id, index) => ({
      order: index,
      targetSets: 4,
      exercise: { id } as Exercise,
    })),
  }
}

function renderModal(onClose = vi.fn()) {
  render(
    <ToastProvider>
      <AddToWorkoutModal open exerciseId="e2" onClose={onClose} />
    </ToastProvider>,
  )
  return onClose
}

describe('AddToWorkoutModal', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.resetAllMocks()
  })

  it('adds the exercise to the chosen workout and closes after a moment', async () => {
    vi.mocked(getWorkoutSummaries).mockResolvedValue({
      items: [summary, { ...summary, id: 'w2', name: 'Legs', exerciseCount: 0 }],
      page: 1,
      pageSize: 100,
      totalCount: 2,
    })
    vi.mocked(getWorkout).mockResolvedValue(workoutWith('e1'))
    vi.mocked(replaceWorkoutExercises).mockResolvedValue()
    const onClose = renderModal()

    expect(await screen.findByText('1 exercise')).toBeInTheDocument()
    expect(screen.getByText('0 exercises')).toBeInTheDocument()
    vi.useFakeTimers({ shouldAdvanceTime: true })
    await userEvent.click(screen.getByRole('button', { name: /Push Day/ }))

    expect(await screen.findByText('Added')).toBeInTheDocument()
    expect(replaceWorkoutExercises).toHaveBeenCalledWith('w1', [
      { exerciseId: 'e1', targetSets: 4 },
      { exerciseId: 'e2', targetSets: 3 },
    ])
    expect(onClose).not.toHaveBeenCalled()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1500)
    })
    expect(onClose).toHaveBeenCalled()
  })

  it('does not add the exercise twice', async () => {
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [summary], page: 1, pageSize: 100, totalCount: 1 })
    vi.mocked(getWorkout).mockResolvedValue(workoutWith('e2'))
    renderModal()

    await userEvent.click(await screen.findByRole('button', { name: /Push Day/ }))

    expect(await screen.findByText('Added')).toBeInTheDocument()
    expect(replaceWorkoutExercises).not.toHaveBeenCalled()
  })

  it('shows a toast when adding fails', async () => {
    vi.mocked(getWorkoutSummaries).mockResolvedValue({ items: [summary], page: 1, pageSize: 100, totalCount: 1 })
    vi.mocked(getWorkout).mockRejectedValue(new ApiError(500, 'boom'))
    renderModal()

    await userEvent.click(await screen.findByRole('button', { name: /Push Day/ }))

    expect(await screen.findByText('The server ran into an issue. Please try again in a moment.')).toBeInTheDocument()
    expect(screen.queryByText('Added')).not.toBeInTheDocument()
  })

  it('shows an error when the workouts cannot be loaded', async () => {
    vi.mocked(getWorkoutSummaries).mockRejectedValue(new ApiError(500, 'boom'))
    renderModal()

    expect(await screen.findByText('Could not load your workouts.')).toBeInTheDocument()
  })

  it('ignores an aborted request after unmounting', async () => {
    let rejectRequest: (reason: unknown) => void = () => {}
    vi.mocked(getWorkoutSummaries).mockReturnValue(
      new Promise((_, reject) => {
        rejectRequest = reject
      }),
    )

    const { unmount } = render(
      <ToastProvider>
        <AddToWorkoutModal open exerciseId="e2" onClose={() => {}} />
      </ToastProvider>,
    )
    unmount()
    rejectRequest(new DOMException('aborted', 'AbortError'))
    await Promise.resolve()

    expect(screen.queryByText('Could not load your workouts.')).not.toBeInTheDocument()
  })
})
