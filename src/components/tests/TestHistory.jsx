import { useState } from 'react'
import { getSubjectById } from '../../data/subjects'
import { formatDateLabel, formatMinutes } from '../../utils/clock'
import { formatKonkoorPercentage, konkoorPercentage } from '../../utils/score'

export default function TestHistory({ tests, activeId, editingId, onEdit, onActivate, onDelete }) {
  const [confirmingId, setConfirmingId] = useState(null)
  const ordered = tests
    .slice()
    .sort((a, b) => String(b.date).localeCompare(String(a.date)) || String(b.id).localeCompare(String(a.id)))

  function removeTest(test) {
    onDelete(test)
    setConfirmingId(null)
  }

  return (
    <section className="card">
      <h2>آزمون‌های ذخیره‌شده</h2>
      {ordered.length === 0 ? (
        <p className="card-copy">هنوز آزمونی نیست.</p>
      ) : (
        <ul className="test-list">
          {ordered.map((test) => {
            const subject = getSubjectById(test.subject)
            const score = formatKonkoorPercentage(test.correct, test.wrong, test.totalQuestions)
            const negative = (konkoorPercentage(test.correct, test.wrong, test.totalQuestions) || 0) < 0
            const confirming = confirmingId === test.id
            const rowClass = [
              activeId === test.id ? 'is-active' : '',
              editingId === test.id ? 'is-editing' : '',
            ]
              .filter(Boolean)
              .join(' ')
            return (
              <li key={test.id} className={rowClass || undefined}>
                <div className="test-row">
                  <div>
                    <strong>{subject ? subject.name : test.subject}</strong>
                    <p className="card-copy">
                      {test.topic} · {test.source} · {formatDateLabel(test.date)}
                      {test.entry === 'manual' ? ' · دستی' : ''}
                    </p>
                    <p className="card-copy">
                      {test.correct} درست، {test.wrong} غلط، {test.blank} سفید · {formatMinutes(test.durationMinutes)}
                    </p>
                  </div>
                  <p className={negative ? 'score-pill is-negative' : 'score-pill'}>{score}</p>
                </div>
                <div className="button-row">
                  <button type="button" className="button" onClick={() => onEdit(test)}>
                    {editingId === test.id ? 'در حال ویرایش' : 'ویرایش'}
                  </button>
                  <button type="button" className="button" onClick={() => onActivate(test.id)}>
                    ثبت اشتباه
                  </button>
                  {confirming ? (
                    <>
                      <button type="button" className="button button-danger is-solid" onClick={() => removeTest(test)}>
                        تایید حذف
                      </button>
                      <button type="button" className="button" onClick={() => setConfirmingId(null)}>
                        انصراف
                      </button>
                    </>
                  ) : (
                    <button type="button" className="button button-danger" onClick={() => setConfirmingId(test.id)}>
                      حذف
                    </button>
                  )}
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
