import { getSubjectById } from '../../data/subjects'
import { useCollection } from '../../hooks/useCollection'
import { formatMinutes, formatTimeOfDay, isSameLocalDay } from '../../utils/clock'

export default function TodaySessions() {
  const sessions = useCollection('sessions')
  const today = new Date()
  const todays = sessions
    .filter((session) => isSameLocalDay(session.startTime, today))
    .slice()
    .sort((a, b) => String(b.startTime).localeCompare(String(a.startTime)))
  const total = todays.reduce((sum, session) => sum + (Number(session.durationMinutes) || 0), 0)

  return (
    <section className="timer-card">
      <div className="timer-card-head">
        <h2>امروز</h2>
        <p>{formatMinutes(total)} مطالعه</p>
      </div>
      {todays.length === 0 ? (
        <p className="timer-muted">امروز مطالعه‌ای ثبت نشده.</p>
      ) : (
        <ul className="session-list">
          {todays.map((session) => {
            const subject = getSubjectById(session.subject)
            return (
              <li key={session.id}>
                <div>
                  <strong>{subject ? subject.name : session.subject}</strong>
                  <p className="timer-muted">
                    {session.topic || 'بدون مبحث'}
                    {' · '}
                    {session.type === 'manual' ? 'دستی' : 'پومودورو'}
                  </p>
                </div>
                <div className="session-time">
                  <strong>{formatMinutes(session.durationMinutes)}</strong>
                  <p className="timer-muted">{formatTimeOfDay(session.startTime)}</p>
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
