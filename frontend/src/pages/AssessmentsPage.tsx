// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './LearningIntelligence.css'

export default function AssessmentsPage() {
  const { status } = useAuth()
  const [items, setItems] = useState([])
  const [history, setHistory] = useState([])
  const [active, setActive] = useState(null)
  const [attemptId, setAttemptId] = useState(null)
  const [answers, setAnswers] = useState({})
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [title, setTitle] = useState('آزمون خودارزیابی')
  const [subject, setSubject] = useState('ریاضی')
  const [topic, setTopic] = useState('معادله')

  const load = useCallback(async () => {
    try {
      setError('')
      const [list, hist] = await Promise.all([
        request('GET', '/assessments'),
        request('GET', '/assessments/attempts/me'),
      ])
      setItems(list.items || [])
      setHistory(hist.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری آزمون‌ها ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function createSelf() {
    setBusy(true)
    try {
      await request('POST', '/assessments', {
        title,
        kind: 'SelfAssessment',
        subject,
        topic,
        chapter: 'فصل ۱',
        publish: true,
        questions: [
          {
            type: 'MultipleChoice',
            prompt: `۲ + ۲ چند می‌شود؟ (${topic})`,
            options: ['۳', '۴', '۵'],
            correctAnswer: '۴',
            points: 1,
            subject,
            topic,
          },
          {
            type: 'TrueFalse',
            prompt: 'مشتق تابع ثابت صفر است.',
            options: [true, false],
            correctAnswer: true,
            points: 1,
            subject,
            topic,
          },
          {
            type: 'Descriptive',
            prompt: 'یک کاربرد این مبحث را بنویس.',
            options: [],
            correctAnswer: null,
            points: 2,
            subject,
            topic,
          },
        ],
      })
      await load()
    } catch (err) {
      setError(err.message || 'ساخت آزمون ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function openAssessment(id) {
    setResult(null)
    setAnswers({})
    try {
      const detail = await request('GET', `/assessments/${id}`)
      setActive(detail)
      const started = await request('POST', `/assessments/${id}/attempts`, {})
      setAttemptId(started.attemptId)
    } catch (err) {
      setError(err.message || 'شروع آزمون ممکن نشد.')
    }
  }

  async function submit() {
    if (!attemptId || !active) return
    setBusy(true)
    try {
      const payload = {
        answers: (active.questions || []).map((q) => ({
          questionId: q.id,
          response: answers[q.id] ?? null,
        })),
      }
      setResult(await request('POST', `/assessments/attempts/${attemptId}/submit`, payload))
      setActive(null)
      setAttemptId(null)
      await load()
    } catch (err) {
      setError(err.message || 'ارسال پاسخ ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="learn-page">
      <header className="learn-hero">
        <div>
          <p className="learn-kicker">ارزیابی</p>
          <h1>آزمون‌ها</h1>
          <p className="learn-sub">چندگزینه‌ای، صحیح/غلط، تشریحی — با ثبت نمره و عملکرد سؤال.</p>
        </div>
      </header>

      {error ? <p className="learn-error">{error}</p> : null}
      {result ? (
        <section className="learn-panel">
          <h2>نتیجه</h2>
          <p>
            {result.percent}% · {result.score}/{result.maxScore} · {result.passed ? 'قبول' : 'نیاز به تمرین'} ·{' '}
            {result.durationSeconds} ثانیه
          </p>
        </section>
      ) : null}

      {!active ? (
        <>
          <section className="learn-panel">
            <h2>خودارزیابی سریع</h2>
            <div className="learn-row">
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="عنوان" />
              <input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="موضوع" />
              <input value={topic} onChange={(e) => setTopic(e.target.value)} placeholder="مبحث" />
              <button type="button" disabled={busy} onClick={createSelf}>
                ساخت و انتشار
              </button>
            </div>
          </section>

          <section className="learn-panel">
            <h2>فهرست</h2>
            <ul className="learn-list">
              {items.length === 0 ? <li className="muted">آزمونی نیست.</li> : null}
              {items.map((item) => (
                <li key={item.id}>
                  <div>
                    <strong>{item.title}</strong>
                    <span className="muted">
                      {item.kind} · {item.subject} · {item.questionCount} سؤال
                    </span>
                  </div>
                  <button type="button" onClick={() => openAssessment(item.id)}>
                    شروع
                  </button>
                </li>
              ))}
            </ul>
          </section>

          <section className="learn-panel">
            <h2>تاریخچه</h2>
            <ul className="learn-list">
              {history.map((item) => (
                <li key={item.id}>
                  <strong>{item.title}</strong>
                  <span>{item.percent}%</span>
                </li>
              ))}
            </ul>
          </section>
        </>
      ) : (
        <section className="learn-panel">
          <h2>{active.title}</h2>
          {(active.questions || []).map((q) => (
            <div key={q.id} className="question-block">
              <p>
                <strong>
                  {q.ordinal}. [{q.type}]
                </strong>{' '}
                {q.prompt}
              </p>
              {q.type === 'MultipleChoice' ? (
                <div className="learn-row wrap">
                  {(q.options || []).map((opt) => (
                    <label key={String(opt)} className="learn-check">
                      <input
                        type="radio"
                        name={q.id}
                        checked={answers[q.id] === opt}
                        onChange={() => setAnswers((prev) => ({ ...prev, [q.id]: opt }))}
                      />
                      {String(opt)}
                    </label>
                  ))}
                </div>
              ) : null}
              {q.type === 'TrueFalse' ? (
                <div className="learn-row">
                  {[true, false].map((opt) => (
                    <label key={String(opt)} className="learn-check">
                      <input
                        type="radio"
                        name={q.id}
                        checked={answers[q.id] === opt}
                        onChange={() => setAnswers((prev) => ({ ...prev, [q.id]: opt }))}
                      />
                      {opt ? 'صحیح' : 'غلط'}
                    </label>
                  ))}
                </div>
              ) : null}
              {q.type === 'Descriptive' ? (
                <textarea
                  rows={3}
                  value={answers[q.id] || ''}
                  onChange={(e) => setAnswers((prev) => ({ ...prev, [q.id]: e.target.value }))}
                />
              ) : null}
            </div>
          ))}
          <button type="button" disabled={busy} onClick={submit}>
            ارسال پاسخ‌ها
          </button>
        </section>
      )}
    </div>
  )
}
