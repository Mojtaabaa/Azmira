// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import CapabilityGate from '../components/CapabilityGate'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function ParentPortalPage() {
  return (
    <CapabilityGate require="parent" title="پورتال والدین">
      <ParentPortalInner />
    </CapabilityGate>
  )
}

function ParentPortalInner() {
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
        <p className="muted">ابتدا به فضای مدرسه سوییچ کن.</p>
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
        <h2>خلاصه</h2>
        <div className="school-stats">
          <div><strong>{children.length}</strong><span>فرزند تأییدشده</span></div>
        </div>
      </section>

      <section className="school-panel">
        <h2>درخواست ارتباط</h2>
        <div className="school-row">
          <input value={studentUserId} onChange={(e) => setStudentUserId(e.target.value)} placeholder="شناسه دانش‌آموز" />
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
        {children.length === 0 ? <p className="muted">هنوز فرزندی تأیید نشده.</p> : null}
        <div className="school-table-wrap">
          <table className="school-table">
            <thead>
              <tr>
                <th>نام</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {children.map((item) => (
                <tr key={item.studentUserId}>
                  <td>{item.displayName}</td>
                  <td>
                    <button
                      type="button"
                      onClick={async () => {
                        setSummary(await request('GET', `/parent/children/${item.studentUserId}/summary`))
                      }}
                    >
                      خلاصه
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {summary ? (
          <div className="school-stats school-stats-secondary">
            <div><strong>{summary.xp ?? summary.totalXp ?? '—'}</strong><span>XP</span></div>
            <div><strong>{summary.streak ?? summary.streakCurrent ?? '—'}</strong><span>استریک</span></div>
            <div><strong>{summary.level ?? '—'}</strong><span>سطح</span></div>
            <div><strong>{String(summary.readOnly ?? true)}</strong><span>فقط‌خواندنی</span></div>
          </div>
        ) : null}
      </section>
    </div>
  )
}
