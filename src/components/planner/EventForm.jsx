import { useEffect, useState } from 'react'
import DatePicker from '../DatePicker'
import SubjectField from '../SubjectField'
import { EVENT_TYPES } from '../../utils/events'

export default function EventForm({ value, editing, done, error, namePrompt, onChange, onSubmit, onCancel, onToggle, onDelete }) {
  const [confirming, setConfirming] = useState(false)

  useEffect(() => {
    const field = document.getElementById('event-title')
    field?.focus()
    if (namePrompt) field?.select()
  }, [namePrompt])

  return (
    <section className="card cal-sheet" id="event-form" onKeyDown={(event) => {
      if (event.key === 'Escape') onCancel()
    }}>
      <h2>{namePrompt ? 'اسم این مطالعه' : editing ? 'ویرایش رویداد' : 'رویداد تازه'}</h2>
      {namePrompt ? <p className="card-copy">اسم را به موضوع همین جلسه عوض کن.</p> : null}
      <form className="event-form" noValidate onSubmit={onSubmit}>
        <label className="span-2">
          عنوان
          <input
            id="event-title"
            dir="auto"
            value={value.title}
            maxLength={160}
            autoComplete="off"
            onChange={(event) => onChange('title', event.target.value)}
          />
        </label>
        <label>
          نوع
          <select value={value.type} onChange={(event) => onChange('type', event.target.value)}>
            <option value="">انتخاب</option>
            {EVENT_TYPES.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>
        <label>
          درس (اختیاری)
          <SubjectField optional value={value.subject} onChange={(id) => onChange('subject', id)} />
        </label>
        <label>
          تاریخ
          <DatePicker variant="field" value={value.date} onChange={(next) => onChange('date', next)} />
        </label>
        <label>
          شروع
          <input type="time" value={value.startTime} onChange={(event) => onChange('startTime', event.target.value)} />
        </label>
        <label>
          پایان
          <input type="time" value={value.endTime} onChange={(event) => onChange('endTime', event.target.value)} />
        </label>
        {error ? (
          <p className="form-message is-error span-2" role="alert">{error}</p>
        ) : null}
        <div className="button-row span-2">
          <button type="submit" className="button button-primary">
            {namePrompt ? 'ثبت مطالعه' : editing ? 'ذخیره تغییرات' : 'افزودن رویداد'}
          </button>
          {editing ? (
            <button type="button" className="button" aria-pressed={done} onClick={onToggle}>
              {done ? 'انجام نشده' : 'انجام شد'}
            </button>
          ) : null}
          {editing && confirming ? (
            <button type="button" className="button button-danger is-solid" onClick={onDelete}>تایید حذف</button>
          ) : null}
          {editing && !confirming ? (
            <button type="button" className="button button-danger" onClick={() => setConfirming(true)}>حذف</button>
          ) : null}
          <button type="button" className="button" onClick={onCancel}>بستن</button>
        </div>
      </form>
    </section>
  )
}
