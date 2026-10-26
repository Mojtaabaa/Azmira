import { Link } from 'react-router-dom'
import { getSubjectById } from '../../data/subjects'
import { eventTypeLabel } from '../../utils/events'

export default function TodayLists({ tasks, events, overdue }) {
  return (
    <div className="dash-split">
      <section className="card">
        <h2>کارهای امروز</h2>
        {tasks.length === 0 ? (
          <p className="card-copy">برای امروز کاری نیست.</p>
        ) : (
          <ul className="plain-list">
            {tasks.map((task) => {
              const subject = task.subject ? getSubjectById(task.subject) : null
              return (
                <li key={task.id}>
                  <strong>{task.title}</strong>
                  <span>{subject ? subject.name : 'بدون درس'}</span>
                </li>
              )
            })}
          </ul>
        )}
        {overdue > 0 ? (
          <p className="flag">{overdue} کار عقب افتاده</p>
        ) : null}
        <Link className="text-link" to="/tasks">همه کارها</Link>
      </section>
      <section className="card">
        <h2>رویدادهای امروز</h2>
        {events.length === 0 ? (
          <p className="card-copy">برای امروز رویدادی نیست.</p>
        ) : (
          <ul className="plain-list">
            {events.map((event) => (
              <li key={event.id} className={event.done ? 'is-done' : undefined}>
                <strong>{event.title}</strong>
                <span>
                  {eventTypeLabel(event.type)}
                  {event.startTime ? <span dir="ltr">{` ${event.startTime}`}</span> : ''}
                </span>
              </li>
            ))}
          </ul>
        )}
        <Link className="text-link" to="/planner">برنامه هفته</Link>
      </section>
    </div>
  )
}
