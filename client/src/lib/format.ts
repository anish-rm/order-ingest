export function formatMoney(cents: number, currency: string): string {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(
    cents / 100,
  )
}

export function formatTime(iso: string): string {
  return new Date(iso).toLocaleString()
}
