import { useState, type FormEvent } from 'react'
import { ArrowRight, Lock, Mail, User } from 'lucide-react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { WeightUnit } from '../../api/auth'
import { ApiError } from '../../api/client'
import { friendlyErrorMessage } from '../../api/errors'
import { LoginAfterRegisterError, useAuth } from '../../auth/AuthContext'
import {
  MINIMUM_PASSWORD_LENGTH,
  validateEmail,
  validateFirstName,
  validateLastName,
  validateNewPassword,
} from '../../auth/validation'
import { AuthBanner, AuthLayout } from '../../components/auth/AuthLayout'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { SegmentedControl } from '../../components/ui/SegmentedControl'

type Field = 'firstName' | 'lastName' | 'email' | 'password'
type UnitChoice = 'kg' | 'lbs'

const EMAIL_EXISTS_CODE = 'BLException_AuthService_EmailAlreadyExists'
const FIELDS: Field[] = ['firstName', 'lastName', 'email', 'password']
const ACCOUNT_CREATED = 'Your account was created. Log in to continue.'
const CREATE_FAILED ='We couldn’t create your account. Fix the highlighted fields and try again.'

const UNIT_OPTIONS: { value: UnitChoice; label: string }[] = [
  { value: 'kg', label: 'Kg (Kilograms)' },
  { value: 'lbs', label: 'Lbs (Pounds)' },
]

function isField(value: string): value is Field {
  return (FIELDS as string[]).includes(value)
}

function lowerFirst(value: string): string {
  return value.charAt(0).toLowerCase() + value.slice(1)
}

export function RegisterPage() {
  const { status, register } = useAuth()
  const navigate = useNavigate()
  const [values, setValues] = useState<Record<Field, string>>({ firstName: '', lastName: '', email: '', password: '' })
  const [unit, setUnit] = useState<UnitChoice>('kg')
  const [touched, setTouched] = useState<Record<Field, boolean>>({
    firstName: false,
    lastName: false,
    email: false,
    password: false,
  })
  const [serverErrors, setServerErrors] = useState<Partial<Record<Field, string>>>({})
  const [loading, setLoading] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  if (status === 'authenticated') {
    return <Navigate to="/" replace />
  }

  const checks: Record<Field, string | undefined> = {
    firstName: validateFirstName(values.firstName),
    lastName: validateLastName(values.lastName),
    email: validateEmail(values.email),
    password: validateNewPassword(values.password),
  }

  const errorFor = (field: Field) => (touched[field] ? checks[field] : undefined) ?? serverErrors[field]

  const update = (field: Field, value: string) => {
    setValues((current) => ({ ...current, [field]: value }))
    setServerErrors((current) => ({ ...current, [field]: undefined }))
  }

  const touch = (field: Field) => setTouched((current) => ({ ...current, [field]: true }))

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setTouched({ firstName: true, lastName: true, email: true, password: true })
    if (FIELDS.some((field) => checks[field])) {
      return
    }
    setFailure(null)
    setServerErrors({})
    setLoading(true)
    try {
      await register({
        email: values.email.trim(),
        password: values.password,
        firstName: values.firstName.trim(),
        lastName: values.lastName.trim() || null,
        weightUnit: unit === 'kg' ? WeightUnit.Kilograms : WeightUnit.Pounds,
      })
    } catch (error) {
      if (error instanceof LoginAfterRegisterError) {
        navigate('/login', { replace: true, state: { notice: ACCOUNT_CREATED } })
        return
      }
      if (error instanceof ApiError && error.code === EMAIL_EXISTS_CODE) {
        setServerErrors({ email: error.message })
        setFailure(CREATE_FAILED)
      } else if (error instanceof ApiError && error.fieldErrors.length > 0) {
        const mapped: Partial<Record<Field, string>> = {}
        for (const { property, message } of error.fieldErrors) {
          const field = lowerFirst(property)
          if (isField(field)) {
            mapped[field] = message
          }
        }
        setServerErrors(mapped)
        setFailure(CREATE_FAILED)
      } else {
        setFailure(friendlyErrorMessage(error))
      }
      setLoading(false)
    }
  }

  return (
    <AuthLayout
      accent="emerald"
      wide
      title="Create account"
      subtitle="Start your transformation right now."
      footer={
        <>
          Already have an account?
          <Link to="/login">Log in</Link>
        </>
      }
    >
      <form className="auth__form" onSubmit={handleSubmit} noValidate>
        {failure ? <AuthBanner message={failure} /> : null}
        <fieldset className="auth__fields" disabled={loading}>
          <div className="auth__row">
            <Input
              label="First name"
              name="firstName"
              autoComplete="given-name"
              placeholder="John"
              icon={<User size={20} />}
              value={values.firstName}
              error={errorFor('firstName')}
              onChange={(event) => update('firstName', event.target.value)}
              onBlur={() => touch('firstName')}
            />
            <Input
              label="Last name"
              optional
              name="lastName"
              autoComplete="family-name"
              placeholder="Doe"
              icon={<User size={20} />}
              value={values.lastName}
              error={errorFor('lastName')}
              onChange={(event) => update('lastName', event.target.value)}
              onBlur={() => touch('lastName')}
            />
          </div>
          <Input
            label="Email"
            type="email"
            name="email"
            autoComplete="email"
            placeholder="name@example.com"
            icon={<Mail size={20} />}
            value={values.email}
            error={errorFor('email')}
            onChange={(event) => update('email', event.target.value)}
            onBlur={() => touch('email')}
          />
          <Input
            label="Password"
            type="password"
            name="password"
            autoComplete="new-password"
            placeholder="••••••••"
            icon={<Lock size={20} />}
            hint={`At least ${MINIMUM_PASSWORD_LENGTH} characters`}
            value={values.password}
            error={errorFor('password')}
            onChange={(event) => update('password', event.target.value)}
            onBlur={() => touch('password')}
          />
          <div className="auth__group">
            <span className="auth__group-label">Weight unit</span>
            <SegmentedControl label="Weight unit" options={UNIT_OPTIONS} value={unit} onChange={setUnit} wide />
          </div>
        </fieldset>
        <Button type="submit" size="lg" fullWidth loading={loading} className="auth__submit">
          {loading ? 'Creating account…' : 'Create account'}
          {loading ? null : <ArrowRight size={20} aria-hidden="true" />}
        </Button>
      </form>
    </AuthLayout>
  )
}
