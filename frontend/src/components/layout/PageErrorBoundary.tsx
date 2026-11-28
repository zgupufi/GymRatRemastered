import { Component, type ReactNode } from 'react'
import { Button } from '../ui/Button'
import { ButtonLink } from '../ui/ButtonLink'

interface PageErrorBoundaryProps {
  resetKey: string
  children: ReactNode
}

interface PageErrorBoundaryState {
  failed: boolean
  resetKey: string
}

export class PageErrorBoundary extends Component<PageErrorBoundaryProps, PageErrorBoundaryState> {
  state: PageErrorBoundaryState = { failed: false, resetKey: this.props.resetKey }

  static getDerivedStateFromError(): Partial<PageErrorBoundaryState> {
    return { failed: true }
  }

  static getDerivedStateFromProps(
    { resetKey }: PageErrorBoundaryProps,
    state: PageErrorBoundaryState,
  ): PageErrorBoundaryState | null {
    return resetKey === state.resetKey ? null : { failed: false, resetKey }
  }

  render() {
    if (this.state.failed) {
      return (
        <main className="page">
          <header className="page__header" role="alert">
            <h1>Something went wrong</h1>
            <p>This page could not be shown. Reload to try again.</p>
          </header>
          <div className="page__actions">
            <Button variant="secondary" onClick={() => window.location.reload()}>
              Reload
            </Button>
            {this.state.resetKey === '/' ? null : (
              <ButtonLink to="/" variant="ghost">
                Go to home
              </ButtonLink>
            )}
          </div>
        </main>
      )
    }
    return this.props.children
  }
}
