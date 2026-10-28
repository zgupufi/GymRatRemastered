import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MotionGlobalConfig } from 'motion/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeAll, describe, expect, it } from 'vitest'
import { LandingPage } from './LandingPage'

function renderLanding() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route index element={<LandingPage />} />
        <Route path="register" element={<h1>Register page</h1>} />
        <Route path="login" element={<h1>Login page</h1>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('LandingPage', () => {
  beforeAll(() => {
    MotionGlobalConfig.skipAnimations = true
  })

  it('renders the hero headline', () => {
    renderLanding()

    expect(
      screen.getByRole('heading', { level: 1, name: 'Log workouts. Get stronger. Stay motivated.' }),
    ).toBeInTheDocument()
  })

  it.each(['Sign up', 'Start now', 'Create free account'])('links %s to the register page', (name) => {
    renderLanding()

    expect(screen.getByRole('link', { name })).toHaveAttribute('href', '/register')
  })

  it('links every log in call to action to the login page', () => {
    renderLanding()

    const links = screen.getAllByRole('link', { name: 'Log in' })

    expect(links).toHaveLength(3)
    links.forEach((link) => expect(link).toHaveAttribute('href', '/login'))
  })

  it('navigates to the register page from the hero call to action', async () => {
    renderLanding()

    await userEvent.click(screen.getByRole('link', { name: 'Start now' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Register page' })).toBeInTheDocument()
  })

  it('navigates to the login page from the nav', async () => {
    renderLanding()

    const nav = screen.getByRole('navigation', { name: 'Main' })
    await userEvent.click(within(nav).getByRole('link', { name: 'Log in' }))

    expect(screen.getByRole('heading', { level: 1, name: 'Login page' })).toBeInTheDocument()
  })

  it.each([
    ['Log workouts seamlessly', 'Intuitive workout logging'],
    ['Measure your progress', 'Clear progress charts'],
    ['Your personal AI coach', 'Generate custom routines instantly'],
  ])('renders the %s feature section', (title, point) => {
    renderLanding()

    const section = screen.getByRole('region', { name: title })

    expect(within(section).getByRole('heading', { level: 2, name: title })).toBeInTheDocument()
    expect(within(section).getAllByRole('listitem')).toHaveLength(4)
    expect(within(section).getByText(point)).toBeInTheDocument()
  })

  it('hides the decorative mockups from assistive technology', () => {
    renderLanding()

    expect(screen.queryByText('Pec deck / butterfly', { ignore: '[aria-hidden="true"] *' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Save changes' })).not.toBeInTheDocument()
  })

  it('shows the current year in the footer', () => {
    renderLanding()

    expect(
      screen.getByText(`© ${new Date().getFullYear()} GymRat. Engineered for athletes.`),
    ).toBeInTheDocument()
  })
})
