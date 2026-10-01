import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { fetchOrders } from '../api/orders'
import { formatMoney, formatTime } from '../lib/format'
import Layout from '../components/Layout'
import StatusBadge, { ProviderBadge } from '../components/StatusBadge'
import styles from './OrderList.module.css'

export default function OrderList() {
  const navigate = useNavigate()
  const { data: orders, isPending, isError, error, refetch } = useQuery({
    queryKey: ['orders'],
    queryFn: fetchOrders,
  })

  return (
    <Layout>
      <div className={styles.heading}>
        <h1>Orders</h1>
        {orders && orders.length > 0 && (
          <p className={styles.summary}>
            {orders.length} {orders.length === 1 ? 'order' : 'orders'} from{' '}
            {new Set(orders.map((o) => o.provider)).size}{' '}
            {new Set(orders.map((o) => o.provider)).size === 1 ? 'provider' : 'providers'}
          </p>
        )}
      </div>

      {isPending && (
        <div className={styles.card} aria-label="Loading orders">
          {[0, 1, 2].map((i) => (
            <div key={i} className={styles.skeletonRow}>
              <span className={styles.skeleton} style={{ width: '18%' }} />
              <span className={styles.skeleton} style={{ width: '14%' }} />
              <span className={styles.skeleton} style={{ width: '12%' }} />
              <span className={styles.skeleton} style={{ width: '26%' }} />
            </div>
          ))}
        </div>
      )}

      {isError && (
        <div className={styles.error} role="alert">
          <p>
            <strong>Could not load orders.</strong> {error.message}
          </p>
          <p className={styles.errorHint}>Is the API running on port 5080?</p>
          <button className={styles.retry} onClick={() => refetch()}>
            Retry
          </button>
        </div>
      )}

      {orders && orders.length === 0 && (
        <div className={styles.empty}>
          <span className={styles.emptyMark} aria-hidden>
            ◎
          </span>
          <h2>No orders yet</h2>
          <p>Send a provider webhook (see the README curls), then refresh.</p>
        </div>
      )}

      {orders && orders.length > 0 && (
        <div className={styles.card}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Provider</th>
                <th>Status</th>
                <th className={styles.right}>Total</th>
                <th>Received</th>
                <th aria-hidden />
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr
                  key={order.id}
                  className={styles.row}
                  onClick={() => navigate(`/order/${order.id}`)}
                >
                  <td>
                    <ProviderBadge provider={order.provider} />
                  </td>
                  <td>
                    <StatusBadge status={order.status} />
                  </td>
                  <td className={`${styles.right} ${styles.money}`}>
                    {formatMoney(order.totalCents, order.currency)}
                  </td>
                  <td className={styles.time}>{formatTime(order.receivedAt)}</td>
                  <td className={styles.chevron} aria-hidden>
                    →
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Layout>
  )
}
