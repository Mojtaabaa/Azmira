// @ts-nocheck
import { Link } from 'react-router-dom'
import { listSubjects } from '../../data/subjects'
import { dayKeyFromValue } from '../../utils/analytics'
import { formatMinutes, todayInputValue } from '../../utils/clock'
import { saturdayOf, shiftDay } from '../../utils/week'

export default function SubjectBalance({ sessions, goals }) {
  const today = todayInputValue()
  const start = saturdayOf(today)
  const end = shiftDay(start, 6)
  const subjects = listSubjects()
  const rows = subjects.map((subject) => {
    const minutes = sessions.reduce((sum, session) => {
      const day = dayKeyFromValue(session.startTime)
      if (!day || day < start || day > end || session.subject !== subject.id) return sum
      return sum + (Number(session.durationMinutes) || 0)
    }, 0)
    const goal = Number(goals?.[subject.id]) || 0
    return { ...subject, minutes, goal }
  }).filter((row) => row.minutes > 0 || row.goal > 0)

  const peak = Math.max(1, ...rows.map((row) => Math.max(row.minutes, row.goal)))

  return (
    <section className="card">
      <h2>تعادل درس‌ها در این هفته</h2>
      <p className="card-copy">اگر یک درس جلو بیفتد و بقیه خالی بمانند، اینجا دیده می‌شود.</p>
      {rows.length === 0 ? (
        <p className="card-copy">این هفته هنوز مطالعه‌ای ثبت نشده. با زمان‌سنج شروع کن.</p>
      ) : (
        <ul className="balance-list">
          {rows.map((row) => (
            <li key={row.id}>
              <div className="balance-label">
                <span>{row.name}</span>
                <strong>{formatMinutes(row.minutes)}{row.goal ? ` از ${formatMinutes(row.goal)}` : ''}</strong>
              </div>
              <div className="meter" aria-hidden="true">
                <span style={{ width: `${Math.min(100, (row.minutes / peak) * 100)}%`, background: row.color }} />
              </div>
            </li>
          ))}
        </ul>
      )}
      <Link className="text-link" to="/settings#weekly-goal">هدف هر درس</Link>
    </section>
  )
}
