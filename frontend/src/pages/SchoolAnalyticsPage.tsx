// @ts-nocheck
import { useCallback, useEffect, useMemo, useState } from 'react'
import CapabilityGate from '../components/CapabilityGate'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function SchoolAnalyticsPage() {
  return (
    <CapabilityGate require="schoolAnalytics" title="تحلیل مدرسه">
      <SchoolAnalyticsInner />
    </CapabilityGate>
  )
}

function SchoolAnalyticsInner() {
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

  const subjectBars = useMemo(() => {
    const rows = data?.assessmentPerformance?.bySubject || []
    const max = Math.max(1, ...rows.map((item) => Number(item.averagePercent) || 0))
    return rows.map((item) => ({
      ...item,
      width: `${Math.round((100 * (Number(item.averagePercent) || 0)) / max)}%`,
    }))
  }, [data])

  if (!isSchool) {
    return (
      <div className="school-page">
        <h1>تحلیل مدرسه</h1>
        <p className="muted">فقط در فضای مدرسه.</p>
      </div>
    )
  }

  return (
    <div className="school-page">
      <header className="school-hero">
        <div>
          <p className="school-kicker">تحلیل</p>
          <h1>داشبورد مدرسه</h1>
          <p className="school-sub">شاخص‌های فعالیت، آزمون و عملکرد کلاس‌ها.</p>
        </div>
      </header>
      {error ? <p className="school-error">{error}</p> : null}

      <section className="school-panel">
        <div className="school-row">
          <select value={classId} onChange={(e) => setClassId(e.target.value)}>
            <option value="">همه کلاس‌ها</option>
            {classes.map((item) => (
              <option key={item.id} value={item.id}>{item.name}</option>
            ))}
          </select>
          <input value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="موضوع (اختیاری)" />
          <button type="button" onClick={load}>اعمال فیلتر</button>
        </div>
      </section>

      {data ? (
        <>
          <section className="school-panel">
            <h2>KPI</h2>
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
            <div className="school-stats school-stats-secondary">
              <div>
                <strong>{data.studentActivity?.streakActive ?? 0}</strong>
                <span>استریک فعال</span>
              </div>
              <div>
                <strong>{data.studyHoursProxy?.totalXp ?? 0}</strong>
                <span>XP بازه</span>
              </div>
              <div>
                <strong>{data.assessmentPerformance?.attemptCount ?? 0}</strong>
                <span>تلاش آزمون</span>
              </div>
            </div>
          </section>

          <section className="school-panel">
            <h2>میانگین آزمون بر اساس موضوع</h2>
            {subjectBars.length === 0 ? <p className="muted">داده‌ای نیست.</p> : null}
            <div className="school-bars">
              {subjectBars.map((item) => (
                <div className="school-bar-row" key={item.subject || 'none'}>
                  <span>{item.subject || '—'}</span>
                  <div className="school-bar-track"><div className="school-bar-fill" style={{ width: item.width }} /></div>
                  <strong>{item.averagePercent}%</strong>
                </div>
              ))}
            </div>
          </section>

          <section className="school-panel">
            <h2>عملکرد کلاس‌ها</h2>
            <div className="school-table-wrap">
              <table className="school-table">
                <thead>
                  <tr>
                    <th>کلاس</th>
                    <th>میانگین XP</th>
                    <th>میانگین آزمون</th>
                  </tr>
                </thead>
                <tbody>
                  {(data.classPerformance || []).map((item) => (
                    <tr key={item.classId}>
                      <td>{item.name}</td>
                      <td>{Math.round(item.avgXp || 0)}</td>
                      <td>{item.avgAssessmentPercent}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="school-panel">
            <h2>پایه‌ها</h2>
            <div className="school-table-wrap">
              <table className="school-table">
                <thead>
                  <tr>
                    <th>پایه</th>
                    <th>دانش‌آموز</th>
                    <th>میانگین XP</th>
                  </tr>
                </thead>
                <tbody>
                  {(data.gradePerformance || []).map((item) => (
                    <tr key={item.gradeId}>
                      <td>{item.name}</td>
                      <td>{item.students}</td>
                      <td>{Math.round(item.avgXp || 0)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      ) : null}
    </div>
  )
}
