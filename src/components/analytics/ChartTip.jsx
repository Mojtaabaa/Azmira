function formatTipValue(dataKey, value, name = '') {
  const number = Number(value)
  if (!Number.isFinite(number)) return String(value ?? '')
  if (dataKey === 'hours') return `${number.toFixed(1)} س`
  if (dataKey === 'sessions') return `${Math.round(number)} جلسه`
  if (dataKey === 'tests') return `${Math.round(number)} آزمون`
  if (dataKey === 'done' || name === 'کار') return `${Math.round(number)} کار`
  if (dataKey === 'count') return `${Math.round(number)} تست`
  return `${number.toFixed(1)}%`
}

export default function ChartTip({ active, payload, label }) {
  if (!active || !payload?.length) return null
  const seen = new Set()
  const rows = payload.filter((item) => {
    if (item.value == null || item.value === '') return false
    const key = String(item.dataKey)
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
  if (rows.length === 0) return null
  const row = rows[0]?.payload || {}

  return (
    <div className="chart-tip">
      <strong>{row.label || label}</strong>
      {rows.map((item) => (
        <p key={String(item.dataKey)}>
          {item.name}: {formatTipValue(item.dataKey, item.value, item.name)}
        </p>
      ))}
      {row.sessions != null && rows.every((item) => item.dataKey !== 'sessions') ? (
        <p>جلسه: {formatTipValue('sessions', row.sessions)}</p>
      ) : null}
      {row.tests != null && rows.every((item) => item.dataKey !== 'tests') ? (
        <p>آزمون: {formatTipValue('tests', row.tests)}</p>
      ) : null}
      {row.detail ? <p>{row.detail}</p> : null}
    </div>
  )
}
