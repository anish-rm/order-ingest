import { Component, type ReactNode } from 'react'
import styles from './ErrorBoundary.module.css'

interface Props {
  children: ReactNode
}

interface State {
  error: Error | null
}

/**
 * Catches render-time errors so the admin degrades to a recoverable
 * message instead of a blank page. Query/network errors are handled per
 * page; this is the net under everything else.
 */
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  render() {
    if (this.state.error === null) {
      return this.props.children
    }

    return (
      <main className={styles.page} role="alert">
        <h1>Something went wrong</h1>
        <p className={styles.message}>{this.state.error.message}</p>
        <button className={styles.reload} onClick={() => window.location.assign('/')}>
          Reload admin
        </button>
      </main>
    )
  }
}
