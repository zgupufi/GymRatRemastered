import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { getExercises, type Exercise, type PagedResult } from '../../api/exercises'
import { ExerciseList } from './ExerciseList'

vi.mock('../../api/exercises', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/exercises')>()),
  getExercises: vi.fn(),
}))

function exercise(id: string, name: string, mainMuscle: string): Exercise {
  return { id, name, mainMuscle, subMuscles: 'Triceps', equipment: 'Barbell', difficulty: 'Intermediate', description: null, youtubeLink: null }
}

function page(items: Exercise[], totalCount = items.length, pageNumber = 1): PagedResult<Exercise> {
  return { items, page: pageNumber, pageSize: 24, totalCount }
}

function renderList() {
  return render(
    <MemoryRouter>
      <ExerciseList />
    </MemoryRouter>,
  )
}

describe('ExerciseList', () => {
  afterEach(() => {
    vi.resetAllMocks()
  })

  it('groups the loaded exercises by muscle', async () => {
    vi.mocked(getExercises).mockResolvedValue(
      page([exercise('1', 'Bench Press', 'Chest'), exercise('2', 'Push-up', 'Chest'), exercise('3', 'Squat', 'Legs')]),
    )

    renderList()

    expect(screen.getByText('Loading exercises…')).toBeInTheDocument()
    expect(await screen.findByText('Bench Press')).toBeInTheDocument()
    expect(screen.getByText('Showing 3 of 3 exercises')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Chest' })).toHaveTextContent('Push-up')
    expect(screen.getByRole('region', { name: 'Legs' })).toHaveTextContent('Squat')
    expect(screen.getByRole('link', { name: 'View details for Squat' })).toHaveAttribute('href', '/exercises/3')
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('loads and appends the next page', async () => {
    vi.mocked(getExercises)
      .mockResolvedValueOnce(page([exercise('1', 'Bench Press', 'Chest')], 2))
      .mockResolvedValueOnce(page([exercise('2', 'Push-up', 'Chest')], 2, 2))

    renderList()
    await userEvent.click(await screen.findByRole('button', { name: 'Load more' }))

    expect(await screen.findByText('Push-up')).toBeInTheDocument()
    expect(screen.getByText('Bench Press')).toBeInTheDocument()
    expect(vi.mocked(getExercises).mock.calls[1][0]).toMatchObject({ page: 2 })
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('lets the user retry when loading more fails', async () => {
    vi.mocked(getExercises)
      .mockResolvedValueOnce(page([exercise('1', 'Bench Press', 'Chest')], 2))
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce(page([exercise('2', 'Push-up', 'Chest')], 2, 2))

    renderList()
    await userEvent.click(await screen.findByRole('button', { name: 'Load more' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load more exercises.')

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Push-up')).toBeInTheDocument()
  })

  it('searches after the user stops typing', async () => {
    vi.mocked(getExercises).mockResolvedValue(page([exercise('1', 'Bench Press', 'Chest')]))

    renderList()
    await screen.findByText('Bench Press')
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search exercises' }), 'bench')

    await waitFor(() => expect(vi.mocked(getExercises).mock.calls.at(-1)?.[0]).toMatchObject({ term: 'bench', page: 1 }))
  })

  it('filters by muscle group', async () => {
    vi.mocked(getExercises).mockResolvedValue(page([exercise('1', 'Bench Press', 'Chest')]))

    renderList()
    await screen.findByText('Bench Press')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Muscle group' }), 'Chest')

    await waitFor(() => expect(vi.mocked(getExercises).mock.calls.at(-1)?.[0]).toMatchObject({ muscle: 'Chest' }))
  })

  it('shows an empty state for a search and clears it', async () => {
    vi.mocked(getExercises).mockImplementation(async (query) =>
      query.term ? page([]) : page([exercise('1', 'Bench Press', 'Chest')]),
    )

    renderList()
    await screen.findByText('Bench Press')
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search exercises' }), 'xyz')

    expect(await screen.findByText('No exercises match “xyz”')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear search' }))

    expect(await screen.findByText('Bench Press')).toBeInTheDocument()
    expect(screen.getByRole('searchbox', { name: 'Search exercises' })).toHaveValue('')
  })

  it('shows a plain empty state when there is nothing to search', async () => {
    vi.mocked(getExercises).mockResolvedValue(page([]))

    renderList()

    expect(await screen.findByText('No exercises found')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Clear search' })).not.toBeInTheDocument()
  })

  it('shows an error and retries', async () => {
    vi.mocked(getExercises)
      .mockRejectedValueOnce(new ApiError(0, 'Network request failed'))
      .mockResolvedValueOnce(page([exercise('1', 'Bench Press', 'Chest')]))

    renderList()

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load exercises')
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Bench Press')).toBeInTheDocument()
  })

  it('shows no error when the request is aborted by unmounting', async () => {
    let rejectRequest: (reason: unknown) => void = () => {}
    vi.mocked(getExercises).mockReturnValue(
      new Promise<PagedResult<Exercise>>((_, reject) => {
        rejectRequest = reject
      }),
    )

    const { unmount } = renderList()
    unmount()
    rejectRequest(new DOMException('aborted', 'AbortError'))
    await Promise.resolve()

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
