import type { ReactNode } from 'react'
import { motion } from 'motion/react'
import { ArrowLeft, CircleAlert } from 'lucide-react'
import { Link } from 'react-router-dom'
import { spring } from '../../lib/motion'
import './AuthLayout.css'

interface AuthLayoutProps {
  accent: 'blue' | 'emerald'
  wide?: boolean
  title: string
  subtitle: string
  footer: ReactNode
  children: ReactNode
}

export function AuthLayout({ accent, wide = false, title, subtitle, footer, children }: AuthLayoutProps) {
  return (
    <main className={`auth auth--${accent}`}>
      <Link to="/" className="auth__back" aria-label="Back to home">
        <ArrowLeft size={20} aria-hidden="true" />
      </Link>
      <motion.section
        className={`auth__card${wide ? ' auth__card--wide' : ''}`}
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={spring}
      >
        <header className="auth__header">
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </header>
        {children}
        <p className="auth__footer">{footer}</p>
      </motion.section>
    </main>
  )
}

export function AuthBanner({ message }: { message: string }) {
  return (
    <div className="auth__banner" role="alert">
      <CircleAlert size={18} aria-hidden="true" />
      <span>{message}</span>
    </div>
  )
}
