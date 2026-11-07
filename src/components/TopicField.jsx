import { useId, useMemo } from 'react'
import { useCollection } from '../hooks/useCollection'
import { useSettings } from '../hooks/useSettings'
import { resolveTopicName, topicNamesForSubject } from '../utils/topics'

export default function TopicField({
  subject = '',
  value = '',
  onChange,
  disabled = false,
  variant = 'field',
  placeholder = 'مبحث',
  id,
}) {
  const listId = useId()
  const settings = useSettings()
  const topics = useCollection('topics')
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const hiddenTopics = settings.hiddenTopics || []
  const known = useMemo(
    () => topicNamesForSubject(subject, { topics, sessions, tests, hiddenTopics }),
    [subject, topics, sessions, tests, hiddenTopics],
  )
  const inputId = id || listId

  function commit(raw) {
    onChange?.(resolveTopicName(raw, known))
  }

  if (variant === 'bare') {
    return (
      <span className="topic-field is-bare">
        <input
          id={inputId}
          className="topic-input"
          aria-label={placeholder}
          value={value}
          disabled={disabled}
          dir="auto"
          placeholder={placeholder}
          maxLength={80}
          autoComplete="off"
          list={`${inputId}-list`}
          onChange={(event) => onChange?.(event.target.value)}
          onBlur={(event) => commit(event.target.value)}
        />
        <datalist id={`${inputId}-list`}>
          {known.map((name) => (
            <option key={name} value={name} />
          ))}
        </datalist>
      </span>
    )
  }

  return (
    <span className="topic-field">
      <input
        id={inputId}
        dir="auto"
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        maxLength={80}
        autoComplete="off"
        list={`${inputId}-list`}
        onChange={(event) => onChange?.(event.target.value)}
        onBlur={(event) => commit(event.target.value)}
      />
      <datalist id={`${inputId}-list`}>
        {known.map((name) => (
          <option key={name} value={name} />
        ))}
      </datalist>
    </span>
  )
}
