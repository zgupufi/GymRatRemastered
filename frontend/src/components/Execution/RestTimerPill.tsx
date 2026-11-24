import { AnimatePresence, motion } from 'motion/react'
import { spring } from '../../lib/motion'
import { formatClock } from '../../lib/clock'

interface RestTimerPillProps {
  remaining: number | null
  finished: boolean
  onExtend: () => void
  onReduce: () => void
  onSkip: () => void
}

export function RestTimerPill({ remaining, finished, onExtend, onReduce, onSkip }: RestTimerPillProps) {
  const visible = remaining !== null || finished
  return (
    <AnimatePresence>
      {visible ? (
        <motion.div
          className={finished ? 'rest-pill rest-pill--over' : 'rest-pill'}
          role="timer"
          aria-label="Rest timer"
          initial={{ opacity: 0, y: 24, scale: 0.96 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={{ opacity: 0, y: 24, scale: 0.96 }}
          transition={spring}
        >
          <span className="rest-pill__dot" aria-hidden="true" />
          {remaining !== null ? (
            <>
              <strong className="rest-pill__time">{formatClock(remaining)}</strong>
              <button type="button" onClick={onExtend}>
                +30s
              </button>
              <button type="button" onClick={onReduce}>
                −10s
              </button>
              <button type="button" className="rest-pill__skip" onClick={onSkip}>
                Skip
              </button>
            </>
          ) : (
            <>
              <strong className="rest-pill__time">Rest over</strong>
              <button type="button" className="rest-pill__skip" onClick={onSkip}>
                Dismiss
              </button>
            </>
          )}
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
