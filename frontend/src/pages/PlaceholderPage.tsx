import { BackButton } from '../components/ui/BackButton'
import { ButtonLink } from '../components/ui/ButtonLink'

interface PlaceholderPageProps {
  title: string
  description: string
  showBack?: boolean
  homeLink?: boolean
}

export function PlaceholderPage({ title, description, showBack = true, homeLink = false }: PlaceholderPageProps) {
  return (
    <main className="page">
      {showBack ? <BackButton fallback="/" /> : null}
      <header className="page__header">
        <h1>{title}</h1>
        <p>{description}</p>
      </header>
      {homeLink ? (
        <ButtonLink to="/" variant="secondary">
          Go to home
        </ButtonLink>
      ) : null}
    </main>
  )
}
