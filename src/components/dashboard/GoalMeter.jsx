import { Link } from 'react-router-dom'
import { formatMinutes } from '../../utils/clock'

export default function GoalMeter({ title, doneMinutes, planMinutes, planLabel, note, to, action }) {
  const ratio = planMinutes > 0 ? doneMinutes / planMinutes : 0
  const width = Math.max(0, Math.min(100, Math.round(ratio * 100)))

  return (
    <section className="card goal-meter">
      <h2>{title}</h2>
      <p className="goal-figures">
        <strong>{formatMinutes(doneMinutes)}</strong>
        <span>{planLabel}</span>
      </p>
      <div
        className="meter"
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={width}
        aria-label={title}
      >
        <span style={{ width: `${width}%` }} />
      </div>
      {note ? <p className="card-copy">{note}</p> : null}
      {to ? <Link className="text-link" to={to}>{action}</Link> : null}
    </section>
  )
}
