import { useState } from 'react'
import { listHiddenBuiltins, listSubjects } from '../../data/subjects'
import { useSettings } from '../../hooks/useSettings'
import {
  cleanSubjectName,
  removeSubject,
  renameCustomSubject,
  resolveSubjectName,
  restoreAllBuiltinSubjects,
  restoreBuiltinSubject,
} from '../../utils/subjects'

export default function SubjectNames() {
  useSettings()
  const subjects = listSubjects()
  const hidden = listHiddenBuiltins()
  const [name, setName] = useState('')
  const [error, setError] = useState('')
  const [confirming, setConfirming] = useState('')

  function addSubject(event) {
    event.preventDefault()
    const cleaned = cleanSubjectName(name)
    if (listSubjects({ includeHidden: true }).some((subject) => subject.name === cleaned)) {
      setError('این درس از قبل هست.')
      return
    }
    const result = resolveSubjectName(cleaned)
    if (result.error) {
      setError(result.error)
      return
    }
    setName('')
    setError('')
  }

  function rename(id, value) {
    const result = renameCustomSubject(id, value)
    setError(result.error || '')
  }

  function remove(id) {
    const result = removeSubject(id)
    if (result.error) setError(result.error)
    else {
      setError('')
      setConfirming('')
    }
  }

  return (
    <section className="card" id="subjects">
      <h2>درس‌ها</h2>
      <p className="card-copy">
        درس‌های پیش‌فرض را می‌توانی حذف کنی و فقط درس‌های خودت را نگه داری. حذف پیش‌فرض‌ها داده‌های قبلی آن درس را پاک نمی‌کند؛ فقط از فهرست انتخاب کنار می‌روند.
      </p>
      <form className="subject-names" noValidate onSubmit={addSubject}>
        <div className="subject-add">
          <label>
            نام درس
            <input
              dir="auto"
              value={name}
              maxLength={40}
              autoComplete="off"
              placeholder="مثلاً زیست"
              onChange={(event) => {
                setName(event.target.value)
                setError('')
              }}
            />
          </label>
          <button type="submit" className="button button-primary">افزودن</button>
        </div>
        {error ? (
          <p className="form-message is-error" role="alert">{error}</p>
        ) : null}
      </form>
      {subjects.length === 0 ? (
        <p className="card-copy">هنوز درسی در فهرست نیست. یکی اضافه کن.</p>
      ) : (
        <ul className="subject-list">
          {subjects.map((subject) => (
            <li key={subject.id} className="subject-row">
              {subject.custom ? (
                <input
                  dir="auto"
                  defaultValue={subject.name}
                  maxLength={40}
                  aria-label={`نام ${subject.name}`}
                  onBlur={(event) => rename(subject.id, event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') event.currentTarget.blur()
                  }}
                />
              ) : (
                <span className="subject-row-name" dir="auto">
                  {subject.name}
                  <small>پیش‌فرض</small>
                </span>
              )}
              {confirming === subject.id ? (
                <button
                  type="button"
                  className="button button-danger is-solid"
                  onClick={() => remove(subject.id)}
                >
                  تایید حذف
                </button>
              ) : (
                <button type="button" className="button button-danger" onClick={() => setConfirming(subject.id)}>
                  حذف
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {hidden.length > 0 ? (
        <div className="subject-hidden">
          <p className="card-copy">درس‌های پیش‌فرض پنهان‌شده:</p>
          <ul className="subject-list">
            {hidden.map((subject) => (
              <li key={subject.id} className="subject-row">
                <span className="subject-row-name" dir="auto">{subject.name}</span>
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    restoreBuiltinSubject(subject.id)
                    setError('')
                  }}
                >
                  بازگردانی
                </button>
              </li>
            ))}
          </ul>
          <div className="button-row">
            <button
              type="button"
              className="button"
              onClick={() => {
                restoreAllBuiltinSubjects()
                setError('')
              }}
            >
              بازگردانی همه پیش‌فرض‌ها
            </button>
          </div>
        </div>
      ) : null}
    </section>
  )
}
