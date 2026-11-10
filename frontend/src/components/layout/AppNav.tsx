import { useState } from 'react'
import { BookOpen, CalendarDays, CircleHelp, Dumbbell, House, LineChart, Settings, type LucideIcon } from 'lucide-react'
import { Link, NavLink } from 'react-router-dom'
import { HowToModal } from '../../pages/DashboardPage/HowToModal'
import { SettingsModal } from '../../pages/DashboardPage/SettingsModal'
import './AppNav.css'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
}

const navItems: NavItem[] = [
  { to: '/', label: 'Home', icon: House },
  { to: '/workouts', label: 'Workouts', icon: Dumbbell },
  { to: '/split', label: 'Split', icon: CalendarDays },
  { to: '/progress', label: 'Progress', icon: LineChart },
  { to: '/exercises', label: 'Library', icon: BookOpen },
]

export function AppNav() {
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [howToOpen, setHowToOpen] = useState(false)

  return (
    <>
      <header className="appnav">
        <div className="appnav__inner">
          <Link to="/" className="appnav__logo" aria-label="GymRat home">
            GymRat
          </Link>
          <nav className="appnav__links" aria-label="Main">
            {navItems.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.to === '/'} className="appnav__link">
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className="appnav__actions">
            <button type="button" className="appnav__action" onClick={() => setHowToOpen(true)}>
              <CircleHelp size={16} aria-hidden="true" />
              <span>How to use</span>
            </button>
            <button type="button" className="appnav__action" onClick={() => setSettingsOpen(true)}>
              <Settings size={16} aria-hidden="true" />
              <span>Settings</span>
            </button>
          </div>
        </div>
      </header>
      <nav className="tabbar" aria-label="Sections">
        {navItems.map((item) => {
          const Icon = item.icon
          return (
            <NavLink key={item.to} to={item.to} end={item.to === '/'} className="tabbar__item">
              <Icon size={22} aria-hidden="true" />
              <span>{item.label}</span>
            </NavLink>
          )
        })}
      </nav>
      <SettingsModal open={settingsOpen} onClose={() => setSettingsOpen(false)} />
      <HowToModal open={howToOpen} onClose={() => setHowToOpen(false)} />
    </>
  )
}
