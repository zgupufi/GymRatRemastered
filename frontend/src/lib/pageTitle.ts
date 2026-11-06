const TITLES: Record<string, string> = {
  '/': 'Home',
  '/login': 'Log in',
  '/register': 'Create account',
  '/workouts': 'Workouts',
  '/split': 'Training split',
  '/progress': 'Progression',
  '/exercises': 'Exercise library',
  '/workout-execution': 'Workout',
}

export function pageTitle(pathname: string): string {
  const title = TITLES[pathname] ?? (pathname.startsWith('/exercises/') ? 'Exercise' : null)
  return title ? `${title} · GymRat` : 'GymRat'
}
