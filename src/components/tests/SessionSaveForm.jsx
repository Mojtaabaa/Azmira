import DatePicker from '../DatePicker'
import SubjectField from '../SubjectField'
import { todayInputValue } from '../../utils/clock'
import { tally } from '../../utils/testRecord'

export default function SessionSaveForm({ meta, editing, onChange, onSubmit, onCancel, questions, error }) {
  const counts = tally(questions)

  function update(field, value) {
    onChange(field, value)
  }

  return (
    <section className="card">
      <h2>{editing ? 'ذخیره تغییرات' : 'ذخیره این آزمون'}</h2>
      <p className="card-copy">
        {counts.total} سوال · {counts.correct} درست · {counts.wrong} غلط · {counts.blank} سفید
        {counts.open > 0 ? ` · ${counts.open} هنوز علامت نخورده` : ''}
      </p>
      {counts.score ? (
        <p className={counts.score.startsWith('-') ? 'score-preview is-negative' : 'score-preview'}>
          درصد کنکور: {counts.score}
        </p>
      ) : (
        <p className="card-copy">درصد بعد از علامت‌گذاری همه پاسخ‌ها نشان داده می‌شود.</p>
      )}
      <form className="exam-form" noValidate onSubmit={onSubmit}>
        <label>
          تاریخ
          <DatePicker
            variant="field"
            value={meta.date}
            max={todayInputValue()}
            onChange={(next) => update('date', next)}
          />
        </label>
        <label>
          درس
          <SubjectField value={meta.subject} onChange={(id) => update('subject', id)} />
        </label>
        <label>
          مبحث
          <input
            dir="auto"
            value={meta.topic}
            maxLength={80}
            autoComplete="off"
            onChange={(event) => update('topic', event.target.value)}
          />
        </label>
        <label>
          منبع
          <input
            dir="auto"
            value={meta.source}
            maxLength={80}
            autoComplete="off"
            placeholder="کلاس، کتاب یا آزمون"
            onChange={(event) => update('source', event.target.value)}
          />
        </label>
        <label className="span-2">
          دقیقه
          <input
            type="number"
            min="1"
            max="1440"
            step="1"
            inputMode="numeric"
            value={meta.duration}
            onChange={(event) => update('duration', event.target.value)}
          />
        </label>
        {error ? (
          <p className="form-message is-error span-2" role="alert">
            {error}
          </p>
        ) : null}
        <div className="button-row span-2">
          <button type="submit" className="button button-primary">
            {editing ? 'ذخیره تغییرات' : 'ذخیره آزمون'}
          </button>
          {editing ? (
            <button type="button" className="button" onClick={onCancel}>
              انصراف
            </button>
          ) : null}
        </div>
      </form>
    </section>
  )
}
