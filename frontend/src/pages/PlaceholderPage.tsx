import { BackButton } from '../components/ui/BackButton'

interface PlaceholderPageProps {
  title: string
  description: string
  showBack?: boolean
}

export function PlaceholderPage({ title, description, showBack = true }: PlaceholderPageProps) {
  return (
    <main className="page">
      {showBack ? <BackButton /> : null}
      <header className="page__header">
        <h1>{title}</h1>
        <p>{description}</p>
      </header>
    </main>
  )
}
