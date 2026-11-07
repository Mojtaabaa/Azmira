import DatePicker from '../DatePicker'
import SubjectPicker from './SubjectPicker'

export default function TaskForm({ value, editing, error, onChange, onSubmit, onCancel }) {
  return (
    <form id="task-composer" className="todo-composer" noValidate onSubmit={onSubmit}>
      {editing ? <p className="todo-composer-note">ویرایش کار</p> : null}
      <input
        className="todo-name"
        dir={value.title.trim() ? 'auto' : 'rtl'}
        value={value.title}
        maxLength={160}
        autoComplete="off"
        placeholder="نام کار"
        aria-label="نام کار"
        onChange={(event) => onChange('title', event.target.value)}
      />
      <div className="todo-tools">
        <DatePicker clearable value={value.dueDate} onChange={(next) => onChange('dueDate', next)} />
        <SubjectPicker value={value.subject} onChange={(next) => onChange('subject', next)} />
        <button type="button" className="todo-clear" aria-label="پاک کردن" onClick={onCancel}>
          ×
        </button>
        <button type="submit" className="todo-submit" aria-label={editing ? 'ذخیره' : 'افزودن کار'}>
          <SubmitIcon />
        </button>
      </div>
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
    </form>
  )
}

function SubmitIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 12.5 9.2 17 19 7" />
    </svg>
  )
}
