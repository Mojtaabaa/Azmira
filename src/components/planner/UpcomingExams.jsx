import { getSubjectById } from '../../data/subjects'
import { formatGregorian, formatJalali } from '../../utils/week'

export default function UpcomingExams({ exams, today, showJalali, onEdit }) {
  if (exams.length === 0) return null

  return (
    <section className="upcoming-exams" aria-label="آزمون‌های پیش رو">
      <h2>آزمون‌های پیش رو</h2>
      <ul className="upcoming-strip">
        {exams.map((exam, index) => {
          const subject = exam.subject ? getSubjectById(exam.subject) : null
          return (
            <li key={exam.id}>
              <button
                type="button"
                className={index === 0 ? 'is-next' : undefined}
                data-subject={exam.subject || 'none'}
                data-custom={subject?.custom ? 'true' : 'false'}
                style={{ '--subject': subject?.color || 'var(--accent)' }}
                onClick={() => onEdit(exam)}
              >
                <strong>{exam.title}</strong>
                <span>
                  {subject ? `${subject.name} · ` : ''}
                  {exam.date === today ? 'امروز · ' : ''}
                  {formatGregorian(exam.date)}
                  {showJalali ? ` · ${formatJalali(exam.date)}` : ''}
                  {exam.startTime ? (
                    <span className="plan-time" dir="ltr">{` · ${exam.startTime}`}</span>
                  ) : null}
                </span>
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
