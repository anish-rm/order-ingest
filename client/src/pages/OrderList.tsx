import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchOrders } from '../api/orders'
import { formatMoney, formatTime } from '../lib/format'
import styles from './OrderList.module.css'

export default function OrderList() {
  const { data: orders, isPending, isError, error, refetch } = useQuery({
    queryKey: ['orders'],
    queryFn: fetchOrders,
  })

  return (
    <main className={styles.page}>
      <h1>Orders</h1>

      {isPending && <p className={styles.muted}>Loading orders…</p>}

      {isError && (
        <div className={styles.error} role="alert">
          <p>Could not load orders: {error.message}</p>
          <button onClick={() => refetch()}>Retry</button>
        </div>
      )}

      {orders && orders.length === 0 && (
        <p className={styles.muted}>
          No orders yet. Send a webhook (see the README curls) and refresh.
        </p>
      )}

      {orders && orders.length > 0 && (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Provider</th>
              <th>Status</th>
              <th className={styles.right}>Total</th>
              <th>Received</th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order) => (
              <tr key={order.id}>
                <td>
                  <Link to={`/order/${order.id}`}>{order.provider}</Link>
                </td>
                <td>
                  <span className={styles.status}>{order.status}</span>
                </td>
                <td className={styles.right}>
                  {formatMoney(order.totalCents, order.currency)}
                </td>
                <td>{formatTime(order.receivedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  )
}
