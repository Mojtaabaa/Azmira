import { useState } from 'react'
import { causeLabel } from '../../data/causes'
import { getSubjectById } from '../../data/subjects'
import { formatDateLabel } from '../../utils/clock'

export default function MistakeCard({ mistake, test, onToggle, onDelete }) {
  const [confirming, setConfirming] = useState(false)
  const subject = getSubjectById(mistake.subject)

  return (
    <article className={mistake.reviewed ? 'mistake-card' : 'mistake-card is-open'}>
      <div>
        <strong>
          {subject ? subject.name : mistake.subject} · س{mistake.questionNumber}
        </strong>
        <p className="card-copy">
          {mistake.topic} · {causeLabel(mistake.cause)}
          {test ? ` · ${test.source} · ${formatDateLabel(test.date)}` : ''}
        </p>
        {mistake.note ? <p>{mistake.note}</p> : null}
      </div>
      <div className="button-row">
        <button type="button" className="button" onClick={() => onToggle(mistake)}>
          {mistake.reviewed ? 'مرور دوباره' : 'مرور شد'}
        </button>
        {confirming ? (
          <>
            <button type="button" className="button button-danger is-solid" onClick={() => onDelete(mistake)}>
              تایید حذف
            </button>
            <button type="button" className="button" onClick={() => setConfirming(false)}>
              انصراف
            </button>
          </>
        ) : (
          <button type="button" className="button button-danger" onClick={() => setConfirming(true)}>
            حذف
          </button>
        )}
      </div>
    </article>
  )
}
