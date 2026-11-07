// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function ParentPortalPage() {
  const { status, workspaces, activeWorkspaceId } = useAuth()
  const [children, setChildren] = useState([])
  const [summary, setSummary] = useState(null)
  const [studentUserId, setStudentUserId] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const isSchool = active?.type === 'school'

  const load = useCallback(async () => {
    if (!isSchool) return
    try {
      setError('')
      const payload = await request('GET', '/parent/children')
      setChildren(payload.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری فرزندان ممکن نشد.')
    }
  }, [isSchool])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  if (!isSchool) {
    return (
      <div className="school-page">
        <h1>پورتال والدین</h1>
        <p>ابتدا به فضای مدرسه سوییچ کن.</p>
      </div>
    )
  }

  return (
    <div className="school-page">
      <header className="school-hero">
        <div>
          <p className="school-kicker">والدین</p>
          <h1>خلاصه فرزندان</h1>
          <p className="school-sub">فقط خواندنی پس از تأیید ارتباط توسط مدرسه.</p>
        </div>
      </header>
      {error ? <p className="school-error">{error}</p> : null}
      {message ? <p className="school-ok">{message}</p> : null}

      <section className="school-panel">
        <h2>درخواست ارتباط</h2>
        <div className="school-row">
          <input value={studentUserId} onChange={(e) => setStudentUserId(e.target.value)} placeholder="studentUserId" />
          <button
            type="button"
            onClick={async () => {
              try {
                const result = await request('POST', '/parent/links', { studentUserId: Number(studentUserId) })
                setMessage(`وضعیت: ${result.status}`)
                setStudentUserId('')
                await load()
              } catch (err) {
                setError(err.message)
              }
            }}
          >
            ارسال درخواست
          </button>
        </div>
      </section>

      <section className="school-panel">
        <h2>فرزندان تأییدشده</h2>
        <ul className="school-list">
          {children.length === 0 ? <li className="muted">هنوز فرزندی تأیید نشده.</li> : null}
          {children.map((item) => (
            <li key={item.studentUserId}>
              <strong>{item.displayName}</strong>
              <button
                type="button"
                onClick={async () => {
                  setSummary(await request('GET', `/parent/children/${item.studentUserId}/summary`))
                }}
              >
                خلاصه
              </button>
            </li>
          ))}
        </ul>
        {summary ? (
          <>
            <p className="muted">readOnly: {String(summary.readOnly)}</p>
            <pre className="school-facts">{JSON.stringify(summary, null, 2)}</pre>
          </>
        ) : null}
      </section>
    </div>
  )
}
