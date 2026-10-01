import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import styles from './Layout.module.css'

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <>
      <header className={styles.header}>
        <Link to="/" className={styles.brand}>
          <span className={styles.mark} aria-hidden />
          Order Ingest
        </Link>
        <span className={styles.tagline}>provider orders</span>
        <span className={styles.context}>
          <span className={styles.contextDot} aria-hidden />
          Admin
        </span>
      </header>
      <main className={styles.content}>{children}</main>
    </>
  )
}
