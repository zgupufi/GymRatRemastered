export interface GuideStep {
  title: string
  description: string
}

export const guideSteps: GuideStep[] = [
  {
    title: 'Build a workout',
    description: 'Group exercises into a routine and set how many sets you want for each one.',
  },
  {
    title: 'Assign it to days',
    description: 'Pick a workout for each day in your split so today’s workout is ready when you open the app.',
  },
  {
    title: 'Log your sets',
    description: 'Start the workout, enter weight and reps, and tick each set. A rest timer starts for you.',
  },
  {
    title: 'Watch your progress',
    description: 'Progression charts your estimated one-rep max over time. The AI coach can also build and change workouts for you.',
  },
]
