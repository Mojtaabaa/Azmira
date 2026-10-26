import { Link } from 'react-router-dom'
import { formatGregorian, formatJalali } from '../../utils/week'

export default function CountdownCard({ countdown }) {
  const { days, started } = countdown
  let headline = String(Math.max(days, 0))
  let caption = 'روز مانده تا کنکور'
  if (days < 0) {
    headline = String(Math.abs(days))
    caption = 'روز از کنکور گذشته'
  } else if (days === 0 && started) {
    headline = 'امروز'
    caption = 'کنکور امروز شروع شده'
  } else if (days === 0) {
    headline = 'امروز'
    caption = 'کنکور امروز است'
  }

  return (
    <section className="card countdown-card">
      <h2>تا کنکور</h2>
      <p className="countdown-figure" dir="ltr">{headline}</p>
      <p>{caption}</p>
      <p className="card-copy">
        {formatGregorian(countdown.day, true)}
        {' · '}
        {formatJalali(countdown.day, true)}
        {' · '}
        <span dir="ltr">08:00</span>
      </p>
      <Link className="text-link" to="/settings#konkoor-date">تغییر تاریخ</Link>
    </section>
  )
}
