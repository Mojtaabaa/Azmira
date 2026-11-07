// @ts-nocheck
import { useId, useState } from 'react'
import { getSubjectById, listSubjects } from '../data/subjects'
import { useSettings } from '../hooks/useSettings'
import { resolveSubjectName } from '../utils/subjects'

export default function SubjectField({
  value,
  onChange,
  optional = false,
  disabled = false,
  variant = 'field',
  placeholder,
}) {
  useSettings()
  const listId = useId()
  const [draft, setDraft] = useState(null)
  const [error, setError] = useState('')
  const savedName = getSubjectById(value)?.name || ''
  const text = draft === null ? savedName : draft
  const emptyLabel = placeholder || (optional ? 'بنویس یا انتخاب کن' : 'درس')

  function finish(raw) {
    const result = resolveSubjectName(raw, { optional })
    if (result.error) {
      setError(result.error)
      return
    }
    setError('')
    setDraft(null)
    if (result.id !== value) onChange(result.id)
  }

  return (
    <span className={variant === 'title' ? 'subject-field is-title' : 'subject-field'}>
      <input
        className={variant === 'title' ? (text ? 'title-select' : 'title-select is-empty') : undefined}
        name="subject-name"
        dir="auto"
        value={text}
        disabled={disabled}
        placeholder={emptyLabel}
        maxLength={40}
        autoComplete="off"
        list={listId}
        aria-label={variant === 'title' ? 'درس' : undefined}
        style={variant === 'title' ? { width: `${Math.max(text.length || emptyLabel.length, 4) * 0.95}em` } : undefined}
        onFocus={() => {
          setDraft(text)
          setError('')
        }}
        onChange={(event) => {
          const next = event.target.value
          setDraft(next)
          setError('')
          const known = listSubjects().find((subject) => subject.name === next.trim())
          if (known && known.id !== value) onChange(known.id)
          if (!next.trim() && optional && value) onChange('')
        }}
        onBlur={(event) => finish(event.target.value)}
      />
      <datalist id={listId}>
        {listSubjects().map((subject) => (
          <option key={subject.id} value={subject.name} />
        ))}
      </datalist>
      {error ? (
        <span className="form-message is-error" role="alert">{error}</span>
      ) : null}
    </span>
  )
}
