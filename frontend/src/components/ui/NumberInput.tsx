import type { ChangeEvent, InputHTMLAttributes, KeyboardEvent } from 'react'
import './Input.css'

const BLOCKED_KEYS = new Set(['e', 'E', '+', '-', '.', ','])

interface NumberInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'onChange' | 'value'> {
  value: string
  onValueChange: (value: string) => void
  'aria-label': string
}

export function NumberInput({ value, onValueChange, className, ...rest }: NumberInputProps) {
  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (BLOCKED_KEYS.has(event.key)) {
      event.preventDefault()
    }
  }
  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    onValueChange(event.target.value)
  }
  return (
    <input
      {...rest}
      type="number"
      inputMode="numeric"
      value={value}
      className={['number-input', className].filter(Boolean).join(' ')}
      onKeyDown={handleKeyDown}
      onChange={handleChange}
      onFocus={(event) => event.target.select()}
    />
  )
}
