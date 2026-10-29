import { useState, type FormEvent } from 'react'
import { LogIn, Lock, Mail } from 'lucide-react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { friendlyErrorMessage } from '../../api/errors'
import { useAuth } from '../../auth/AuthContext'
import { validateEmail, validateLoginPassword } from '../../auth/validation'
import { AuthBanner, AuthLayout, AuthNotice } from '../../components/auth/AuthLayout'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'

type Field = 'email' | 'password'

export function LoginPage() {
  const { status, login } = useAuth()
  const location = useLocation()
  const notice = (location.state as { notice?: string } | null)?.notice
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [touched, setTouched] = useState<Record<Field, boolean>>({ email: false, password: false })
  const [loading, setLoading] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  if (status === 'authenticated') {
    return <Navigate to="/" replace />
  }

  const errors: Record<Field, string | undefined> = {
    email: touched.email ? validateEmail(email) : undefined,
    password: touched.password ? validateLoginPassword(password) : undefined,
  }

  const touch = (field: Field) => setTouched((current) => ({ ...current, [field]: true }))

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setTouched({ email: true, password: true })
    if (validateEmail(email) || validateLoginPassword(password)) {
      return
    }
    setFailure(null)
    setLoading(true)
    try {
      await login(email.trim(), password)
    } catch (error) {
      setFailure(friendlyErrorMessage(error))
      setLoading(false)
    }
  }

  return (
    <AuthLayout
      accent="blue"
      title="Welcome back"
      subtitle="Log in to your account to continue your progress."
      footer={
        <>
          Don’t have an account yet?
          <Link to="/register">Sign up</Link>
        </>
      }
    >
      <form className="auth__form" onSubmit={handleSubmit} noValidate>
        {notice && !failure ? <AuthNotice message={notice} /> : null}
        {failure ? <AuthBanner message={failure} /> : null}
        <fieldset className="auth__fields" disabled={loading}>
          <Input
            label="Email"
            type="email"
            name="email"
            autoComplete="email"
            placeholder="name@example.com"
            icon={<Mail size={20} />}
            value={email}
            error={errors.email}
            onChange={(event) => setEmail(event.target.value)}
            onBlur={() => touch('email')}
          />
          <Input
            label="Password"
            type="password"
            name="password"
            autoComplete="current-password"
            placeholder="••••••••"
            icon={<Lock size={20} />}
            value={password}
            error={errors.password}
            onChange={(event) => setPassword(event.target.value)}
            onBlur={() => touch('password')}
          />
        </fieldset>
        <Button type="submit" variant="blue" size="lg" fullWidth loading={loading} className="auth__submit">
          {loading ? null : <LogIn size={20} aria-hidden="true" />}
          {loading ? 'Logging in…' : 'Log in'}
        </Button>
      </form>
    </AuthLayout>
  )
}
