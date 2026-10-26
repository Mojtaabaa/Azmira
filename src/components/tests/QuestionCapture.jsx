import { useRef, useState } from 'react'
import { ANSWER_CHOICES, parseWholeNumber } from '../../utils/testRecord'

const OPTIONS = [
  ...ANSWER_CHOICES.map((choice) => ({ id: choice, label: choice })),
  { id: 'blank', label: 'سفید' },
]

export default function QuestionCapture({ onAdd }) {
  const [number, setNumber] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const inputRef = useRef(null)

  function add(choice) {
    const parsed = parseWholeNumber(number)
    if (parsed == null || parsed < 1) {
      setNotice('')
      setError('اول شماره سوال را بنویس.')
      inputRef.current?.focus()
      return
    }

    const result = onAdd(parsed, choice)
    if (result?.error) {
      setNotice('')
      setError(result.error)
      return
    }

    setNumber('')
    setError('')
    setNotice(result?.updated ? `سوال ${parsed} عوض شد.` : `سوال ${parsed} اضافه شد.`)
    inputRef.current?.focus()
  }

  return (
    <div className="capture">
      <label>
        شماره سوال
        <input
          ref={inputRef}
          type="number"
          min="1"
          max="999"
          step="1"
          inputMode="numeric"
          value={number}
          onChange={(event) => {
            setNumber(event.target.value)
            setError('')
            setNotice('')
          }}
        />
      </label>
      <div className="choice-pad" role="group" aria-label="گزینه‌ای که زدی">
        {OPTIONS.map((option) => (
          <button key={option.id} type="button" className="choice-button" onClick={() => add(option.id)}>
            {option.label}
          </button>
        ))}
      </div>
      {error ? (
        <p className="form-message is-error" role="alert">
          {error}
        </p>
      ) : null}
      {notice ? (
        <p className="form-message is-ok" role="status">
          {notice}
        </p>
      ) : null}
    </div>
  )
}
