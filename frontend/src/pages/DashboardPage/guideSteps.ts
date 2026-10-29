export interface GuideStep {
  title: string
  description: string
  color: string
}

export const guideSteps: GuideStep[] = [
  {
    title: 'Purpose & Scope',
    description: "GymRat isn't just a tracker; it's a virtual coach built around consistent progression.",
    color: 'var(--text-2)',
  },
  {
    title: 'Database Mastery',
    description: 'Browse the Exercise DB to study detailed form instructions and filter movements by targeted muscle groups.',
    color: 'var(--orange)',
  },
  {
    title: 'Build Workouts',
    description: 'Group exercises into routines. Set how many sets you want for each exercise.',
    color: 'var(--red)',
  },
  {
    title: 'AI Assistant',
    description: 'Use the GymRat AI chatbot to generate workouts, modify sets, or add exercises using natural language.',
    color: 'var(--cyan)',
  },
  {
    title: 'Set Your Split',
    description: 'Assign your newly created workouts to specific days of the week for proper structure and muscle recovery.',
    color: '#7b8cff',
  },
  {
    title: 'Execute & Log',
    description: "Hit 'Start Workout', log your sets, and use the built-in rest timers between sets to maintain intensity.",
    color: 'var(--emerald)',
  },
  {
    title: 'Analyze Progression',
    description: 'Visit the Progression tab to monitor your Estimated 1RM, volume history, and visualize your lifetime gains.',
    color: 'var(--purple)',
  },
]
