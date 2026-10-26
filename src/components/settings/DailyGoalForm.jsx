import { useState } from 'react'
import { useSettings } from '../../hooks/useSettings'
import { updateSettings } from '../../utils/storage'

const DAY_MAX = 24
const WEEK_MAX = 168

function wholeHours(text, max) {
  const value = Number(text)
  if (!Number.isInteger(value) || value < 1 || value > max) return null
  return value
}

export default function DailyGoalForm() {
  const settings = useSettings()
  const [draft, setDraft] = useState(null)
  const [weekDraft, setWeekDraft] = useState(null)
  const [error, setError] = useState('')
  const shown = draft === null ? settings.dailyGoalMinutes / 60 : draft
  const weekShown = weekDraft === null ? settings.weeklyGoalMinutes / 60 : weekDraft

  function commit(text) {
    const hours = wholeHours(text, DAY_MAX)
    setDraft(null)
    if (hours == null) {
      setError('هدف روزانه باید یک عدد از 1 تا 24 ساعت باشد.')
      return
    }
    const minutes = hours * 60
    if (minutes !== settings.dailyGoalMinutes) updateSettings({ dailyGoalMinutes: minutes })
    setError('')
  }

  function commitWeek(text) {
    const hours = wholeHours(text, WEEK_MAX)
    setWeekDraft(null)
    if (hours == null) {
      setError('هدف هفته باید یک عدد از 1 تا 168 ساعت باشد.')
      return
    }
    const minutes = hours * 60
    if (minutes !== settings.weeklyGoalMinutes) updateSettings({ weeklyGoalMinutes: minutes })
    setError('')
  }

  return (
    <section className="card" id="daily-goal">
      <h2>هدف مطالعه</h2>
      <p className="card-copy">پیشخوان امروز را با هدف روزانه و این هفته را با هدف هفتگی مقایسه می‌کند.</p>
      <form className="length-form" noValidate onSubmit={(event) => event.preventDefault()}>
        <label>
          <span>هر روز</span>
          <input
            type="number"
            inputMode="numeric"
            min="1"
            max={DAY_MAX}
            aria-label="هدف روزانه"
            value={shown}
            onChange={(event) => {
              const text = event.target.value
              setDraft(text)
              const hours = wholeHours(text, DAY_MAX)
              if (hours == null) return
              updateSettings({ dailyGoalMinutes: hours * 60 })
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
            inputMode="numeric"
            min="1"
            max={WEEK_MAX}
            aria-label="هدف هفته"
            value={weekShown}
            onChange={(event) => {
              const text = event.target.value
              setWeekDraft(text)
              const hours = wholeHours(text, WEEK_MAX)
              if (hours == null) return
              updateSettings({ weeklyGoalMinutes: hours * 60 })
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
    </section>
  )
}
