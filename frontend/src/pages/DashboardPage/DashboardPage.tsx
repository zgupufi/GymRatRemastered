import { useState, type CSSProperties } from 'react'
import { motion } from 'motion/react'
import { BookOpen, CalendarDays, ChevronRight, CircleHelp, Dumbbell, LineChart, Play, Settings, type LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { ButtonLink } from '../../components/ui/ButtonLink'
import { spring } from '../../lib/motion'
import { HowToModal } from './HowToModal'
import { SettingsModal } from './SettingsModal'
import './DashboardPage.css'

interface Module {
  to: string
  title: string
  description: string
  icon: LucideIcon
  color: string
}

const modules: Module[] = [
  { to: '/workouts', title: 'Workouts', description: 'Build & customize your fitness routines', icon: Dumbbell, color: 'var(--blue)' },
  { to: '/split', title: 'Training Split', description: 'Organize your weekly schedule', icon: CalendarDays, color: 'var(--purple)' },
  { to: '/progress', title: 'Progression', description: 'Track volume, 1RM and history', icon: LineChart, color: 'var(--emerald)' },
  { to: '/exercises', title: 'Exercise DB', description: 'Master forms with detailed instructions', icon: BookOpen, color: 'var(--orange)' },
]

export function DashboardPage() {
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [howToOpen, setHowToOpen] = useState(false)

  return (
    <div className="dashboard">
      <header className="dashboard__nav">
        <nav className="dashboard__nav-inner" aria-label="Main">
          <Link to="/" className="dashboard__logo" aria-label="GymRat home">
            <span className="dashboard__logo-mark" aria-hidden="true" />
            GymRat
          </Link>
          <Button variant="secondary" size="sm" onClick={() => setSettingsOpen(true)}>
            <Settings size={16} aria-hidden="true" />
            Settings
          </Button>
        </nav>
      </header>

      <main className="dashboard__main">
        <motion.section
          className="dashboard__hero"
          aria-labelledby="dashboard-title"
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={spring}
        >
          <div>
            <span className="dashboard__eyebrow">Today</span>
            <h1 id="dashboard-title">Ready to crush your goals?</h1>
            <p>Your workout is ready. Execute with intensity, log your sets, and enforce progressive overload.</p>
          </div>
          <ButtonLink to="/workout-execution" size="lg" className="dashboard__start">
            <Play size={18} fill="currentColor" aria-hidden="true" />
            Start Workout
          </ButtonLink>
        </motion.section>

        <section aria-labelledby="modules-title">
          <div className="dashboard__section-head">
            <h2 id="modules-title">Core Modules</h2>
            <Button variant="secondary" size="sm" onClick={() => setHowToOpen(true)}>
              <CircleHelp size={16} aria-hidden="true" />
              How to use GymRat
            </Button>
          </div>
          <ul className="dashboard__modules">
            {modules.map((module, index) => {
              const Icon = module.icon
              return (
                <motion.li
                  key={module.to}
                  initial={{ opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ ...spring, delay: 0.05 * index }}
                >
                  <Link to={module.to} className="module" style={{ '--module-color': module.color } as CSSProperties}>
                    <span className="module__icon" aria-hidden="true">
                      <Icon size={26} />
                    </span>
                    <span className="module__chevron" aria-hidden="true">
                      <ChevronRight size={18} />
                    </span>
                    <span className="module__text">
                      <strong>{module.title}</strong>
                      <span>{module.description}</span>
                    </span>
                  </Link>
                </motion.li>
              )
            })}
          </ul>
        </section>
      </main>

      <SettingsModal open={settingsOpen} onClose={() => setSettingsOpen(false)} />
      <HowToModal open={howToOpen} onClose={() => setHowToOpen(false)} />
    </div>
  )
}
