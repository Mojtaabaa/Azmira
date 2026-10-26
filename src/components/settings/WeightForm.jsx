import { useState } from 'react'
import { listSubjects } from '../../data/subjects'
import { useSettings } from '../../hooks/useSettings'
import { subjectWeight } from '../../utils/dashboard'
import { updateSettings } from '../../utils/storage'

export default function WeightForm() {
  const settings = useSettings()
  const subjects = listSubjects()
  const [drafts, setDrafts] = useState({})
  const [error, setError] = useState('')

  function commit(id, text) {
    const value = Number(text)
    setDrafts((current) => {
      const next = { ...current }
      delete next[id]
      return next
    })
    if (!Number.isInteger(value) || value < 0 || value > 100) {
      setError('وزن هر درس باید یک عدد از 0 تا 100 باشد.')
      return
    }
    if (value === subjectWeight(settings.subjectWeights, id) && settings.subjectWeights?.[id] === value) return
    updateSettings({ subjectWeights: { ...settings.subjectWeights, [id]: value } })
    setError('')
  }

  return (
    <section className="card" id="subject-weights">
      <h2>وزن درس‌ها</h2>
      <p className="card-copy">برآورد درصد، میانگین آزمون هر درس را در این وزن ضرب می‌کند. وزن‌ها نسبت هستند و لازم نیست جمعشان 100 شود. صفر یعنی آن درس در برآورد نیاید.</p>
      <form className="length-form" noValidate onSubmit={(event) => event.preventDefault()}>
        {subjects.map((subject) => (
          <label key={subject.id}>
            <span>{subject.name}</span>
            <input
              type="number"
              inputMode="numeric"
              min="0"
              max="100"
              dir="ltr"
              aria-label={`وزن ${subject.name}`}
              value={drafts[subject.id] ?? subjectWeight(settings.subjectWeights, subject.id)}
              onChange={(event) => {
                const text = event.target.value
                setDrafts((current) => ({ ...current, [subject.id]: text }))
                const value = Number(text)
                if (!Number.isInteger(value) || value < 0 || value > 100) return
                updateSettings({ subjectWeights: { ...settings.subjectWeights, [subject.id]: value } })
                setError('')
              }}
              onBlur={(event) => commit(subject.id, event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') event.currentTarget.blur()
              }}
            />
            <span>وزن</span>
          </label>
        ))}
      </form>
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
    </section>
  )
}
