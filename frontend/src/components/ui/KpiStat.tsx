// @ts-nocheck
import './KpiStat.css'

export default function KpiStat({ label, value, hint, dir = 'ltr' }) {
  return (
    <div className="kpi-stat">
      <span className="kpi-stat__label">{label}</span>
      <strong className="kpi-stat__value" dir={dir}>
        {value ?? '—'}
      </strong>
      {hint ? <span className="kpi-stat__hint">{hint}</span> : null}
    </div>
  )
}
