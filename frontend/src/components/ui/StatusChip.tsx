// @ts-nocheck
import './StatusChip.css'

const LABELS = {
  Active: 'فعال',
  InProgress: 'در حال انجام',
  Completed: 'آماده دریافت',
  Claimable: 'آماده دریافت',
  Claimed: 'دریافت‌شده',
  Expired: 'منقضی',
  Locked: 'قفل',
  Available: 'آزاد',
  Complete: 'کامل',
  Ready: 'آماده',
  Collecting: 'در حال جمع‌آوری',
}

export function statusLabel(status) {
  if (!status) return '—'
  return LABELS[status] || LABELS[String(status)] || String(status)
}

export default function StatusChip({ status, tone }) {
  const label = statusLabel(status)
  const kind = tone || String(status || 'default').toLowerCase()
  return (
    <span className={`status-chip status-chip--${kind}`} title={label}>
      {label}
    </span>
  )
}
