import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { PlaceholderPage } from '../../pages/PlaceholderPage'
import { BackButton } from './BackButton'
import { Button } from './Button'
import { ButtonLink } from './ButtonLink'
import { Input } from './Input'
import { Modal } from './Modal'
import { useToast } from './ToastContext'

function CurrentPath() {
  return <p data-testid="path">{useLocation().pathname}</p>
}

function ToastConsumer() {
  useToast()
  return null
}

describe('ui component branches', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('navigates to the fallback when the page was opened directly', async () => {
    render(
      <MemoryRouter initialEntries={['/detail']}>
        <BackButton fallback="/home" />
        <CurrentPath />
      </MemoryRouter>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Back' }))

    expect(screen.getByTestId('path')).toHaveTextContent('/home')
  })

  it('goes back in history when the user navigated within the app', async () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<ButtonLink to="/detail">Open</ButtonLink>} />
          <Route path="/detail" element={<BackButton />} />
        </Routes>
        <CurrentPath />
      </MemoryRouter>,
    )

    await userEvent.click(screen.getByRole('link', { name: 'Open' }))
    expect(screen.getByTestId('path')).toHaveTextContent('/detail')
    await userEvent.click(screen.getByRole('button', { name: 'Back' }))

    expect(screen.getByTestId('path')).toHaveTextContent('/')
  })

  it('renders a placeholder page with and without the back button', () => {
    const { rerender } = render(
      <MemoryRouter>
        <PlaceholderPage title="Workouts" description="Soon" />
      </MemoryRouter>,
    )
    expect(screen.getByRole('heading', { name: 'Workouts' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Back' })).toBeInTheDocument()

    rerender(
      <MemoryRouter>
        <PlaceholderPage title="Workouts" description="Soon" showBack={false} />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('button', { name: 'Back' })).not.toBeInTheDocument()
  })

  it('applies the variant, size, width and custom class to a button', () => {
    render(
      <Button variant="danger" size="lg" fullWidth className="extra">
        Delete
      </Button>,
    )

    expect(screen.getByRole('button', { name: 'Delete' })).toHaveClass(
      'button--danger',
      'button--lg',
      'button--full',
      'extra',
    )
  })

  it('applies the variant, size, width and custom class to a button link', () => {
    render(
      <MemoryRouter>
        <ButtonLink to="/x" variant="ghost" size="sm" fullWidth className="extra">
          Go
        </ButtonLink>
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Go' })).toHaveClass(
      'button--ghost',
      'button--sm',
      'button--full',
      'extra',
    )
  })

  it('renders an input with a label, icon and hint', () => {
    render(<Input label="Email" icon={<span>@</span>} hint="We never share it" className="wide" />)

    const input = screen.getByLabelText('Email')
    expect(input).not.toHaveAttribute('aria-invalid')
    expect(input).toHaveAccessibleDescription('We never share it')
    expect(screen.getByText('@')).toBeInTheDocument()
  })

  it('renders a bare input without a label, icon or message', () => {
    render(<Input aria-label="Plain" id="plain" />)

    const input = screen.getByLabelText('Plain')
    expect(input).toHaveAttribute('id', 'plain')
    expect(input).not.toHaveAttribute('aria-describedby')
  })

  it('wraps focus from the first to the last element on shift+tab and ignores other keys', async () => {
    render(
      <Modal open title="Edit" onClose={() => {}}>
        <button>First</button>
        <button>Last</button>
      </Modal>,
    )
    const dialog = screen.getByRole('dialog')
    expect(dialog).toHaveFocus()

    await userEvent.keyboard('{Shift>}{Tab}{/Shift}')
    expect(screen.getByRole('button', { name: 'Last' })).toHaveFocus()

    await userEvent.keyboard('a')
    expect(screen.getByRole('button', { name: 'Last' })).toHaveFocus()
  })

  it('keeps focus on the dialog when nothing inside can be focused', async () => {
    render(
      <Modal open title="Empty" onClose={() => {}}>
        <p>Nothing to focus</p>
      </Modal>,
    )
    screen.getByRole('button', { name: 'Close' }).setAttribute('disabled', '')
    const focusable = screen.getByRole('dialog').querySelectorAll('button')
    focusable.forEach((button) => button.setAttribute('disabled', ''))

    await userEvent.keyboard('{Tab}')

    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('restores the previous focus when the modal closes', async () => {
    const { rerender } = render(
      <>
        <button>Trigger</button>
        <Modal open={false} title="Edit" onClose={() => {}}>
          <p>Body</p>
        </Modal>
      </>,
    )
    screen.getByRole('button', { name: 'Trigger' }).focus()

    rerender(
      <>
        <button>Trigger</button>
        <Modal open title="Edit" onClose={() => {}}>
          <p>Body</p>
        </Modal>
      </>,
    )
    rerender(
      <>
        <button>Trigger</button>
        <Modal open={false} title="Edit" onClose={() => {}}>
          <p>Body</p>
        </Modal>
      </>,
    )

    expect(screen.getByRole('button', { name: 'Trigger' })).toHaveFocus()
  })

  it('refuses to use the toast hook outside a provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<ToastConsumer />)).toThrow('useToast must be used inside a ToastProvider')
  })
})
