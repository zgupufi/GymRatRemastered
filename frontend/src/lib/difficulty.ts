export type DifficultyTone = 'beginner' | 'intermediate' | 'advanced' | 'neutral'

export function difficultyTone(difficulty: string | null): DifficultyTone {
  switch (difficulty?.trim().toLowerCase()) {
    case 'beginner':
      return 'beginner'
    case 'intermediate':
      return 'intermediate'
    case 'advanced':
      return 'advanced'
    default:
      return 'neutral'
  }
}
