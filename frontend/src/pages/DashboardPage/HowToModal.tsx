import { Modal } from '../../components/ui/Modal'
import { guideSteps } from './guideSteps'
import './DashboardPage.css'

interface HowToModalProps {
  open: boolean
  onClose: () => void
}

export function HowToModal({ open, onClose }: HowToModalProps) {
  return (
    <Modal open={open} title="How to use GymRat" onClose={onClose}>
      <ol className="howto__steps">
        {guideSteps.map((step, index) => (
          <li key={step.title} className="howto__step">
            <span className="howto__number" aria-hidden="true">
              {index + 1}
            </span>
            <div>
              <h4>{step.title}</h4>
              <p>{step.description}</p>
            </div>
          </li>
        ))}
      </ol>
    </Modal>
  )
}
