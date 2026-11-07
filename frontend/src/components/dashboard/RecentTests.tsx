// @ts-nocheck
import { Link } from 'react-router-dom'
import { getSubjectById } from '../../data/subjects'
import { formatDateLabel } from '../../utils/clock'
import { formatKonkoorPercentage } from '../../utils/score'

export default function RecentTests({ tests }) {
  return (
    <section className="card">
      <h2>آخرین آزمون‌ها</h2>
      {tests.length === 0 ? (
        <p className="card-copy">هنوز آزمونی ثبت نشده.</p>
      ) : (
        <ul className="plain-list">
          {tests.map((test) => {
            const subject = getSubjectById(test.subject)
            const score = formatKonkoorPercentage(test.correct, test.wrong, test.totalQuestions)
            return (
              <li key={test.id}>
                <strong>{subject ? subject.name : 'درس'} · {test.topic}</strong>
                <span>
                  {formatDateLabel(test.date)}
                  {score ? <span dir="ltr">{` · ${score}`}</span> : ''}
                </span>
              </li>
            )
          })}
        </ul>
      )}
      <Link className="text-link" to="/tests">آزمون‌ها</Link>
    </section>
  )
}
