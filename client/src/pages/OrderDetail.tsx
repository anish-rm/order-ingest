import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchOrder } from '../api/orders'
import { formatMoney, formatTime } from '../lib/format'
import styles from './OrderDetail.module.css'

export default function OrderDetail() {
  const { id } = useParams<{ id: string }>()
  const { data: order, isPending, isError, error, refetch } = useQuery({
    queryKey: ['orders', id],
    queryFn: () => fetchOrder(id!),
    enabled: id !== undefined,
  })

  return (
    <main className={styles.page}>
      <Link to="/">← All orders</Link>

      {isPending && <p className={styles.muted}>Loading order…</p>}

      {isError && (
        <div className={styles.error} role="alert">
          <p>Could not load this order: {error.message}</p>
          <button onClick={() => refetch()}>Retry</button>
        </div>
      )}

      {order && (
        <>
          <header className={styles.header}>
            <h1>
              {order.provider} order{' '}
              <span className={styles.muted}>#{order.externalOrderId}</span>
            </h1>
            <span className={styles.status}>{order.status}</span>
          </header>

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
                    <td className={styles.right}>
                      {formatMoney(item.priceCents, order.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={2}>Total</td>
                  <td className={styles.right}>
                    {formatMoney(order.totalCents, order.currency)}
                  </td>
                </tr>
              </tfoot>
            </table>
          </section>

          <p className={styles.muted}>
            Provider status "{order.rawStatus}" · received{' '}
            {formatTime(order.receivedAt)} · updated {formatTime(order.lastUpdatedAt)}
          </p>
        </>
      )}
    </main>
  )
}
