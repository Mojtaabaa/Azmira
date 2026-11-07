// @ts-nocheck
import { useState } from 'react'
import SubjectField from '../SubjectField'
import TopicField from '../TopicField'
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
        <TopicField
          subject={subject}
          value={name}
          onChange={(next) => {
            setName(next)
            setError('')
          }}
        />
      </label>
      <button type="submit" className="button button-primary">افزودن مبحث</button>
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
    </form>
  )
}
