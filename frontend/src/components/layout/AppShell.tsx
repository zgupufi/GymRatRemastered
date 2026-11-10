import { useEffect } from 'react'
import { MotionConfig } from 'motion/react'
import { Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { installHaptics } from '../../lib/haptics'
import { pageTitle } from '../../lib/pageTitle'
import { clearChatEntries } from '../Chat/chatStorage'
import { ChatErrorBoundary } from '../Chat/ChatErrorBoundary'
import { ChatWidget } from '../Chat/ChatWidget'
import { ToastProvider } from '../ui/Toast'
import { AppNav } from './AppNav'
import { ScrollToTop } from './ScrollToTop'
import './AppShell.css'

export function AppShell() {
  const { pathname } = useLocation()
  const { status } = useAuth()
  useEffect(() => installHaptics(), [])
  useEffect(() => {
    document.title = pageTitle(pathname)
  }, [pathname])
  useEffect(() => {
    if (status === 'anonymous') {
      clearChatEntries()
    }
  }, [status])

  const showNav = status === 'authenticated' && pathname !== '/workout-execution'

  return (
    <MotionConfig reducedMotion="user">
      <ToastProvider>
        <ScrollToTop />
        <div className={`app-shell${pathname === '/register' ? ' app-shell--mirrored' : ''}${showNav ? ' app-shell--nav' : ''}`}>
          <div className="app-shell__glow app-shell__glow--blue" aria-hidden="true" />
          <div className="app-shell__glow app-shell__glow--emerald" aria-hidden="true" />
          {showNav ? <AppNav /> : null}
          <Outlet />
          {status === 'authenticated' && pathname !== '/workout-execution' ? (
            <ChatErrorBoundary>
              <ChatWidget />
            </ChatErrorBoundary>
          ) : null}
        </div>
      </ToastProvider>
    </MotionConfig>
  )
}
