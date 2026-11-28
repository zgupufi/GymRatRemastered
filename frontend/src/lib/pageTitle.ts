const TITLES: Record<string, string> = {
  '/': 'Home',
  '/login': 'Log in',
  '/register': 'Create account',
  '/workouts': 'Workouts',
  '/split': 'Split',
  '/progress': 'Progress',
  '/exercises': 'Library',
  '/workout-execution': 'Workout',
  '/settings': 'Settings',
}

export function pageTitle(pathname: string): string {
  const title = TITLES[pathname] ?? (pathname.startsWith('/exercises/') ? 'Exercise' : null)
  return title ? `${title} · GymRat` : 'GymRat'
}
