import { useState } from 'react'
import SubjectField from '../SubjectField'
import { subjectFromForm } from '../../utils/subjects'
import { buildTopic } from '../../utils/topics'

export default function TopicForm({ existing, onCreate }) {
  const [name, setName] = useState('')
  const [subject, setSubject] = useState('')
  const [error, setError] = useState('')

  function handleSubmit(event) {
    event.preventDefault()
    const resolved = subjectFromForm(event.currentTarget)
    if (resolved.error) {
      setError(resolved.error)
      return
    }
    const result = buildTopic({ name, subject: resolved.id, status: 'new' }, existing)
    if (result.error) {
      setError(result.error)
      return
    }
    onCreate(result.topic)
    setName('')
    setError('')
  }

  return (
    <form className="topic-form" noValidate onSubmit={handleSubmit}>
      <label>
        درس
        <SubjectField value={subject} onChange={setSubject} />
      </label>
      <label>
        مبحث
        <input
          dir="auto"
          value={name}
          maxLength={80}
          autoComplete="off"
          onChange={(event) => {
            setName(event.target.value)
            setError('')
          }}
        />
      </label>
      <button type="submit" className="button button-primary">افزودن مبحث</button>
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
    </form>
  )
}
