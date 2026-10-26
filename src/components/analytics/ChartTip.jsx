export default function ChartTip({ active, payload, label }) {
  if (!active || !payload?.length) return null
  const rows = payload.filter((item) => item.value != null && item.value !== '')
  if (rows.length === 0) return null

  return (
    <div className="chart-tip">
      <strong>{rows[0]?.payload?.label || label}</strong>
      {rows.map((item) => {
        const number = Number(item.value)
        const count = item.dataKey === 'count'
        const shown = count ? String(number) : number.toFixed(1)
        const unit = count || item.dataKey === 'hours' ? '' : '%'
        return (
          <p key={String(item.dataKey)}>
            {item.name} {shown}{unit}
            {item.payload?.detail && item.dataKey === 'hours' ? ` · ${item.payload.detail}` : ''}
          </p>
        )
      })}
    </div>
  )
}
