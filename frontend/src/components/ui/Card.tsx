import type { HTMLAttributes } from 'react'
import './Card.css'

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  tone?: 'surface' | 'material'
}

export function Card({ tone = 'surface', className, ...rest }: CardProps) {
  return <div {...rest} className={['card', `card--${tone}`, className].filter(Boolean).join(' ')} />
}
