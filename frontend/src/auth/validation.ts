const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export const MINIMUM_PASSWORD_LENGTH = 8
export const MAXIMUM_NAME_LENGTH = 50
export const MAXIMUM_EMAIL_LENGTH = 100

export function validateEmail(value: string): string | undefined {
  const email = value.trim()
  if (!email) {
    return 'Enter your email address.'
  }
  if (!EMAIL_PATTERN.test(email) || email.length > MAXIMUM_EMAIL_LENGTH) {
    return 'Enter a valid email address.'
  }
  return undefined
}

export function validateLoginPassword(value: string): string | undefined {
  return value ? undefined : 'Enter your password.'
}

export function validateNewPassword(value: string): string | undefined {
  if (!value) {
    return 'Choose a password.'
  }
  if (value.length < MINIMUM_PASSWORD_LENGTH) {
    return `Use at least ${MINIMUM_PASSWORD_LENGTH} characters.`
  }
  return undefined
}

export function validateFirstName(value: string): string | undefined {
  const name = value.trim()
  if (!name) {
    return 'Enter your first name.'
  }
  if (name.length > MAXIMUM_NAME_LENGTH) {
    return `Use at most ${MAXIMUM_NAME_LENGTH} characters.`
  }
  return undefined
}

export function validateLastName(value: string): string | undefined {
  return value.trim().length > MAXIMUM_NAME_LENGTH ? `Use at most ${MAXIMUM_NAME_LENGTH} characters.` : undefined
}
