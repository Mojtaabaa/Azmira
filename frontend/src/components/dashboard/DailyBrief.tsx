// @ts-nocheck
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { loadVault } from '../../features/notes/lib/db'
import { useNotesStore } from '../../features/notes/store/notesStore'
import { formatMinutes } from '../../utils/clock'
import { getSubjectById } from '../../data/subjects'

export default function DailyBrief({ overdueTasks, studied, goal }) {
  const { user } = useAuth()
  const userId = user?.id || 'anon'
  const notes = useNotesStore((state) => state.notes)
  const loaded = useNotesStore((state) => state.loaded)
  const hydrate = useNotesStore((state) => state.hydrate)
  const [savedId, setSavedId] = useState(null)

  useEffect(() => {
    let alive = true
    ;(async () => {
      try {
        if (!loaded) await hydrate(userId)
        const vault = await loadVault(userId)
        if (!alive) return
        setSavedId(vault.settings?.activeNoteId || null)
      } catch {
        if (alive) setSavedId(null)
      }
    })()
    return () => {
      alive = false
    }
  }, [userId, loaded, hydrate])

  const resume = useMemo(() => {
    if (savedId) {
      const match = notes.find((note) => note.id === savedId)
      if (match) return match
    }
    return notes.slice().sort((a, b) => (b.updatedAt || 0) - (a.updatedAt || 0))[0] || null
  }, [notes, savedId])

  const remaining = Math.max(0, (Number(goal) || 0) - (Number(studied) || 0))
  const done = remaining === 0 && (Number(goal) || 0) > 0
  const snippet = firstLine(resume?.content || '')

  return (
    <section className="card dash-brief" aria-label="خلاصه امروز">
      <header className="dash-brief-head">
        <h2>امروز چه کار داری</h2>
        <p>از اینجا شروع کن: کار عقب‌افتاده، زمان باقی‌مانده، ادامهٔ یادداشت.</p>
      </header>
      <div className="dash-brief-grid">
        <article className="dash-brief-item">
          <span className="dash-brief-kicker">کار عقب‌افتاده</span>
          <strong dir="ltr">{overdueTasks.length}</strong>
          {overdueTasks.length === 0 ? (
            <p>کار عقب‌افتاده‌ای نیست.</p>
          ) : (
            <ul>
              {overdueTasks.slice(0, 3).map((task) => (
                <li key={task.id} dir="auto">
                  {task.title}
                  {task.subject && getSubjectById(task.subject)
                    ? ` · ${getSubjectById(task.subject).name}`
                    : ''}
                </li>
              ))}
            </ul>
          )}
          <Link className="text-link" to="/tasks?filter=overdue">کارها</Link>
        </article>
        <article className="dash-brief-item">
          <span className="dash-brief-kicker">زمان امروز</span>
          <strong>{done ? 'به هدف رسیدی' : formatMinutes(remaining)}</strong>
          <p>
            {done
              ? `${formatMinutes(studied)} مطالعه کردی.`
              : `${formatMinutes(studied)} از ${formatMinutes(goal)}؛ بقیه را با زمان‌سنج جبران کن.`}
          </p>
          <Link className="text-link" to="/timer">زمان‌سنج</Link>
        </article>
        <article className="dash-brief-item">
          <span className="dash-brief-kicker">ادامه یادداشت</span>
          {resume ? (
            <>
              <strong dir="auto">{resume.title}</strong>
              <p dir="auto">{snippet || 'هنوز چیزی ننوشته‌ای — از اینجا ادامه بده.'}</p>
              <Link className="text-link" to={`/notes?n=${encodeURIComponent(resume.id)}`}>ادامه بده</Link>
            </>
          ) : (
            <>
              <strong>یادداشت امروز</strong>
              <p>هنوز یادداشتی نیست. یک صفحه برای امروز باز کن.</p>
              <Link className="text-link" to="/notes?daily=1">یادداشت امروز</Link>
            </>
          )}
        </article>
      </div>
    </section>
  )
}

function firstLine(content) {
  return content
    .replace(/^---[\s\S]*?---\s*/, '')
    .split('\n')
    .map((line) => line.replace(/^#+\s*/, '').trim())
    .find((line) => line && !line.startsWith('![')) || ''
}
