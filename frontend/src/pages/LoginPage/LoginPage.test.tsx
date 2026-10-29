import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, WeightUnit } from '../../api/auth'
import { ApiError, setAccessToken } from '../../api/client'
import { getUserProfile } from '../../api/users'
import { AuthProvider } from '../../auth/AuthProvider'
import { LoginPage } from './LoginPage'

vi.mock('../../api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/auth')>()),
  login: vi.fn(),
}))
vi.mock('../../api/users')

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<h1>Register page</h1>} />
          <Route index element={<h1>Home page</h1>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

async function fillAndSubmit(email: string, password: string) {
  await userEvent.type(screen.getByLabelText('Email'), email)
  await userEvent.type(screen.getByLabelText('Password'), password)
  await userEvent.click(screen.getByRole('button', { name: 'Log in' }))
}

describe('LoginPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterAll(() => {
    MotionGlobalConfig.skipAnimations = false
  })

  beforeEach(() => {
    vi.mocked(login).mockResolvedValue({ accessToken: 'token', expiresAt: '2999-01-01T00:00:00Z' })
    vi.mocked(getUserProfile).mockResolvedValue({
      firstName: 'Rafael',
      lastName: null,
      email: 'rafael@example.com',
      weightUnit: WeightUnit.Kilograms,
      defaultRestTimeSeconds: 90,
    })
  })

  afterEach(() => {
    localStorage.clear()
    setAccessToken(null)
    vi.resetAllMocks()
  })

  it('shows the welcome heading and a way back to the landing page', () => {
    renderLogin()

    expect(screen.getByRole('heading', { level: 1, name: 'Welcome back' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to home' })).toHaveAttribute('href', '/')
  })

  it('links to the register page', async () => {
    renderLogin()

    await userEvent.click(screen.getByRole('link', { name: 'Sign up' }))

    expect(await screen.findByRole('heading', { name: 'Register page' })).toBeInTheDocument()
  })

  it('validates each field when it loses focus', async () => {
    renderLogin()

    await userEvent.click(screen.getByLabelText('Email'))
    await userEvent.type(screen.getByLabelText('Email'), 'nope')
    await userEvent.tab()
    await userEvent.click(screen.getByLabelText('Password'))
    await userEvent.tab()

    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
  })

  it('does not call the API while the form is invalid', async () => {
    renderLogin()

    await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

    expect(screen.getByText('Enter your email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
    expect(login).not.toHaveBeenCalled()
  })

  it('logs in with the trimmed email and sends the user home', async () => {
    renderLogin()

    await userEvent.type(screen.getByLabelText('Email'), ' rafael@example.com ')
    await userEvent.type(screen.getByLabelText('Password'), 'secret123')
    await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('heading', { name: 'Home page' })).toBeInTheDocument()
    expect(login).toHaveBeenCalledWith({ email: 'rafael@example.com', password: 'secret123' })
  })

  it('shows a loading state and locks the fields while logging in', async () => {
    vi.mocked(login).mockReturnValue(new Promise(() => undefined))
    renderLogin()

    await fillAndSubmit('rafael@example.com', 'secret123')

    const button = await screen.findByRole('button', { name: 'Logging in…' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByLabelText('Email')).toBeDisabled()
    expect(screen.getByLabelText('Password')).toBeDisabled()
    expect(screen.getByRole('link', { name: 'Back to home' })).toBeInTheDocument()
  })

  it('shows the server message when the credentials are wrong and lets the user retry', async () => {
    vi.mocked(login).mockRejectedValueOnce(
      new ApiError(400, 'The email or password is incorrect.', 'BLException_AuthService_InvalidCredentials'),
    )
    renderLogin()

    await fillAndSubmit('rafael@example.com', 'wrong-password')

    expect(await screen.findByRole('alert')).toHaveTextContent('The email or password is incorrect.')
    expect(screen.getByLabelText('Email')).toBeEnabled()
    await userEvent.click(screen.getByRole('button', { name: 'Log in' }))
    expect(await screen.findByRole('heading', { name: 'Home page' })).toBeInTheDocument()
  })

  it('shows a friendly message when the server cannot be reached', async () => {
    vi.mocked(login).mockRejectedValueOnce(new ApiError(0, 'Network request failed'))
    renderLogin()

    await fillAndSubmit('rafael@example.com', 'secret123')

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not reach the server.')
  })

  it('redirects an already logged-in user to the home page', async () => {
    localStorage.setItem('gymrat.session', JSON.stringify({ accessToken: 'stored', expiresAt: '2999-01-01T00:00:00Z' }))

    renderLogin()

    expect(await screen.findByRole('heading', { name: 'Home page' })).toBeInTheDocument()
  })
})
