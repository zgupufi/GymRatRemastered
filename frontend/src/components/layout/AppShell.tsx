import { useEffect } from 'react'
import { MotionConfig } from 'motion/react'
import { Outlet } from 'react-router-dom'
import { installHaptics } from '../../lib/haptics'
import { ToastProvider } from '../ui/Toast'
import { ScrollToTop } from './ScrollToTop'
import './AppShell.css'

export function AppShell() {
  useEffect(() => installHaptics(), [])

  return (
    <MotionConfig reducedMotion="user">
      <ToastProvider>
        <ScrollToTop />
        <div className="app-shell">
          <div className="app-shell__glow app-shell__glow--blue" aria-hidden="true" />
          <div className="app-shell__glow app-shell__glow--emerald" aria-hidden="true" />
          <Outlet />
        </div>
      </ToastProvider>
    </MotionConfig>
  )
}
