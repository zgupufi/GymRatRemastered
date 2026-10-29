import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders the headings and the loaded exercises', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(
        Response.json([{ id: 'a', name: 'Squat', mainMuscle: 'Quads' }]),
      ),
    )

    render(<App />)

    expect(screen.getByRole('heading', { name: 'GymRat' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Exercises' })).toBeInTheDocument()
    expect(await screen.findByText('Squat')).toBeInTheDocument()
  })
})
