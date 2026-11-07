// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function TeacherPortalPage() {
  const { status, workspaces, activeWorkspaceId } = useAuth()
  const [classes, setClasses] = useState([])
  const [selectedClass, setSelectedClass] = useState(null)
  const [students, setStudents] = useState([])
  const [progress, setProgress] = useState(null)
  const [assignmentTitle, setAssignmentTitle] = useState('')
  const [missionTitle, setMissionTitle] = useState('')
  const [feedback, setFeedback] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const isSchool = active?.type === 'school'

  const load = useCallback(async () => {
    if (!isSchool) return
    try {
      setError('')
      const payload = await request('GET', '/teacher/classes')
      setClasses(payload.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری کلاس‌ها ممکن نشد.')
    }
  }, [isSchool])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function openClass(id) {
    setSelectedClass(id)
    setProgress(null)
    try {
      const payload = await request('GET', `/teacher/classes/${id}/students`)
      setStudents(payload.items || [])
    } catch (err) {
      setError(err.message)
    }
  }

  if (!isSchool) {
    return (
      <div className="school-page">
        <h1>پورتال معلم</h1>
        <p>برای دسترسی، به یک فضای مدرسه سوییچ کن.</p>
      </div>
    )
  }

  return (
    <div className="school-page">
      <header className="school-hero">
        <div>
          <p className="school-kicker">معلم</p>
          <h1>پورتال معلم</h1>
          <p className="school-sub">فقط کلاس‌ها و دانش‌آموزان مجاز.</p>
        </div>
      </header>
      {error ? <p className="school-error">{error}</p> : null}

      <section className="school-panel">
        <h2>کلاس‌های من</h2>
        <ul className="school-list">
          {classes.map((item) => (
            <li key={item.id}>
              <div>
                <strong>{item.name}</strong>
                <span className="muted">{item.studentCount} دانش‌آموز</span>
              </div>
              <button type="button" onClick={() => openClass(item.id)}>
                باز کردن
              </button>
            </li>
          ))}
        </ul>
      </section>

      {selectedClass ? (
        <>
          <section className="school-panel">
            <h2>دانش‌آموزان</h2>
            <ul className="school-list">
              {students.map((item) => (
                <li key={item.userId}>
                  <strong>{item.displayName}</strong>
                  <button
                    type="button"
                    onClick={async () => {
                      setProgress(await request('GET', `/teacher/students/${item.userId}/progress`))
                    }}
                  >
                    پیشرفت
                  </button>
                </li>
              ))}
            </ul>
            {progress ? (
              <pre className="school-facts">{JSON.stringify(progress, null, 2)}</pre>
            ) : null}
          </section>

          <section className="school-panel">
            <h2>تکلیف / ماموریت / بازخورد</h2>
            <div className="school-row">
              <input value={assignmentTitle} onChange={(e) => setAssignmentTitle(e.target.value)} placeholder="عنوان تکلیف" />
              <button
                type="button"
                disabled={busy}
                onClick={async () => {
                  setBusy(true)
                  try {
                    await request('POST', '/teacher/assignments', {
                      classId: selectedClass,
                      title: assignmentTitle,
                      publish: true,
                    })
                    setAssignmentTitle('')
                  } catch (err) {
                    setError(err.message)
                  } finally {
                    setBusy(false)
                  }
                }}
              >
                ساخت تکلیف
              </button>
            </div>
            <div className="school-row">
              <input value={missionTitle} onChange={(e) => setMissionTitle(e.target.value)} placeholder="ماموریت کلاسی" />
              <button
                type="button"
                disabled={busy}
                onClick={async () => {
                  setBusy(true)
                  try {
                    await request('POST', '/teacher/missions', {
                      classId: selectedClass,
                      title: missionTitle,
                      targetValue: 1,
                      rewardXp: 25,
                      rewardCoins: 5,
                    })
                    setMissionTitle('')
                  } catch (err) {
                    setError(err.message)
                  } finally {
                    setBusy(false)
                  }
                }}
              >
                ساخت ماموریت
              </button>
            </div>
            {students[0] ? (
              <div className="school-row">
                <input value={feedback} onChange={(e) => setFeedback(e.target.value)} placeholder="بازخورد برای اولین دانش‌آموز" />
                <button
                  type="button"
                  onClick={async () => {
                    await request('POST', '/teacher/feedback', {
                      classId: selectedClass,
                      studentUserId: students[0].userId,
                      body: feedback,
                    })
                    setFeedback('')
                  }}
                >
                  ارسال بازخورد
                </button>
              </div>
            ) : null}
          </section>
        </>
      ) : null}
    </div>
  )
}
