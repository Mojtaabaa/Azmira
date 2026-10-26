import { useState } from 'react'
import DatePicker from '../DatePicker'
import SubjectField from '../SubjectField'
import { todayInputValue } from '../../utils/clock'
import { formatKonkoorPercentage } from '../../utils/score'
import { addOne, updateOne } from '../../utils/storage'
import { subjectFromForm } from '../../utils/subjects'
import { buildManualTest, parseWholeNumber } from '../../utils/testRecord'

function freshForm() {
  return {
    date: todayInputValue(),
    subject: '',
    topic: '',
    source: '',
    correct: '',
    wrong: '',
    blank: '',
    duration: '',
  }
}

function formFromTest(test) {
  if (!test) return freshForm()
  return {
    date: test.date || todayInputValue(),
    subject: test.subject || '',
    topic: test.topic || '',
    source: test.source || '',
    correct: String(test.correct ?? ''),
    wrong: String(test.wrong ?? ''),
    blank: String(test.blank ?? ''),
    duration: test.durationMinutes ? String(test.durationMinutes) : '',
  }
}

function liveTotal(form) {
  const correct = form.correct === '' ? 0 : parseWholeNumber(form.correct)
  const wrong = form.wrong === '' ? 0 : parseWholeNumber(form.wrong)
  const blank = form.blank === '' ? 0 : parseWholeNumber(form.blank)
  if (correct == null || wrong == null || blank == null) return null
  const total = correct + wrong + blank
  if (total < 1) return null
  return {
    total,
    score: formatKonkoorPercentage(correct, wrong, total),
  }
}

export default function ManualTestForm({ test, onSaved, onCancel }) {
  const [form, setForm] = useState(() => formFromTest(test))
  const [error, setError] = useState('')
  const editing = Boolean(test)
  const preview = liveTotal(form)

  function update(field, value) {
    setForm((current) => ({ ...current, [field]: value }))
    setError('')
  }

  function handleSubmit(event) {
    event.preventDefault()
    const subject = subjectFromForm(event.currentTarget)
    if (subject.error) {
      setError(subject.error)
      return
    }

    const result = buildManualTest({ ...form, subject: subject.id })
    if (result.error) {
      setError(result.error)
      return
    }

    if (editing) {
      const saved = updateOne('tests', test.id, result.test)
      if (!saved) {
        setError('این آزمون دیگر در فهرست نیست.')
        return
      }
      onSaved(saved)
      return
    }

    const saved = addOne('tests', result.test)
    setForm({ ...freshForm(), subject: subject.id })
    onSaved(saved)
  }

  return (
    <section className="card" id="manual-test">
      <h2>{editing ? 'ویرایش آزمون دستی' : 'ثبت آزمون دستی'}</h2>
      <p className="card-copy">اگر فقط تعداد درست، غلط و سفید را داری، همین‌جا بنویس. خانه خالی صفر حساب می‌شود.</p>
      {preview ? (
        <p className={preview.score.startsWith('-') ? 'score-preview is-negative' : 'score-preview'}>
          {preview.total} تست · درصد کنکور: {preview.score}
        </p>
      ) : null}
      <form className="exam-form" noValidate onSubmit={handleSubmit}>
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
            onChange={(event) => update('topic', event.target.value)}
          />
        </label>
        <label>
          منبع
          <input
            dir="auto"
            value={form.source}
            maxLength={80}
            autoComplete="off"
            placeholder="کلاس، کتاب یا آزمون"
            onChange={(event) => update('source', event.target.value)}
          />
        </label>
        <label>
          درست
          <input
            type="number"
            min="0"
            max="999"
            step="1"
            inputMode="numeric"
            value={form.correct}
            onChange={(event) => update('correct', event.target.value)}
          />
        </label>
        <label>
          غلط
          <input
            type="number"
            min="0"
            max="999"
            step="1"
            inputMode="numeric"
            value={form.wrong}
            onChange={(event) => update('wrong', event.target.value)}
          />
        </label>
        <label>
          سفید
          <input
            type="number"
            min="0"
            max="999"
            step="1"
            inputMode="numeric"
            value={form.blank}
            onChange={(event) => update('blank', event.target.value)}
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
        {error ? (
          <p className="form-message is-error span-2" role="alert">
            {error}
          </p>
        ) : null}
        <div className="button-row span-2">
          <button type="submit" className="button button-primary">
            {editing ? 'ذخیره تغییرات' : 'ثبت آزمون'}
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
