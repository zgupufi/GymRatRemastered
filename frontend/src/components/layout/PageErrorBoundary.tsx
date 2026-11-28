import { Component, type ReactNode } from 'react'
import { Button } from '../ui/Button'

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
            <h1>This page did not load</h1>
            <p>Check your connection, then reload to try again.</p>
          </header>
          <Button variant="secondary" onClick={() => window.location.reload()}>
            Reload
          </Button>
        </main>
      )
    }
    return this.props.children
  }
}
