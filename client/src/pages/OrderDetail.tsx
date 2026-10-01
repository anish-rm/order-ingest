import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchOrder } from '../api/orders'
import { formatMoney, formatTime } from '../lib/format'
import Layout from '../components/Layout'
import StatusBadge, { ProviderBadge } from '../components/StatusBadge'
import styles from './OrderDetail.module.css'

export default function OrderDetail() {
  const { id } = useParams<{ id: string }>()
  const { data: order, isPending, isError, error, refetch } = useQuery({
    queryKey: ['orders', id],
    queryFn: () => fetchOrder(id!),
    enabled: id !== undefined,
  })

  return (
    <Layout>
      <Link to="/" className={styles.back}>
        ← All orders
      </Link>

      {isPending && <p className={styles.muted}>Loading order…</p>}

      {isError && (
        <div className={styles.error} role="alert">
          <p>
            <strong>Could not load this order.</strong> {error.message}
          </p>
          <button className={styles.retry} onClick={() => refetch()}>
            Retry
          </button>
        </div>
      )}

      {order && (
        <>
          <header className={styles.heading}>
            <div>
              <h1>
                {formatMoney(order.totalCents, order.currency)}
                <span className={styles.headingSep} aria-hidden>
                  ·
                </span>
                <ProviderBadge provider={order.provider} />
              </h1>
              <p className={styles.orderId}>#{order.externalOrderId}</p>
            </div>
            <StatusBadge status={order.status} />
          </header>

          <div className={styles.grid}>
            <section className={styles.card}>
              <h2>Customer</h2>
              <dl className={styles.facts}>
                <dt>Name</dt>
                <dd>{order.customer.name}</dd>
                {order.customer.phone && (
                  <>
                    <dt>Phone</dt>
                    <dd>{order.customer.phone}</dd>
                  </>
                )}
                {order.customer.email && (
                  <>
                    <dt>Email</dt>
                    <dd>{order.customer.email}</dd>
                  </>
                )}
              </dl>
            </section>

            <section className={styles.card}>
              <h2>Timeline</h2>
              <dl className={styles.facts}>
                <dt>Received</dt>
                <dd>{formatTime(order.receivedAt)}</dd>
                <dt>Updated</dt>
                <dd>{formatTime(order.lastUpdatedAt)}</dd>
                <dt>Provider status</dt>
                <dd>
                  <code className={styles.raw}>{order.rawStatus}</code>
                </dd>
              </dl>
            </section>
          </div>

          <section className={styles.card}>
            <h2>Items</h2>
            <table className={styles.items}>
              <thead>
                <tr>
                  <th>Item</th>
                  <th className={styles.right}>Qty</th>
                  <th className={styles.right}>Price</th>
                </tr>
              </thead>
              <tbody>
                {order.lineItems.map((item, index) => (
                  <tr key={index}>
                    <td>{item.name}</td>
                    <td className={styles.right}>{item.quantity}</td>
                    <td className={`${styles.right} ${styles.money}`}>
                      {formatMoney(item.priceCents, order.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={2}>Total (incl. tax &amp; fees)</td>
                  <td className={`${styles.right} ${styles.money}`}>
                    {formatMoney(order.totalCents, order.currency)}
                  </td>
                </tr>
              </tfoot>
            </table>
          </section>
        </>
      )}
    </Layout>
  )
}
