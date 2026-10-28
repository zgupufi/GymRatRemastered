import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { useState } from 'react'
import { Button } from './Button'
import { Input } from './Input'
import { Modal } from './Modal'
import { NumberInput } from './NumberInput'
import { SegmentedControl } from './SegmentedControl'
import { Spinner } from './Spinner'
import { Switch } from './Switch'
import { ToastProvider } from './Toast'
import { useToast } from './ToastContext'

function ToastTrigger() {
  const { showToast } = useToast()
  return (
    <>
      <button onClick={() => showToast('success', 'Saved')}>ok</button>
      <button onClick={() => showToast('error', 'Failed')}>fail</button>
    </>
  )
}

function ModalHarness({ onClose }: { onClose: () => void }) {
  return (
    <Modal open title="Choose workout" onClose={onClose}>
      <p>Body</p>
    </Modal>
  )
}

function NumberHarness() {
  const [value, setValue] = useState('')
  return <NumberInput aria-label="Weight" value={value} onValueChange={setValue} />
}

describe('ui components', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('does not fire a disabled button', async () => {
    const onClick = vi.fn()
    render(
      <Button disabled onClick={onClick}>
        Save
      </Button>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(onClick).not.toHaveBeenCalled()
  })

  it('shows an inline error on an input and links it to the field', () => {
    render(<Input label="Password" error="Use at least 8 characters." />)

    const input = screen.getByLabelText('Password')

    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input).toHaveAccessibleDescription('Use at least 8 characters.')
  })

  it('blocks the keys that make no sense in a number field', async () => {
    render(<NumberHarness />)
    const input = screen.getByRole('spinbutton', { name: 'Weight' })

    await userEvent.type(input, '1e+-.,2')

    expect(input).toHaveValue(12)
  })

  it('switches the segmented control selection', async () => {
    const onChange = vi.fn()
    render(
      <SegmentedControl
        label="Weight unit"
        options={[
          { value: 'KG', label: 'KG' },
          { value: 'LBS', label: 'LBS' },
        ]}
        value="KG"
        onChange={onChange}
      />,
    )

    await userEvent.click(screen.getByRole('radio', { name: 'LBS' }))

    expect(screen.getByRole('radio', { name: 'KG' })).toBeChecked()
    expect(onChange).toHaveBeenCalledWith('LBS')
  })

  it('announces the spinner', () => {
    render(<Spinner label="Loading exercises…" />)

    expect(screen.getByRole('status')).toHaveTextContent('Loading exercises…')
  })

  it('closes the modal with the close button, escape and the scrim', async () => {
    const onClose = vi.fn()
    render(<ModalHarness onClose={onClose} />)

    expect(screen.getByRole('dialog', { name: 'Choose workout' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))
    await userEvent.keyboard('{Escape}')
    await userEvent.click(screen.getByRole('dialog').parentElement!)

    expect(onClose).toHaveBeenCalledTimes(3)
  })

  it('keeps the modal open when its content is clicked', async () => {
    const onClose = vi.fn()
    render(<ModalHarness onClose={onClose} />)

    await userEvent.click(screen.getByText('Body'))

    expect(onClose).not.toHaveBeenCalled()
  })

  it('shows a toast and removes it after a few seconds', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    render(
      <ToastProvider>
        <ToastTrigger />
      </ToastProvider>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'ok' }))
    expect(await screen.findByText('Saved')).toBeInTheDocument()

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3500)
    })

    expect(screen.queryByText('Saved')).not.toBeInTheDocument()
  })

  it('replaces the current toast with a newer one', async () => {
    render(
      <ToastProvider>
        <ToastTrigger />
      </ToastProvider>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'ok' }))
    await userEvent.click(screen.getByRole('button', { name: 'fail' }))

    expect(await screen.findByText('Failed')).toBeInTheDocument()
  })
})

describe('ui components, second batch', () => {
  it('keeps focus in a modal input while the parent re-renders with an inline onClose', async () => {
    function Harness() {
      const [text, setText] = useState('')
      return (
        <Modal open title="Rename" onClose={() => setText('')}>
          <input aria-label="Name" value={text} onChange={(event) => setText(event.target.value)} />
        </Modal>
      )
    }
    render(<Harness />)
    const input = screen.getByLabelText('Name')
    await userEvent.click(input)

    await userEvent.keyboard('abc')

    expect(input).toHaveValue('abc')
    expect(input).toHaveFocus()
  })

  it('traps tab inside the modal and locks body scroll', async () => {
    render(
      <>
        <button>outside</button>
        <Modal open title="Trap" onClose={() => undefined}>
          <button>inner</button>
        </Modal>
      </>,
    )

    expect(document.body.style.overflow).toBe('hidden')
    screen.getByRole('button', { name: 'Close' }).focus()
    await userEvent.tab()
    expect(screen.getByRole('button', { name: 'inner' })).toHaveFocus()
    await userEvent.tab()
    expect(screen.getByRole('button', { name: 'Close' })).toHaveFocus()
  })

  it('toggles a switch', async () => {
    function Harness() {
      const [on, setOn] = useState(false)
      return <Switch label="Notifications" checked={on} onChange={setOn} />
    }
    render(<Harness />)

    await userEvent.click(screen.getByRole('switch', { name: 'Notifications' }))

    expect(screen.getByRole('switch', { name: 'Notifications' })).toBeChecked()
  })

  it('disables a loading button and marks it busy', () => {
    render(<Button loading>Saving…</Button>)

    expect(screen.getByRole('button', { name: 'Saving…' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Saving…' })).toHaveAttribute('aria-busy', 'true')
  })

  it('announces an error toast as an alert', async () => {
    render(
      <ToastProvider>
        <ToastTrigger />
      </ToastProvider>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'fail' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Failed')
  })
})
