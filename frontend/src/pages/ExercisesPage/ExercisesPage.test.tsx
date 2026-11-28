import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, it, vi } from 'vitest'
import { searchExercises } from '../../api/exercises'
import { expectNoA11yViolations } from '../../test/a11y'
import { ExercisesPage } from './ExercisesPage'

vi.mock('../../api/exercises', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/exercises')>()),
  searchExercises: vi.fn(),
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <ExercisesPage />
    </MemoryRouter>,
  )
}

describe('ExercisesPage', () => {
  beforeEach(() => {
    vi.mocked(searchExercises).mockResolvedValue({
      items: [
        {
          id: '1',
          name: 'Bench Press',
          mainMuscle: 'Chest',
          secondaryMuscles: ['Triceps'],
          equipment: 'Barbell',
          difficulty: 'Intermediate',
          movementPattern: 'Push',
          description: null,
          setup: 'Set up.',
          execution: 'Do it.',
          youtubeLink: null,
        },
      ],
      page: 1,
      pageSize: 24,
      totalCount: 1,
    })
  })

  it('has no accessibility violations', async () => {
    renderPage()
    await screen.findByRole('link', { name: 'Bench Press' })

    await expectNoA11yViolations()
  })

  it('has no accessibility violations when filtered by muscle', async () => {
    renderPage()
    await userEvent.click(screen.getByRole('button', { name: 'Chest' }))
    await screen.findByRole('link', { name: 'Bench Press' })

    await expectNoA11yViolations()
  })

  it('has no accessibility violations when nothing is found', async () => {
    vi.mocked(searchExercises).mockResolvedValue({ items: [], page: 1, pageSize: 24, totalCount: 0 })
    renderPage()
    await screen.findByRole('heading', { level: 2, name: 'No exercises found' })

    await expectNoA11yViolations()
  })
})
