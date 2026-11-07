// @ts-nocheck
import { useState } from 'react'
import { getSubjectById } from '../../data/subjects'
import { formatDateLabel, todayInputValue } from '../../utils/clock'
import { dueDay } from '../../utils/tasks'

export default function TaskCard({ task, overdue, editing, onToggle, onEdit, onDelete }) {
  const [confirming, setConfirming] = useState(false)
  const subject = task.subject ? getSubjectById(task.subject) : null
  const due = dueDay(task)
  const showDate = due && due !== todayInputValue()
  const className = ['todo-row', overdue ? 'is-overdue' : '', task.done ? 'is-done' : '', editing ? 'is-editing' : '']
    .filter(Boolean)
    .join(' ')

  return (
    <li className={className}>
      <button
        type="button"
        className="todo-check"
        aria-pressed={task.done}
        aria-label={task.done ? 'انجام نشده' : 'انجام شد'}
        onClick={() => onToggle(task)}
      >
        {task.done ? <CheckIcon /> : null}
      </button>
      <button type="button" className="todo-title" onClick={() => onEdit(task)}>
        {task.title}
      </button>
      <span className="todo-meta">
        {showDate ? <span className="todo-date">{formatDateLabel(due)}</span> : null}
        {subject ? <span>{subject.name}</span> : null}
      </span>
      {confirming ? (
        <button type="button" className="todo-delete is-solid" onClick={() => onDelete(task)}>
          تایید
        </button>
      ) : (
        <button type="button" className="todo-delete" aria-label="حذف" onClick={() => setConfirming(true)}>
          ×
        </button>
      )}
    </li>
  )
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 12.5 9.2 17 19 7" />
    </svg>
  )
}
