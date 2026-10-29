import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { login, register, WeightUnit } from '../../api/auth'
import { ApiError, setAccessToken } from '../../api/client'
import { getUserProfile } from '../../api/users'
import { AuthProvider } from '../../auth/AuthProvider'
import { LoginPage } from '../LoginPage/LoginPage'
import { RegisterPage } from './RegisterPage'

vi.mock('../../api/auth', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../api/auth')>()),
  login: vi.fn(),
  register: vi.fn(),
}))
vi.mock('../../api/users')

function renderRegister() {
  return render(
    <MemoryRouter initialEntries={['/register']}>
      <AuthProvider>
        <Routes>
          <Route path="register" element={<RegisterPage />} />
          <Route path="login" element={<LoginPage />} />
          <Route index element={<h1>Home page</h1>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

async function fillRequired() {
  await userEvent.type(screen.getByLabelText('First name'), 'Rafael')
  await userEvent.type(screen.getByLabelText('Email'), 'rafael@example.com')
  await userEvent.type(screen.getByLabelText('Password'), 'secret123')
}

const submit = () => userEvent.click(screen.getByRole('button', { name: 'Create account' }))

describe('RegisterPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  afterAll(() => {
    MotionGlobalConfig.skipAnimations = false
  })

  beforeEach(() => {
    vi.mocked(register).mockResolvedValue(undefined)
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

  it('shows the heading, the password hint and kilograms selected by default', () => {
    renderRegister()

    expect(screen.getByRole('heading', { level: 1, name: 'Create account' })).toBeInTheDocument()
    expect(screen.getByText('At least 8 characters')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Kg (Kilograms)' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Lbs (Pounds)' })).not.toBeChecked()
  })

  it('links to the login page', async () => {
    renderRegister()

    await userEvent.click(screen.getByRole('link', { name: 'Log in' }))

    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
  })

  it('does not call the API while required fields are missing', async () => {
    renderRegister()

    await submit()

    expect(screen.getByText('Enter your first name.')).toBeInTheDocument()
    expect(screen.getByText('Enter your email address.')).toBeInTheDocument()
    expect(screen.getByText('Choose a password.')).toBeInTheDocument()
    expect(register).not.toHaveBeenCalled()
  })

  it('flags a password that is too short once the field is left', async () => {
    renderRegister()

    await userEvent.type(screen.getByLabelText('Password'), 'short')
    await userEvent.tab()

    expect(screen.getByText('Use at least 8 characters.')).toBeInTheDocument()
  })

  it('registers with kilograms and no last name, then sends the user home', async () => {
    renderRegister()

    await fillRequired()
    await submit()

    expect(await screen.findByRole('heading', { name: 'Home page' })).toBeInTheDocument()
    expect(register).toHaveBeenCalledWith({
      email: 'rafael@example.com',
      password: 'secret123',
      firstName: 'Rafael',
      lastName: null,
      weightUnit: 0,
    })
    expect(login).toHaveBeenCalledWith({ email: 'rafael@example.com', password: 'secret123' })
  })

  it('sends the chosen unit and the last name', async () => {
    renderRegister()

    await fillRequired()
    await userEvent.type(screen.getByLabelText(/Last name/), ' Foica ')
    await userEvent.click(screen.getByRole('radio', { name: 'Lbs (Pounds)' }))
    await submit()

    await screen.findByRole('heading', { name: 'Home page' })
    expect(register).toHaveBeenCalledWith(expect.objectContaining({ lastName: 'Foica', weightUnit: 1 }))
  })

  it('sends the user to the login page with a notice when the account exists but login failed', async () => {
    vi.mocked(login).mockRejectedValueOnce(new ApiError(0, 'Network request failed'))
    renderRegister()

    await fillRequired()
    await submit()

    expect(await screen.findByRole('status')).toHaveTextContent('Your account was created. Log in to continue.')
    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
  })

  it('shows a loading state and locks the form while creating the account', async () => {
    vi.mocked(register).mockReturnValue(new Promise(() => undefined))
    renderRegister()

    await fillRequired()
    await submit()

    const button = await screen.findByRole('button', { name: 'Creating account…' })
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByLabelText('Email')).toBeDisabled()
    expect(screen.getByRole('radio', { name: 'Kg (Kilograms)' })).toBeDisabled()
  })

  it('marks the email field when the account already exists, and clears it on edit', async () => {
    vi.mocked(register).mockRejectedValueOnce(
      new ApiError(400, 'An account with this email already exists.', 'BLException_AuthService_EmailAlreadyExists'),
    )
    renderRegister()

    await fillRequired()
    await submit()

    expect(await screen.findByText('An account with this email already exists.')).toBeInTheDocument()
    expect(screen.getByText(/We couldn’t create your account/)).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    await userEvent.type(screen.getByLabelText('Email'), 'x')
    expect(screen.queryByText('An account with this email already exists.')).not.toBeInTheDocument()
  })

  it('maps server validation errors onto the matching fields', async () => {
    vi.mocked(register).mockRejectedValueOnce(
      new ApiError(400, 'The request is not valid.', 'BLException_Validation_InvalidRequest', [
        { property: 'FirstName', message: 'First name is too long.' },
        { property: 'Unknown', message: 'ignored' },
      ]),
    )
    renderRegister()

    await fillRequired()
    await submit()

    expect(await screen.findByText('First name is too long.')).toBeInTheDocument()
    expect(screen.getByText(/We couldn’t create your account/)).toBeInTheDocument()
    expect(screen.queryByText('ignored')).not.toBeInTheDocument()
  })

  it('shows a friendly message for other failures', async () => {
    vi.mocked(register).mockRejectedValueOnce(new ApiError(500, 'boom'))
    renderRegister()

    await fillRequired()
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('The server ran into an issue.')
  })

  it('redirects an already logged-in user to the home page', async () => {
    localStorage.setItem('gymrat.session', JSON.stringify({ accessToken: 'stored', expiresAt: '2999-01-01T00:00:00Z' }))

    renderRegister()

    expect(await screen.findByRole('heading', { name: 'Home page' })).toBeInTheDocument()
  })
})
