export default function SubjectGoalsChart({ rows }) {
  if (!rows.length) {
    return <p className="card-copy">هنوز هدف هفتگی برای درسی نگذاشته‌ای.</p>
  }

  return (
    <ul className="goal-bars">
      {rows.map((row) => (
        <li key={row.id} className={row.over ? 'is-over' : row.low ? 'is-low' : ''}>
          <div className="goal-bars-meta">
            <strong>{row.label}</strong>
            <span>
              {row.done} از {row.goal} س
            </span>
          </div>
          <div
            className="meter"
            role="progressbar"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={Math.min(100, row.percent)}
            aria-label={`هدف ${row.label}`}
          >
            <span style={{ width: `${Math.min(100, row.percent)}%` }} />
          </div>
        </li>
      ))}
    </ul>
  )
}