import { difficultyTone } from '../../lib/difficulty'
import './DifficultyChip.css'

export function DifficultyChip({ difficulty }: { difficulty: string | null }) {
  if (!difficulty) {
    return null
  }
  return <span className={`difficulty-chip difficulty-chip--${difficultyTone(difficulty)}`}>{difficulty}</span>
}
