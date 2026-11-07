// @ts-nocheck
import { Link } from 'react-router-dom'

export default function RankCard({ projection }) {
  const score = projection.score == null ? '—' : `${projection.score.toFixed(1)}%`

  return (
    <section className="card rank-card">
      <div className="rank-head">
        <div>
          <h2>برآورد درصد</h2>
          <p className="card-copy">میانگین آزمون‌های هر درس، با وزنی که در تنظیمات گذاشته‌ای.</p>
        </div>
        <p className="rank-score" dir="ltr">{score}</p>
      </div>
      {projection.rows.length === 0 ? (
        <p className="card-copy">هنوز آزمونی نیست که بشود از آن درصد ساخت.</p>
      ) : (
        <ul className="rank-list">
          {projection.rows.map((row) => (
            <li key={row.id}>
              <span>{row.name}</span>
              <span dir="ltr">
                {row.average == null ? '—' : `${row.average.toFixed(1)}%`}
                {' · '}
                {row.weight}
              </span>
            </li>
          ))}
        </ul>
      )}
      <Link className="text-link" to="/settings#subject-weights">وزن درس‌ها</Link>
    </section>
  )
}
