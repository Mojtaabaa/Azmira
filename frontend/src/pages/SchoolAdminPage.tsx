// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function SchoolAdminPage() {
  const { status, user, workspaces, activeWorkspaceId, switchWorkspace, retry } = useAuth()
  const [profile, setProfile] = useState(null)
  const [members, setMembers] = useState([])
  const [years, setYears] = useState([])
  const [grades, setGrades] = useState([])
  const [subjects, setSubjects] = useState([])
  const [classes, setClasses] = useState([])
  const [schoolName, setSchoolName] = useState('')
  const [gradeName, setGradeName] = useState('')
  const [subjectName, setSubjectName] = useState('')
  const [className, setClassName] = useState('')
  const [inviteUserId, setInviteUserId] = useState('')
  const [inviteRole, setInviteRole] = useState('Teacher')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const isSchool = active?.type === 'school'
  const canAdmin =
    user?.platformRole === 100 ||
    active?.role === 'schoolOwner' ||
    active?.role === 'schoolAdministrator' ||
    active?.role === 'academicManager'

  const load = useCallback(async () => {
    if (!isSchool) return
    try {
      setError('')
      const [p, y, g, s, c] = await Promise.all([
        request('GET', '/schools/me'),
        request('GET', '/schools/years'),
        request('GET', '/schools/grades'),
        request('GET', '/schools/subjects'),
        request('GET', '/schools/classes'),
      ])
      setProfile(p)
      setYears(y.items || [])
      setGrades(g.items || [])
      setSubjects(s.items || [])
      setClasses(c.items || [])
      if (canAdmin) {
        const m = await request('GET', '/schools/members')
        setMembers(m.items || [])
      }
    } catch (err) {
      setError(err.message || 'بارگذاری مدرسه ممکن نشد.')
    }
  }, [isSchool, canAdmin])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function registerSchool() {
    if (!schoolName.trim()) return
    setBusy(true)
    try {
      setMessage('')
      const created = await request('POST', '/schools', { name: schoolName.trim(), city: 'تهران' })
      if (retry) await retry()
      if (created.workspaceId && switchWorkspace) await switchWorkspace(created.workspaceId)
      setMessage('مدرسه ساخته شد و فضای مدرسه فعال شد.')
    } catch (err) {
      setError(err.message || 'ثبت مدرسه ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  if (!isSchool) {
    return (
      <div className="school-page">
        <header className="school-hero">
          <div>
            <p className="school-kicker">نسخه مدرسه</p>
            <h1>ثبت مدرسه</h1>
            <p className="school-sub">فضای شخصی حفظ می‌شود؛ مدرسه یک Workspace جدا می‌سازد.</p>
          </div>
        </header>
        {error ? <p className="school-error">{error}</p> : null}
        {message ? <p className="school-ok">{message}</p> : null}
        <section className="school-panel">
          <div className="school-row">
            <input value={schoolName} onChange={(e) => setSchoolName(e.target.value)} placeholder="نام مدرسه" />
            <button type="button" disabled={busy} onClick={registerSchool}>
              ثبت مدرسه
            </button>
          </div>
        </section>
      </div>
    )
  }

  return (
    <div className="school-page">
      <header className="school-hero">
        <div>
          <p className="school-kicker">مدیریت مدرسه</p>
          <h1>{profile?.name || 'مدرسه'}</h1>
          <p className="school-sub">{profile?.city || '—'} · نقش شما: {active?.role}</p>
        </div>
      </header>
      {error ? <p className="school-error">{error}</p> : null}
      {message ? <p className="school-ok">{message}</p> : null}

      {canAdmin ? (
        <section className="school-panel">
          <h2>اعضا</h2>
          <div className="school-row">
            <input value={inviteUserId} onChange={(e) => setInviteUserId(e.target.value)} placeholder="userId" />
            <select value={inviteRole} onChange={(e) => setInviteRole(e.target.value)}>
              {['Student', 'Parent', 'Teacher', 'AcademicManager', 'SchoolAdministrator'].map((role) => (
                <option key={role} value={role}>
                  {role}
                </option>
              ))}
            </select>
            <button
              type="button"
              disabled={busy}
              onClick={async () => {
                setBusy(true)
                try {
                  await request('POST', '/schools/members', { userId: Number(inviteUserId), role: inviteRole })
                  setInviteUserId('')
                  await load()
                } catch (err) {
                  setError(err.message)
                } finally {
                  setBusy(false)
                }
              }}
            >
              دعوت/نقش
            </button>
          </div>
          <ul className="school-list">
            {members.map((item) => (
              <li key={item.userId}>
                <strong>{item.displayName}</strong>
                <span className="muted">{item.role}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section className="school-panel">
        <h2>ساختار</h2>
        <p className="muted">
          سال‌ها: {years.length} · پایه‌ها: {grades.length} · دروس: {subjects.length} · کلاس‌ها: {classes.length}
        </p>
        {canAdmin ? (
          <div className="school-row">
            <input value={gradeName} onChange={(e) => setGradeName(e.target.value)} placeholder="پایه جدید" />
            <button
              type="button"
              onClick={async () => {
                await request('POST', '/schools/grades', { name: gradeName, sortOrder: grades.length + 1 })
                setGradeName('')
                await load()
              }}
            >
              افزودن پایه
            </button>
            <input value={subjectName} onChange={(e) => setSubjectName(e.target.value)} placeholder="درس جدید" />
            <button
              type="button"
              onClick={async () => {
                await request('POST', '/schools/subjects', { name: subjectName })
                setSubjectName('')
                await load()
              }}
            >
              افزودن درس
            </button>
            <input value={className} onChange={(e) => setClassName(e.target.value)} placeholder="کلاس جدید" />
            <button
              type="button"
              onClick={async () => {
                await request('POST', '/schools/classes', {
                  name: className,
                  academicYearId: years.find((y) => y.isCurrent)?.id,
                  gradeId: grades[0]?.id,
                })
                setClassName('')
                await load()
              }}
            >
              افزودن کلاس
            </button>
          </div>
        ) : null}
        <ul className="school-list">
          {classes.map((item) => (
            <li key={item.id}>
              <strong>{item.name}</strong>
              <span className="muted">{item.memberCount} عضو</span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
