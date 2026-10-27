import type { ReactNode } from 'react'
import { motion, type HTMLMotionProps } from 'motion/react'
import { press, spring } from '../../lib/motion'
import './Button.css'

export type ButtonVariant = 'primary' | 'blue' | 'purple' | 'secondary' | 'danger' | 'ghost'
export type ButtonSize = 'sm' | 'md' | 'lg'

interface ButtonProps extends Omit<HTMLMotionProps<'button'>, 'children'> {
  variant?: ButtonVariant
  size?: ButtonSize
  fullWidth?: boolean
  children?: ReactNode
}

export function Button({
  variant = 'primary',
  size = 'md',
  fullWidth = false,
  className,
  type = 'button',
  disabled,
  children,
  ...rest
}: ButtonProps) {
  const classes = ['button', `button--${variant}`, `button--${size}`, fullWidth ? 'button--full' : '', className]
    .filter(Boolean)
    .join(' ')
  return (
    <motion.button
      {...rest}
      type={type}
      disabled={disabled}
      className={classes}
      whileTap={disabled ? undefined : press}
      transition={spring}
    >
      {children}
    </motion.button>
  )
}
