import { useRef, useState } from 'react'
import { useCollection } from '../hooks/useCollection'
import { downloadBackup, importAllData } from '../utils/storage'

export default function DataBackup() {
  const fileRef = useRef(null)
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const topics = useCollection('topics')
  const tasks = useCollection('tasks')
  const events = useCollection('events')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const counts = [
    ['جلسه‌ها', sessions.length],
    ['آزمون‌ها', tests.length],
    ['اشتباهات', mistakes.length],
    ['مباحث', topics.length],
    ['کارها', tasks.length],
    ['برنامه‌ها', events.length],
  ]

  async function handleFile(event) {
    const file = event.target.files?.[0]
    event.target.value = ''
    setMessage('')
    setError('')
    if (!file) return

    let parsed
    try {
      parsed = JSON.parse(await file.text())
    } catch {
      setError('این فایل JSON معتبر نیست.')
      return
    }

    const confirmed = window.confirm('همه داده‌های ذخیره‌شده با این فایل عوض شود؟')
    if (!confirmed) return

    try {
      importAllData(parsed)
      setMessage('پشتیبان وارد شد و داده‌ها جایگزین شدند.')
    } catch (importError) {
      setError(importError.message || 'این فایل وارد نشد.')
    }
  }

  return (
    <section className="card">
      <h2>پشتیبان</h2>
      <p className="card-copy">
        می‌توانی همه داده‌ها و تنظیمات را در یک فایل پشتیبان دانلود کنی، یا با بارگذاری فایل، داده‌های حسابت را عوض کنی.
      </p>
      <div className="button-row">
        <button type="button" className="button button-primary" onClick={downloadBackup}>
          دانلود پشتیبان
        </button>
        <button type="button" className="button" onClick={() => fileRef.current?.click()}>
          بارگذاری پشتیبان
        </button>
        <input
          ref={fileRef}
          type="file"
          accept="application/json,.json"
          onChange={handleFile}
          hidden
        />
      </div>
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
      <ul className="stat-grid">
        {counts.map(([label, count]) => (
          <li key={label}>
            <span>{label}</span>
            <strong>{count}</strong>
          </li>
        ))}
      </ul>
    </section>
  )
}
