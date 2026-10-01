// Types mirror the API's internal DTOs — no provider field names here.

export interface OrderSummary {
  id: number
  provider: string
  status: string
  totalCents: number
  currency: string
  receivedAt: string
}

export interface Customer {
  name: string
  phone: string | null
  email: string | null
}

export interface LineItem {
  name: string
  quantity: number
  priceCents: number
}

export interface OrderDetail extends OrderSummary {
  externalOrderId: string
  rawStatus: string
  customer: Customer
  lineItems: LineItem[]
  lastUpdatedAt: string
}

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${response.statusText}`)
  }
  return response.json()
}

export const fetchOrders = () => getJson<OrderSummary[]>('/api/orders')

export const fetchOrder = (id: string) => getJson<OrderDetail>(`/api/orders/${id}`)
