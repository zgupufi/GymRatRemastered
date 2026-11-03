import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthApi, type AuthStatus } from '../../auth/AuthContext'
import { ChatErrorBoundary } from '../Chat/ChatErrorBoundary'
import { AppShell } from './AppShell'

vi.mock('../Chat/ChatWidget', () => ({ ChatWidget: () => <div>chat widget</div> }))

function renderShell(path: string, status: AuthStatus) {
  const auth = { status } as AuthApi
  window.scrollTo = vi.fn()
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthContext.Provider value={auth}>
        <Routes>
          <Route element={<AppShell />}>
            <Route path="*" element={<p>page</p>} />
          </Route>
        </Routes>
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('AppShell chat widget', () => {
  afterEach(() => {
    sessionStorage.clear()
  })

  it('shows the chat when logged in', () => {
    renderShell('/workouts', 'authenticated')

    expect(screen.getByText('chat widget')).toBeInTheDocument()
  })

  it('hides the chat on the workout execution page', () => {
    renderShell('/workout-execution', 'authenticated')

    expect(screen.queryByText('chat widget')).not.toBeInTheDocument()
  })

  it.each<AuthStatus>(['anonymous', 'loading'])('hides the chat while %s', (status) => {
    renderShell('/', status)

    expect(screen.queryByText('chat widget')).not.toBeInTheDocument()
  })

  it('forgets the conversation once logged out', () => {
    sessionStorage.setItem('gymrat_chat_history', '[]')

    renderShell('/', 'anonymous')

    expect(sessionStorage.getItem('gymrat_chat_history')).toBeNull()
  })

  it('keeps the conversation while the session is loading', () => {
    sessionStorage.setItem('gymrat_chat_history', '[]')

    renderShell('/', 'loading')

    expect(sessionStorage.getItem('gymrat_chat_history')).toBe('[]')
  })
})

describe('ChatErrorBoundary', () => {
  it('shows a fallback instead of crashing the page when the chat throws', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    const Broken = () => {
      throw new Error('boom')
    }

    render(
      <ChatErrorBoundary>
        <Broken />
      </ChatErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('The coach hit a problem')
    vi.restoreAllMocks()
  })
})
