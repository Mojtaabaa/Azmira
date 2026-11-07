// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { resolveCapabilities } from '../utils/capabilities'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function SchoolAdminPage() {
  const { status, user, workspaces, activeWorkspaceId, switchWorkspace, retry, capabilities } = useAuth()
  const caps = resolveCapabilities(capabilities, workspaces, activeWorkspaceId, user)
  const [profile, setProfile] = useState(null)
  const [members, setMembers] = useState([])
  const [years, setYears] = useState([])
  const [grades, setGrades] = useState([])
  const [subjects, setSubjects] = useState([])
  const [classes, setClasses] = useState([])
  const [myRequests, setMyRequests] = useState([])
  const [schoolName, setSchoolName] = useState('')
  const [schoolCity, setSchoolCity] = useState('')
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
  const canAdmin = caps.active.canManageSchool || caps.canManageSchool

  const loadRequests = useCallback(async () => {
    try {
      const mine = await request('GET', '/schools/requests/mine')
      setMyRequests(mine.items || [])
    } catch {
      setMyRequests([])
    }
  }, [])

  const load = useCallback(async () => {
    if (!isSchool) {
      await loadRequests()
      return
    }
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
  }, [isSchool, canAdmin, loadRequests])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function submitRequest() {
    if (!schoolName.trim()) return
    setBusy(true)
    try {
      setMessage('')
      setError('')
      await request('POST', '/schools/requests', {
        name: schoolName.trim(),
        city: schoolCity.trim() || undefined,
      })
      setSchoolName('')
      setSchoolCity('')
      setMessage('درخواست ثبت شد و منتظر تأیید ادمین سامانه است.')
      await loadRequests()
    } catch (err) {
      setError(err.message || 'ثبت درخواست ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  if (!isSchool) {
    const schoolWorkspaces = (workspaces || []).filter((item) => item.type === 'school')
    return (
      <div className="school-page">
        <header className="school-hero">
          <div>
            <p className="school-kicker">نسخه مدرسه</p>
            <h1>درخواست مدرسه</h1>
            <p className="school-sub">کاربر عادی مدرسه نمی‌سازد؛ درخواست می‌دهد و ادمین سامانه تأیید می‌کند.</p>
          </div>
        </header>
        {error ? <p className="school-error">{error}</p> : null}
        {message ? <p className="school-ok">{message}</p> : null}

        {schoolWorkspaces.length > 0 ? (
          <section className="school-panel">
            <h2>مدارس شما</h2>
            <ul className="school-list">
              {schoolWorkspaces.map((item) => (
                <li key={item.id}>
                  <strong>{item.name}</strong>
                  <button type="button" onClick={() => switchWorkspace?.(item.id)}>فعال‌سازی</button>
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <section className="school-panel">
          <h2>ثبت درخواست جدید</h2>
          <div className="school-row">
            <input value={schoolName} onChange={(e) => setSchoolName(e.target.value)} placeholder="نام مدرسه" />
            <input value={schoolCity} onChange={(e) => setSchoolCity(e.target.value)} placeholder="شهر" />
            <button type="button" disabled={busy} onClick={submitRequest}>
              ارسال درخواست
            </button>
          </div>
        </section>

        <section className="school-panel">
          <h2>وضعیت درخواست‌ها</h2>
          {myRequests.length === 0 ? <p className="muted">هنوز درخواستی ثبت نکرده‌ای.</p> : null}
          <ul className="school-list">
            {myRequests.map((item) => (
              <li key={item.id}>
                <div>
                  <strong>{item.name}</strong>
                  <span className="muted">{item.city || '—'} · {item.status}</span>
                </div>
                {item.status === 'Approved' && item.createdWorkspaceId ? (
                  <button
                    type="button"
                    onClick={async () => {
                      if (retry) await retry()
                      await switchWorkspace?.(item.createdWorkspaceId)
                    }}
                  >
                    ورود به مدرسه
                  </button>
                ) : null}
              </li>
            ))}
          </ul>
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

      <section className="school-panel">
        <h2>خلاصه</h2>
        <div className="school-stats">
          <div><strong>{members.length || '—'}</strong><span>اعضا</span></div>
          <div><strong>{classes.length}</strong><span>کلاس</span></div>
          <div><strong>{grades.length}</strong><span>پایه</span></div>
          <div><strong>{subjects.length}</strong><span>درس</span></div>
        </div>
      </section>

      {canAdmin ? (
        <section className="school-panel">
          <h2>اعضا و نقش‌ها</h2>
          <div className="school-row">
            <input value={inviteUserId} onChange={(e) => setInviteUserId(e.target.value)} placeholder="شناسه کاربر (userId)" />
            <select value={inviteRole} onChange={(e) => setInviteRole(e.target.value)}>
              {['Student', 'Parent', 'Teacher', 'AcademicManager', 'SchoolAdministrator'].map((role) => (
                <option key={role} value={role}>{role}</option>
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
                  setMessage('نقش عضو به‌روز شد.')
                  await load()
                } catch (err) {
                  setError(err.message)
                } finally {
                  setBusy(false)
                }
              }}
            >
              دعوت / تغییر نقش
            </button>
          </div>
          <div className="school-table-wrap">
            <table className="school-table">
              <thead>
                <tr>
                  <th>کاربر</th>
                  <th>نقش</th>
                  <th>عضویت</th>
                </tr>
              </thead>
              <tbody>
                {members.map((item) => (
                  <tr key={item.userId}>
                    <td>{item.displayName}</td>
                    <td>{item.role}</td>
                    <td className="muted">{item.joinedAt ? new Date(item.joinedAt).toLocaleDateString('fa-IR') : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : (
        <section className="school-panel">
          <p className="muted">مدیریت اعضا فقط برای مالک / ادمین مدرسه است.</p>
        </section>
      )}

      <section className="school-panel">
        <h2>ساختار</h2>
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
        <div className="school-table-wrap">
          <table className="school-table">
            <thead>
              <tr>
                <th>کلاس</th>
                <th>اعضا</th>
              </tr>
            </thead>
            <tbody>
              {classes.map((item) => (
                <tr key={item.id}>
                  <td>{item.name}</td>
                  <td>{item.memberCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
