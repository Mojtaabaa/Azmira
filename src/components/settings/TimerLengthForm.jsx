import { useState } from 'react'
import { useSettings } from '../../hooks/useSettings'
import { updateSettings } from '../../utils/storage'

const FIELDS = [
  { key: 'pomodoroWork', label: 'تمرکز' },
  { key: 'pomodoroBreak', label: 'استراحت' },
  { key: 'longBreak', label: 'استراحت بلند' },
]

export default function TimerLengthForm() {
  const settings = useSettings()
  const [drafts, setDrafts] = useState({})
  const [error, setError] = useState('')

  function commit(key, text) {
    const value = Number(text)
    setDrafts((current) => {
      const next = { ...current }
      delete next[key]
      return next
    })
    if (!Number.isInteger(value) || value < 1 || value > 180) {
      setError('هر زمان باید یک عدد از 1 تا 180 دقیقه باشد.')
      return
    }
    if (value !== settings[key]) updateSettings({ [key]: value })
    setError('')
  }

  return (
    <section className="card" id="timer-lengths">
      <h2>زمان‌سنج</h2>
      <p className="card-copy">اگر زمان‌سنج در حال کار باشد، عدد جدید از دور بعد اعمال می‌شود.</p>
      <form className="length-form" noValidate onSubmit={(event) => event.preventDefault()}>
        {FIELDS.map((field) => (
          <label key={field.key}>
            <span>{field.label}</span>
            <input
              type="number"
              inputMode="numeric"
              min="1"
              max="180"
              aria-label={field.label}
              value={drafts[field.key] ?? settings[field.key]}
              onChange={(event) => {
                const text = event.target.value
                setDrafts((current) => ({ ...current, [field.key]: text }))
                const value = Number(text)
                if (!Number.isInteger(value) || value < 1 || value > 180 || value === settings[field.key]) return
                updateSettings({ [field.key]: value })
                setError('')
              }}
              onBlur={(event) => commit(field.key, event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') event.currentTarget.blur()
              }}
            />
            <span>دقیقه</span>
          </label>
        ))}
      </form>
      {error ? (
        <p className="form-message is-error" role="alert">
          {error}
        </p>
      ) : null}
    </section>
  )
}
