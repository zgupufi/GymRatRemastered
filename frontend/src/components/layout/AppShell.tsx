import { useEffect } from 'react'
import { MotionConfig } from 'motion/react'
import { Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { installHaptics } from '../../lib/haptics'
import { ChatWidget } from '../Chat/ChatWidget'
import { ToastProvider } from '../ui/Toast'
import { ScrollToTop } from './ScrollToTop'
import './AppShell.css'

export function AppShell() {
  const { pathname } = useLocation()
  const { status } = useAuth()
  useEffect(() => installHaptics(), [])

  return (
    <MotionConfig reducedMotion="user">
      <ToastProvider>
        <ScrollToTop />
        <div className={`app-shell${pathname === '/register' ? ' app-shell--mirrored' : ''}`}>
          <div className="app-shell__glow app-shell__glow--blue" aria-hidden="true" />
          <div className="app-shell__glow app-shell__glow--emerald" aria-hidden="true" />
          <Outlet />
          {status === 'authenticated' && pathname !== '/workout-execution' ? <ChatWidget /> : null}
        </div>
      </ToastProvider>
    </MotionConfig>
  )
}
