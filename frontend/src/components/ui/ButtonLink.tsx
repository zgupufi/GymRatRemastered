import { motion } from 'motion/react'
import { Link, type LinkProps } from 'react-router-dom'
import { press, spring } from '../../lib/motion'
import type { ButtonSize, ButtonVariant } from './Button'
import './Button.css'

const MotionLink = motion.create(Link)

type MotionConflicts = 'onAnimationStart' | 'onDrag' | 'onDragStart' | 'onDragEnd' | 'onDragOver' | 'onDragEnter' | 'onDragLeave' | 'onDrop' | 'style'

interface ButtonLinkProps extends Omit<LinkProps, MotionConflicts> {
  variant?: ButtonVariant
  size?: ButtonSize
  fullWidth?: boolean
}

export function ButtonLink({ variant = 'primary', size = 'md', fullWidth = false, className, ...rest }: ButtonLinkProps) {
  const classes = ['button', `button--${variant}`, `button--${size}`, fullWidth ? 'button--full' : '', className]
    .filter(Boolean)
    .join(' ')
  return <MotionLink {...rest} className={classes} whileTap={press} transition={spring} />
}
