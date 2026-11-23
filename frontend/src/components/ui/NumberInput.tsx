import type { ChangeEvent, InputHTMLAttributes, KeyboardEvent } from 'react'
import './Input.css'

const BLOCKED_KEYS = new Set(['e', 'E', '+', '-', '.', ','])
const DECIMAL_PATTERN = /^\d*\.?\d{0,2}$/

interface NumberInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'onChange' | 'value'> {
  value: string
  onValueChange: (value: string) => void
  decimal?: boolean
  'aria-label': string
}

export function NumberInput({ value, onValueChange, decimal = false, className, ...rest }: NumberInputProps) {
  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (!decimal && BLOCKED_KEYS.has(event.key)) {
      event.preventDefault()
    }
  }
  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    if (!decimal) {
      onValueChange(event.target.value)
      return
    }
    const next = event.target.value.replace(',', '.')
    if (DECIMAL_PATTERN.test(next)) {
      onValueChange(next)
    }
  }
  return (
    <input
      {...rest}
      type={decimal ? 'text' : 'number'}
      inputMode={decimal ? 'decimal' : 'numeric'}
      value={value}
      className={['number-input', className].filter(Boolean).join(' ')}
      onKeyDown={handleKeyDown}
      onChange={handleChange}
      onFocus={(event) => event.target.select()}
    />
  )
}
