import { Link } from 'react-router-dom'

export default function StreakCard({ streak, weekDaysHit, weekDays }) {
  return (
    <section className="card streak-card">
      <h2>مطالعهٔ پیاپی</h2>
      <p className="countdown-figure" dir="ltr">{streak}</p>
      <p className="card-copy">
        {streak > 0 ? 'چند روز پشت‌سرهم مطالعه کردی.' : 'امروز یا دیروز جلسه‌ای نبود.'}
      </p>
      <p className="streak-week" dir="ltr">
        این هفته {weekDaysHit} از {weekDays} روز
      </p>
      <Link className="text-link" to="/analytics">دیدن آمار</Link>
    </section>
  )
}
