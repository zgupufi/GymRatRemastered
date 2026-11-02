import { Component, type ReactNode } from 'react'
import './ChatWidget.css'

interface ChatErrorBoundaryProps {
  children: ReactNode
}

interface ChatErrorBoundaryState {
  failed: boolean
}

export class ChatErrorBoundary extends Component<ChatErrorBoundaryProps, ChatErrorBoundaryState> {
  state: ChatErrorBoundaryState = { failed: false }

  static getDerivedStateFromError(): ChatErrorBoundaryState {
    return { failed: true }
  }

  render() {
    if (this.state.failed) {
      return (
        <div className="chat__crash" role="alert">
          The coach hit a problem. Reload the page to try again.
        </div>
      )
    }
    return this.props.children
  }
}
