import type { ReactNode } from 'react'
import { ArrowRight, Bot, Check, Dumbbell, TrendingUp, type LucideIcon } from 'lucide-react'
import { motion } from 'motion/react'
import { Link } from 'react-router-dom'
import { ButtonLink } from '../../components/ui/ButtonLink'
import { spring } from '../../lib/motion'
import { CoachChatMockup, PhoneMockup, ProgressChartMockup, WorkoutCardMockup } from './LandingMockups'
import './LandingPage.css'

const currentYear = new Date().getFullYear()

const reveal = {
  initial: { opacity: 0, y: 24 },
  whileInView: { opacity: 1, y: 0 },
  viewport: { once: true, amount: 0.25 },
  transition: { ...spring, duration: 0.6 },
}

interface Feature {
  id: string
  tone: 'blue' | 'emerald' | 'cyan'
  icon: LucideIcon
  eyebrow: string
  title: string
  body: string
  points: string[]
  mockup: ReactNode
  reverse?: boolean
  band?: boolean
}

const features: Feature[] = [
  {
    id: 'logging',
    tone: 'blue',
    icon: Dumbbell,
    eyebrow: 'Logging',
    title: 'Log every set, fast',
    body: "Pick today's workout, enter weight and reps, and tick the set off. Rate how hard it felt, and a rest timer starts on its own.",
    points: [
      'Weight, reps and effort for each set',
      'Build your own routines and weekly split',
      'Rest timer with −10s and +30s',
      'Add or remove sets during a workout',
    ],
    mockup: <WorkoutCardMockup />,
  },
  {
    id: 'progress',
    tone: 'emerald',
    icon: TrendingUp,
    eyebrow: 'Progression',
    title: 'Know what to lift next',
    body: 'Every exercise opens with a target for today and the reason behind it: add a rep, move up in weight, hold, or change something after a stall. Your estimated one rep max is charted per exercise.',
    points: [
      "Today's target with the reason",
      "Last session's sets shown next to it",
      'Estimated one rep max chart per exercise',
      'Stalled lifts point you to the coach',
    ],
    mockup: <ProgressChartMockup />,
    reverse: true,
    band: true,
  },
  {
    id: 'coach',
    tone: 'cyan',
    icon: Bot,
    eyebrow: 'AI coach',
    title: 'A coach that proposes, you decide',
    body: 'Ask for a routine or a change to your sets and reps. The coach explains its suggestion, and nothing in your plan changes until you apply it.',
    points: [
      'Ask the coach to build a routine',
      'Suggestions show the current and proposed values',
      'Apply a change or dismiss it',
      'Ask for a plan when a lift stalls',
    ],
    mockup: <CoachChatMockup />,
  },
]

function Logo({ dim = false }: { dim?: boolean }) {
  return (
    <span className={`landing-logo${dim ? ' landing-logo--dim' : ''}`}>
      <span className="landing-logo__tile">
        <Dumbbell size={18} strokeWidth={2.25} />
      </span>
      GymRat
    </span>
  )
}

function FeatureSection({ feature }: { feature: Feature }) {
  const Icon = feature.icon
  const headingId = `feature-${feature.id}`
  const classes = [
    'feature',
    `feature--${feature.tone}`,
    feature.reverse ? 'feature--reverse' : '',
    feature.band ? 'feature--band' : '',
  ]
    .filter(Boolean)
    .join(' ')
  return (
    <section className={classes} aria-labelledby={headingId}>
      <div className="landing__container feature__inner">
        <motion.div className="feature__copy" {...reveal}>
          <span className="feature__eyebrow">
            <span className="feature__icon">
              <Icon size={18} strokeWidth={2.25} />
            </span>
            {feature.eyebrow}
          </span>
          <h2 id={headingId}>{feature.title}</h2>
          <p className="feature__body">{feature.body}</p>
          <ul className="feature__points">
            {feature.points.map((point) => (
              <li key={point}>
                <span className="feature__check" aria-hidden="true">
                  <Check size={14} strokeWidth={3} />
                </span>
                {point}
              </li>
            ))}
          </ul>
        </motion.div>
        <motion.div className="feature__mockup" {...reveal}>
          {feature.mockup}
        </motion.div>
      </div>
    </section>
  )
}

export function LandingPage() {
  return (
    <div className="landing">
      <header className="landing-nav">
        <nav className="landing__container landing-nav__inner" aria-label="Main">
          <Link to="/" className="landing-nav__home" aria-label="GymRat home">
            <Logo />
          </Link>
          <div className="landing-nav__actions">
            <Link to="/login" className="landing-nav__link">
              Log in
            </Link>
            <ButtonLink to="/register" size="sm" className="landing-nav__signup">
              Sign up
            </ButtonLink>
          </div>
        </nav>
      </header>

      <main>
        <section className="hero" aria-labelledby="hero-title">
          <div className="landing__container hero__inner">
            <motion.div className="hero__copy" {...reveal}>
              <span className="hero__eyebrow">
                <span className="hero__dot" aria-hidden="true" />
                Free workout tracker
              </span>
              <h1 id="hero-title" className="hero__title">
                <span>Log workouts.</span>{' '}
                <span className="hero__title-accent">Get stronger.</span>{' '}
                <span>Stay motivated.</span>
              </h1>
              <p className="hero__lead">
                GymRat is a free workout tracker. Build routines, follow a weekly split, and log every set with weight,
                reps and effort.
              </p>
              <div className="hero__actions">
                <ButtonLink to="/register" size="lg">
                  Start now
                  <ArrowRight size={18} strokeWidth={2.25} aria-hidden="true" />
                </ButtonLink>
                <ButtonLink to="/login" variant="secondary" size="lg">
                  Log in
                </ButtonLink>
              </div>
              <p className="footnote">Free to use. Works in any browser.</p>
            </motion.div>
            <motion.div className="hero__visual" {...reveal}>
              <PhoneMockup />
            </motion.div>
          </div>
          <div className="landing__container">
            <hr className="landing__divider" />
          </div>
        </section>

        {features.map((feature) => (
          <FeatureSection key={feature.id} feature={feature} />
        ))}

        <section className="closing" aria-labelledby="closing-title">
          <div className="landing__container">
            <motion.div className="closing__panel" {...reveal}>
              <h2 id="closing-title">Start logging today.</h2>
              <p>Free, simple, and built for the gym floor.</p>
              <div className="closing__actions">
                <ButtonLink to="/register" size="lg">
                  Create free account
                  <ArrowRight size={18} strokeWidth={2.25} aria-hidden="true" />
                </ButtonLink>
                <ButtonLink to="/login" variant="secondary" size="lg">
                  Log in
                </ButtonLink>
              </div>
            </motion.div>
          </div>
        </section>
      </main>

      <footer className="landing-footer">
        <Logo dim />
        <p>© {currentYear} GymRat. Engineered for athletes.</p>
      </footer>
    </div>
  )
}
