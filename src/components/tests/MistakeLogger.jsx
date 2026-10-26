import { useState } from 'react'
import { CAUSES, causeLabel } from '../../data/causes'
import { addOne } from '../../utils/storage'
import { parseWholeNumber } from '../../utils/testRecord'

export default function MistakeLogger({ test, mistakes }) {
  const [questionNumber, setQuestionNumber] = useState('')
  const [topic, setTopic] = useState(test.topic || '')
  const [cause, setCause] = useState('concept')
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const logged = mistakes.filter((mistake) => mistake.testId === test.id)
  const known = Array.isArray(test.questions) && test.questions.length > 0
    ? test.questions.map((item) => item.number)
    : null
  const wrongNumbers = Array.isArray(test.questions)
    ? test.questions.filter((item) => item.mark === 'wrong').map((item) => item.number)
    : []

  function handleSubmit(event) {
    event.preventDefault()
    const number = parseWholeNumber(questionNumber)
    const cleanTopic = topic.trim()
    if (number == null || number < 1) {
      setError('شماره سوال را بنویس.')
      return
    }
    if (known && !known.includes(number)) {
      setError('این سوال در این آزمون نیست.')
      return
    }
    if (!known && number > test.totalQuestions) {
      setError(`شماره سوال باید از 1 تا ${test.totalQuestions} باشد.`)
      return
    }
    if (!cleanTopic) {
      setError('مبحث را بنویس.')
      return
    }
    if (!CAUSES.some((item) => item.id === cause)) {
      setError('علت را انتخاب کن.')
      return
    }

    addOne('mistakes', {
      testId: test.id,
      questionNumber: number,
      subject: test.subject,
      topic: cleanTopic,
      cause,
      reviewed: false,
      note: note.trim(),
    })
    setQuestionNumber('')
    setNote('')
    setError('')
    setMessage('اشتباه ثبت شد.')
  }

  return (
    <section className="card">
      <h2>ثبت اشتباه</h2>
      <p className="card-copy">سوال به سوال، برای همین آزمون.</p>
      {wrongNumbers.length > 0 ? (
        <div className="cause-row">
          {wrongNumbers.map((number) => (
            <button
              key={number}
              type="button"
              className={String(questionNumber) === String(number) ? 'cause-chip is-selected' : 'cause-chip'}
              onClick={() => {
                setQuestionNumber(String(number))
                setError('')
                setMessage('')
              }}
            >
              س{number}
            </button>
          ))}
        </div>
      ) : null}
      <form className="exam-form" noValidate onSubmit={handleSubmit}>
        <label>
          شماره سوال
          <input
            type="number"
            min="1"
            max={test.totalQuestions}
            step="1"
            inputMode="numeric"
            value={questionNumber}
            onChange={(event) => {
              setQuestionNumber(event.target.value)
              setError('')
              setMessage('')
            }}
          />
        </label>
        <label>
          مبحث
          <input
            dir="auto"
            value={topic}
            maxLength={80}
            onChange={(event) => {
              setTopic(event.target.value)
              setError('')
              setMessage('')
            }}
          />
        </label>
        <div className="span-2">
          <p className="field-label">علت</p>
          <div className="cause-row" role="radiogroup" aria-label="علت">
            {CAUSES.map((item) => (
              <button
                key={item.id}
                type="button"
                role="radio"
                aria-checked={cause === item.id}
                className={cause === item.id ? 'cause-chip is-selected' : 'cause-chip'}
                onClick={() => setCause(item.id)}
              >
                {item.label}
              </button>
            ))}
          </div>
        </div>
        <label className="span-2">
          یادداشت
          <input
            dir="auto"
            value={note}
            maxLength={280}
            onChange={(event) => {
              setNote(event.target.value)
              setError('')
              setMessage('')
            }}
          />
        </label>
        {error ? (
          <p className="form-message is-error span-2" role="alert">
            {error}
          </p>
        ) : null}
        {message ? (
          <p className="form-message is-ok span-2" role="status">
            {message}
          </p>
        ) : null}
        <div className="button-row span-2">
          <button type="submit" className="button button-primary">
            افزودن اشتباه
          </button>
        </div>
      </form>
      {logged.length > 0 ? (
        <ul className="mini-list">
          {logged.map((mistake) => (
            <li key={mistake.id}>
              س{mistake.questionNumber} · {causeLabel(mistake.cause)}
              {mistake.note ? ` · ${mistake.note}` : ''}
              {mistake.reviewed ? ' · مرورشده' : ''}
            </li>
          ))}
        </ul>
      ) : (
        <p className="card-copy">برای این آزمون هنوز اشتباهی ثبت نشده.</p>
      )}
    </section>
  )
}
