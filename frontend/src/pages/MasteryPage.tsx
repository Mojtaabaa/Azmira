// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './LearningIntelligence.css'

const LEVEL_LABEL = {
  NotStarted: 'شروع‌نشده',
  Learning: 'در حال یادگیری',
  NeedsReview: 'نیاز به مرور',
  Developing: 'در حال پیشرفت',
  Proficient: 'مسلط',
  Mastered: 'استاد',
}

export default function MasteryPage() {
  const { status } = useAuth()
  const [items, setItems] = useState([])
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    try {
      setError('')
      const payload = await request('GET', '/mastery')
      setItems(payload.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری نقشه تسلط ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  return (
    <div className="learn-page">
      <header className="learn-hero">
        <div>
          <p className="learn-kicker">تسلط</p>
          <h1>نقشه تسلط</h1>
          <p className="learn-sub">موضوع → فصل → مبحث · فقط بر اساس شواهد آزمون و بازبینی، نه صرفاً زمان مطالعه.</p>
        </div>
      </header>
      {error ? <p className="learn-error">{error}</p> : null}
      <section className="learn-panel">
        <ul className="learn-list">
          {items.length === 0 ? <li className="muted">هنوز شواهدی ثبت نشده. یک آزمون بده یا بازبینی کن.</li> : null}
          {items.map((item) => (
            <li key={item.id}>
              <div>
                <strong>
                  {item.subject}
                  {item.chapter ? ` / ${item.chapter}` : ''}
                  {item.topic ? ` / ${item.topic}` : ''}
                </strong>
                <span className="muted">
                  EMA {Math.round((item.scoreEma || 0) * 100)}% · {item.evidenceCount} شاهد
                </span>
              </div>
              <span className={`mastery-pill level-${item.level}`}>{LEVEL_LABEL[item.level] || item.level}</span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
