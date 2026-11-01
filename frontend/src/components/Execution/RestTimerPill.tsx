import { AnimatePresence, motion } from 'motion/react'
import { spring } from '../../lib/motion'
import { formatClock } from '../../lib/clock'

interface RestTimerPillProps {
  remaining: number | null
  onExtend: () => void
  onReduce: () => void
  onSkip: () => void
}

export function RestTimerPill({ remaining, onExtend, onReduce, onSkip }: RestTimerPillProps) {
  return (
    <AnimatePresence>
      {remaining !== null ? (
        <motion.div
          className="rest-pill"
          role="timer"
          aria-label="Rest timer"
          initial={{ opacity: 0, y: 24, scale: 0.96 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={{ opacity: 0, y: 24, scale: 0.96 }}
          transition={spring}
        >
          <span className="rest-pill__dot" aria-hidden="true" />
          <strong className="rest-pill__time">{formatClock(remaining)}</strong>
          <button type="button" onClick={onExtend}>
            +30s
          </button>
          <button type="button" onClick={onReduce}>
            -10s
          </button>
          <button type="button" className="rest-pill__skip" onClick={onSkip}>
            Skip
          </button>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
