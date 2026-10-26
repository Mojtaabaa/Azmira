import { useState } from 'react'
import DatePicker from '../DatePicker'
import SubjectField from '../SubjectField'
import { getSubjectById } from '../../data/subjects'
import { subjectFromForm } from '../../utils/subjects'
import { dateAtCurrentTime, isFutureDate, isRealDateString, todayInputValue } from '../../utils/clock'
import { addOne } from '../../utils/storage'

const EMPTY = {
  subject: '',
  topic: '',
  date: todayInputValue(),
  duration: '',
  note: '',
}

export default function ManualSessionForm() {
  const [form, setForm] = useState(EMPTY)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const topics = getSubjectById(form.subject)?.topics || []

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }))
    setError('')
    setMessage('')
  }

  function handleSubmit(event) {
    event.preventDefault()
    const topic = form.topic.trim()
    const note = form.note.trim()
    const duration = Number(form.duration)

    const subject = subjectFromForm(event.currentTarget)
    if (subject.error) {
      setError(subject.error)
      return
    }
    if (!topic) {
      setError('مبحث را بنویس.')
      return
    }
    if (!form.date) {
      setError('تاریخ را انتخاب کن.')
      return
    }
    if (!isRealDateString(form.date)) {
      setError('یک تاریخ واقعی وارد کن.')
      return
    }
    if (isFutureDate(form.date)) {
      setError('تاریخ نمی‌تواند در آینده باشد.')
      return
    }
    if (form.duration === '' || !Number.isInteger(duration) || duration <= 0) {
      setError('دقیقه باید یک عدد درست مثبت باشد.')
      return
    }
    if (duration > 1440) {
      setError('دقیقه نمی‌تواند بیشتر از 1440 باشد.')
      return
    }

    addOne('sessions', {
      subject: subject.id,
      topic,
      startTime: dateAtCurrentTime(form.date),
      durationMinutes: duration,
      type: 'manual',
      note,
    })
    setForm({ ...EMPTY, date: todayInputValue(), subject: subject.id })
    setMessage('جلسه اضافه شد.')
  }

  return (
    <section className="timer-card">
      <h2>ثبت مطالعه بدون زمان‌سنج</h2>
      <p className="timer-muted">برای کاری که تمام شده.</p>
      <form className="manual-form" noValidate onSubmit={handleSubmit}>
        <label>
          درس
          <SubjectField value={form.subject} onChange={(id) => update('subject', id)} />
        </label>
        <label>
          مبحث
          <input
            dir="auto"
            value={form.topic}
            maxLength={80}
            autoComplete="off"
            list="manual-topics"
            onChange={(event) => update('topic', event.target.value)}
          />
          <datalist id="manual-topics">
            {topics.map((name) => (
              <option key={name} value={name} />
            ))}
          </datalist>
        </label>
        <label>
          تاریخ
          <DatePicker
            variant="field"
            value={form.date}
            max={todayInputValue()}
            onChange={(next) => update('date', next)}
          />
        </label>
        <label>
          دقیقه
          <input
            type="number"
            min="1"
            max="1440"
            step="1"
            inputMode="numeric"
            value={form.duration}
            onChange={(event) => update('duration', event.target.value)}
          />
        </label>
        <label className="manual-note">
          یادداشت
          <input
            dir="auto"
            value={form.note}
            maxLength={280}
            onChange={(event) => update('note', event.target.value)}
          />
        </label>
        <div className="manual-actions">
          <button type="submit" className="button button-primary">
            افزودن جلسه
          </button>
        </div>
      </form>
      {error ? (
        <p className="form-message is-error" role="alert">
          {error}
        </p>
      ) : null}
      {message ? (
        <p className="form-message is-ok" role="status">
          {message}
        </p>
      ) : null}
    </section>
  )
}
