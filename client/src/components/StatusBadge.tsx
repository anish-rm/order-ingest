import styles from './StatusBadge.module.css'

const toneByStatus: Record<string, string> = {
  Created: styles.blue,
  Accepted: styles.green,
  Completed: styles.forest,
  Denied: styles.danger,
  Cancelled: styles.danger,
}

export default function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`${styles.badge} ${toneByStatus[status] ?? styles.neutral}`}>
      {status}
    </span>
  )
}

export function ProviderBadge({ provider }: { provider: string }) {
  return (
    <span className={styles.provider}>
      <span
        className={styles.dot}
        style={{ background: provider === 'Uber' ? 'var(--blue)' : 'var(--green)' }}
        aria-hidden
      />
      {provider}
    </span>
  )
}
