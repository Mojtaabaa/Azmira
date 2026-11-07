// @ts-nocheck
import { useState } from 'react'
import SubjectWeeklyGoalsForm from './SubjectWeeklyGoalsForm'
import { useSettings } from '../../hooks/useSettings'
import { updateSettings } from '../../utils/storage'

const DAY_MAX = 24
const WEEK_MAX = 168
const STEP_HOURS = 0.25

function hoursToMinutes(text, max) {
  const value = Number(String(text).trim().replace(',', '.'))
  if (!Number.isFinite(value) || value < STEP_HOURS || value > max) return null
  const minutes = Math.round(value * 60)
  if (minutes < 15 || minutes > max * 60) return null
  if (minutes % 15 !== 0) return null
  return minutes
}

function displayHours(minutes) {
  const hours = (Number(minutes) || 0) / 60
  const rounded = Math.round(hours * 100) / 100
  return Number.isInteger(rounded) ? String(rounded) : String(rounded)
}

export default function DailyGoalForm() {
  const settings = useSettings()
  const [draft, setDraft] = useState(null)
  const [weekDraft, setWeekDraft] = useState(null)
  const [error, setError] = useState('')
  const shown = draft === null ? displayHours(settings.dailyGoalMinutes) : draft
  const weekShown = weekDraft === null ? displayHours(settings.weeklyGoalMinutes) : weekDraft

  function commit(text) {
    const minutes = hoursToMinutes(text, DAY_MAX)
    setDraft(null)
    if (minutes == null) {
      setError('هدف روزانه را مثل 4.5 بنویس (از ۰٫۲۵ تا ۲۴ ساعت).')
      return
    }
    if (minutes !== settings.dailyGoalMinutes) updateSettings({ dailyGoalMinutes: minutes })
    setError('')
  }

  function commitWeek(text) {
    const minutes = hoursToMinutes(text, WEEK_MAX)
    setWeekDraft(null)
    if (minutes == null) {
      setError('هدف هفتگی را مثل 28.5 بنویس (از ۰٫۲۵ تا ۱۶۸ ساعت).')
      return
    }
    if (minutes !== settings.weeklyGoalMinutes) updateSettings({ weeklyGoalMinutes: minutes })
    setError('')
  }

  return (
    <section className="card" id="daily-goal">
      <h2>هدف مطالعه</h2>
      <p className="card-copy">می‌توانی عدد اعشاری بزنی؛ مثلاً ۴٫۵ برای چهار و نیم ساعت. گام‌ها ۱۵ دقیقه‌ای‌اند.</p>
      <form className="length-form" noValidate onSubmit={(event) => event.preventDefault()}>
        <label>
          <span>هر روز</span>
          <input
            type="number"
            inputMode="decimal"
            min={STEP_HOURS}
            max={DAY_MAX}
            step={STEP_HOURS}
            aria-label="هدف روزانه"
            value={shown}
            onChange={(event) => {
              const text = event.target.value
              setDraft(text)
              const minutes = hoursToMinutes(text, DAY_MAX)
              if (minutes == null) return
              updateSettings({ dailyGoalMinutes: minutes })
              setError('')
            }}
            onBlur={(event) => commit(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') event.currentTarget.blur()
            }}
          />
          <span>ساعت</span>
        </label>
        <label id="weekly-goal">
          <span>هر هفته</span>
          <input
            type="number"
            inputMode="decimal"
            min={STEP_HOURS}
            max={WEEK_MAX}
            step={STEP_HOURS}
            aria-label="هدف هفتگی"
            value={weekShown}
            onChange={(event) => {
              const text = event.target.value
              setWeekDraft(text)
              const minutes = hoursToMinutes(text, WEEK_MAX)
              if (minutes == null) return
              updateSettings({ weeklyGoalMinutes: minutes })
              setError('')
            }}
            onBlur={(event) => commitWeek(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') event.currentTarget.blur()
            }}
          />
          <span>ساعت</span>
        </label>
      </form>
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
      <SubjectWeeklyGoalsForm embedded />
    </section>
  )
}
