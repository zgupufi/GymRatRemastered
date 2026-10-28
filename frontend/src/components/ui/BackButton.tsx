import { ChevronLeft } from 'lucide-react'
import { useLocation, useNavigate } from 'react-router-dom'
import './BackButton.css'

interface BackButtonProps {
  fallback?: string
}

export function BackButton({ fallback = '/' }: BackButtonProps) {
  const navigate = useNavigate()
  const location = useLocation()

  const goBack = () => {
    if (location.key === 'default') {
      navigate(fallback)
    } else {
      navigate(-1)
    }
  }

  return (
    <button type="button" className="back-button" onClick={goBack}>
      <ChevronLeft size={18} aria-hidden="true" />
      Back
    </button>
  )
}
