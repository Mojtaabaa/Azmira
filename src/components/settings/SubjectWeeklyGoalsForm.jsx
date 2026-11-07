import { useState } from 'react'
import { listSubjects } from '../../data/subjects'
import { useSettings } from '../../hooks/useSettings'
import { updateSettings } from '../../utils/storage'

const WEEK_MAX = 168
const STEP_HOURS = 0.25

function hoursToMinutes(text) {
  if (text === '' || text == null) return 0
  const value = Number(String(text).trim().replace(',', '.'))
  if (!Number.isFinite(value) || value < 0 || value > WEEK_MAX) return null
  if (value === 0) return 0
  if (value < STEP_HOURS) return null
  const minutes = Math.round(value * 60)
  if (minutes < 15 || minutes > WEEK_MAX * 60 || minutes % 15 !== 0) return null
  return minutes
}

function displayHours(minutes) {
  const value = Number(minutes) || 0
  if (value <= 0) return ''
  const hours = value / 60
  const rounded = Math.round(hours * 100) / 100
  return String(rounded)
}

export default function SubjectWeeklyGoalsForm({ embedded = false }) {
  const settings = useSettings()
  const subjects = listSubjects()
  const [drafts, setDrafts] = useState({})
  const [error, setError] = useState('')
  const goals = settings.subjectWeeklyGoals || {}

  function commit(id, text) {
    const minutes = hoursToMinutes(text)
    setDrafts((current) => {
      const next = { ...current }
      delete next[id]
      return next
    })
    if (minutes == null) {
      setError('هدف هر درس را مثل 4.5 بنویس؛ خالی یا صفر یعنی بدون هدف.')
      return
    }
    const nextGoals = { ...goals }
    if (minutes === 0) delete nextGoals[id]
    else nextGoals[id] = minutes
    updateSettings({ subjectWeeklyGoals: nextGoals })
    setError('')
  }

  const body = (
    <>
      {embedded ? (
        <>
          <h3 className="card-subheading" id="subject-weekly-goals">هدف هفتگی هر درس</h3>
          <p className="card-copy">
            برای هر درس جداگانه هدف بگذار. زمان‌سنج اگر درسی کم مانده یا از هدفش گذشته باشد، موقع شروع بهت می‌گوید.
            خالی یا صفر = بدون هدف برای آن درس.
          </p>
        </>
      ) : (
        <>
          <h2>هدف هفتگی هر درس</h2>
          <p className="card-copy">
            برای هر درس یک هدف هفتگی بگذار. اگر از هدف یک درس عقب باشی یا از آن رد شده باشی، زمان‌سنج موقع مطالعه بهت می‌گوید.
            خالی یا صفر یعنی برای آن درس هدفی نیست.
          </p>
        </>
      )}
      <form className="length-form" noValidate onSubmit={(event) => event.preventDefault()}>
        {subjects.map((subject) => {
          const saved = goals[subject.id] || 0
          const shown = drafts[subject.id] ?? displayHours(saved)
          return (
            <label key={subject.id}>
              <span>{subject.name}</span>
              <input
                type="number"
                inputMode="decimal"
                min="0"
                max={WEEK_MAX}
                step={STEP_HOURS}
                dir="ltr"
                placeholder="—"
                aria-label={`هدف هفتگی ${subject.name}`}
                value={shown}
                onChange={(event) => {
                  const text = event.target.value
                  setDrafts((current) => ({ ...current, [subject.id]: text }))
                  const minutes = hoursToMinutes(text)
                  if (minutes == null) return
                  const nextGoals = { ...(settings.subjectWeeklyGoals || {}) }
                  if (minutes === 0) delete nextGoals[subject.id]
                  else nextGoals[subject.id] = minutes
                  updateSettings({ subjectWeeklyGoals: nextGoals })
                  setError('')
                }}
                onBlur={(event) => commit(subject.id, event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') event.currentTarget.blur()
                }}
              />
              <span>ساعت در هفته</span>
            </label>
          )
        })}
      </form>
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
    </>
  )

  if (embedded) return <div className="subject-weekly-goals-embed">{body}</div>
  return (
    <section className="card" id="subject-weekly-goals">
      {body}
    </section>
  )
}
