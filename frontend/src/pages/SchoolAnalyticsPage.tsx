// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function SchoolAnalyticsPage() {
  const { status, workspaces, activeWorkspaceId } = useAuth()
  const [data, setData] = useState(null)
  const [classes, setClasses] = useState([])
  const [classId, setClassId] = useState('')
  const [subject, setSubject] = useState('')
  const [error, setError] = useState('')

  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const isSchool = active?.type === 'school'

  const load = useCallback(async () => {
    if (!isSchool) return
    try {
      setError('')
      const classPayload = await request('GET', '/schools/classes')
      setClasses(classPayload.items || [])
      const qs = new URLSearchParams()
      if (classId) qs.set('classId', classId)
      if (subject) qs.set('subject', subject)
      const payload = await request('GET', `/schools/analytics?${qs.toString()}`)
      setData(payload)
    } catch (err) {
      setError(err.message || 'بارگذاری تحلیل ممکن نشد.')
    }
  }, [isSchool, classId, subject])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  if (!isSchool) {
    return (
      <div className="school-page">
        <h1>تحلیل مدرسه</h1>
        <p>فقط در فضای مدرسه.</p>
      </div>
    )
  }

  return (
    <div className="school-page">
      <header className="school-hero">
        <div>
          <p className="school-kicker">تحلیل</p>
          <h1>داشبورد مدرسه</h1>
          <p className="school-sub">فیلتر سال/پایه/کلاس/موضوع و بازه زمانی.</p>
        </div>
      </header>
      {error ? <p className="school-error">{error}</p> : null}

      <section className="school-panel">
        <div className="school-row">
          <select value={classId} onChange={(e) => setClassId(e.target.value)}>
            <option value="">همه کلاس‌ها</option>
            {classes.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
          <input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="موضوع (اختیاری)" />
          <button type="button" onClick={load}>
            اعمال فیلتر
          </button>
        </div>
      </section>

      {data ? (
        <section className="school-panel">
          <h2>شاخص‌ها</h2>
          <div className="school-stats">
            <div>
              <strong>{data.studentActivity?.activeStudents ?? 0}</strong>
              <span>دانش‌آموز</span>
            </div>
            <div>
              <strong>{data.studentActivity?.engagementRate ?? 0}%</strong>
              <span>درگیری</span>
            </div>
            <div>
              <strong>{data.assessmentPerformance?.averagePercent ?? 0}</strong>
              <span>میانگین آزمون</span>
            </div>
            <div>
              <strong>{data.goalCompletion?.hits ?? 0}</strong>
              <span>اهداف</span>
            </div>
          </div>
          <pre className="school-facts">{JSON.stringify(data, null, 2)}</pre>
        </section>
      ) : null}
    </div>
  )
}
