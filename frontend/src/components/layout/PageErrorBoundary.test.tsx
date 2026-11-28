import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PageErrorBoundary } from './PageErrorBoundary'

function Broken(): never {
  throw new Error('chunk failed to load')
}

describe('PageErrorBoundary', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('offers a reload when a page does not load', async () => {
    const reload = vi.fn()
    vi.stubGlobal('location', { reload })
    render(
      <PageErrorBoundary resetKey="/split">
        <Broken />
      </PageErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('This page did not load')
    await userEvent.click(screen.getByRole('button', { name: 'Reload' }))

    expect(reload).toHaveBeenCalledOnce()
  })

  it('keeps the message on the same page and clears it on another one', () => {
    const { rerender } = render(
      <PageErrorBoundary resetKey="/split">
        <Broken />
      </PageErrorBoundary>,
    )

    rerender(
      <PageErrorBoundary resetKey="/split">
        <p>same page</p>
      </PageErrorBoundary>,
    )
    expect(screen.queryByText('same page')).not.toBeInTheDocument()

    rerender(
      <PageErrorBoundary resetKey="/workouts">
        <p>next page</p>
      </PageErrorBoundary>,
    )
    expect(screen.getByText('next page')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
