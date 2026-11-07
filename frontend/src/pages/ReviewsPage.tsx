// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './LearningIntelligence.css'

export default function ReviewsPage() {
  const { status } = useAuth()
  const [due, setDue] = useState([])
  const [history, setHistory] = useState([])
  const [subject, setSubject] = useState('ریاضی')
  const [topic, setTopic] = useState('معادله')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setError('')
      const [duePayload, hist] = await Promise.all([
        request('GET', '/reviews/due'),
        request('GET', '/reviews/history'),
      ])
      setDue(duePayload.items || [])
      setHistory(hist.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری بازبینی ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function addCard() {
    setBusy(true)
    try {
      await request('POST', '/reviews/cards', { subject, topic, chapter: 'فصل ۱' })
      await load()
    } catch (err) {
      setError(err.message || 'افزودن کارت ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function review(cardId, outcome) {
    setBusy(true)
    try {
      await request('POST', `/reviews/${cardId}`, { outcome, difficulty: 3, confidence: 3 })
      await load()
    } catch (err) {
      setError(err.message || 'ثبت بازبینی ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="learn-page">
      <header className="learn-hero">
        <div>
          <p className="learn-kicker">بازبینی فاصله‌دار</p>
          <h1>مرورها</h1>
          <p className="learn-sub">سررسید، دشواری، اطمینان، و تاریخچه الگوریتمی قابل ارتقا.</p>
        </div>
      </header>
      {error ? <p className="learn-error">{error}</p> : null}

      <section className="learn-panel">
        <h2>کارت جدید</h2>
        <div className="learn-row">
          <input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="موضوع" />
          <input value={topic} onChange={(e) => setTopic(e.target.value)} placeholder="مبحث" />
          <button type="button" disabled={busy} onClick={addCard}>
            افزودن
          </button>
        </div>
      </section>

      <section className="learn-panel">
        <h2>سررسید / عقب‌افتاده</h2>
        <ul className="learn-list">
          {due.length === 0 ? <li className="muted">کارت سررسیدی نیست.</li> : null}
          {due.map((item) => (
            <li key={item.id}>
              <div>
                <strong>
                  {item.subject} / {item.topic}
                </strong>
                <span className="muted">
                  {item.overdueDays > 0 ? `${item.overdueDays} روز عقب` : 'امروز'} · فاصله {item.intervalDays} روز
                </span>
              </div>
              <div className="learn-actions">
                <button type="button" disabled={busy} onClick={() => review(item.id, 'Fail')}>
                  سخت
                </button>
                <button type="button" disabled={busy} onClick={() => review(item.id, 'Good')}>
                  خوب
                </button>
                <button type="button" disabled={busy} onClick={() => review(item.id, 'Easy')}>
                  آسان
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>

      <section className="learn-panel">
        <h2>تاریخچه</h2>
        <ul className="learn-list">
          {history.slice(0, 20).map((item) => (
            <li key={item.id}>
              <span>
                {item.outcome} · {item.successful ? 'موفق' : 'ناموفق'}
              </span>
              <span className="muted">
                {item.intervalBeforeDays}→{item.intervalAfterDays} روز
              </span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
