import { useState } from 'react'
import TaskFilters from '../components/tasks/TaskFilters'
import TaskForm from '../components/tasks/TaskForm'
import TaskList from '../components/tasks/TaskList'
import { useCollection } from '../hooks/useCollection'
import { todayInputValue } from '../utils/clock'
import { addOne, deleteOne, updateOne } from '../utils/storage'
import { buildTask, countTaskBuckets, taskBucket, tasksForFilter } from '../utils/tasks'
import './TasksPage.css'

const EMPTY_FORM = {
  title: '',
  subject: '',
  dueDate: '',
}

const EMPTY_TEXT = {
  today: 'برای امروز کاری نیست.',
  upcoming: 'کار آینده‌ای نیست.',
  overdue: 'کار عقب‌افتاده‌ای نیست.',
  done: 'کار انجام‌شده‌ای نیست.',
}

const VIEW_TITLE = {
  today: 'امروز',
  upcoming: 'آینده',
  overdue: 'عقب‌افتاده',
  done: 'انجام‌شده',
}

export default function TasksPage() {
  const tasks = useCollection('tasks')
  const [form, setForm] = useState(EMPTY_FORM)
  const [editingId, setEditingId] = useState(null)
  const [filter, setFilter] = useState('today')
  const [error, setError] = useState('')
  const today = todayInputValue()
  const counts = countTaskBuckets(tasks, today)
  const visible = tasksForFilter(tasks, filter, today)

  function changeForm(field, value) {
    setForm((current) => ({ ...current, [field]: value }))
    setError('')
  }

  function cancelEdit() {
    setEditingId(null)
    setForm(EMPTY_FORM)
    setError('')
  }

  function handleSubmit(event) {
    event.preventDefault()
    const result = buildTask(form)
    if (result.error) {
      setError(result.error)
      return
    }

    const current = editingId ? tasks.find((task) => task.id === editingId) : null
    if (editingId && !current) {
      cancelEdit()
      setError('این کار دیگر در فهرست نیست.')
      return
    }

    try {
      if (current) {
        const saved = updateOne('tasks', current.id, result.task)
        if (!saved) {
          cancelEdit()
          setError('این کار دیگر در فهرست نیست.')
          return
        }
      } else {
        addOne('tasks', {
          ...result.task,
          done: false,
          createdAt: new Date().toISOString(),
        })
      }
    } catch (saveError) {
      setError(saveError.message || 'این کار ذخیره نشد.')
      return
    }

    setFilter(taskBucket({ ...result.task, done: current ? current.done : false }, today))
    setEditingId(null)
    setForm(EMPTY_FORM)
    setError('')
  }

  function toggleDone(task) {
    updateOne('tasks', task.id, { done: !task.done })
  }

  function startEdit(task) {
    setEditingId(task.id)
    setForm({
      title: task.title || '',
      subject: task.subject || '',
      dueDate: dueValue(task.dueDate),
    })
    setError('')
    document.getElementById('task-composer')?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  }

  function removeTask(task) {
    deleteOne('tasks', task.id)
    if (editingId === task.id) cancelEdit()
  }

  return (
    <div className="page todo-page">
      <header className="todo-head">
        <h1>{VIEW_TITLE[filter]}</h1>
        <p className="todo-count">
          <span className="todo-count-mark" aria-hidden="true" />
          {counts[filter]} کار
        </p>
      </header>
      <TaskFilters filter={filter} counts={counts} onChange={setFilter} />
      <TaskList
        tasks={visible}
        empty={EMPTY_TEXT[filter]}
        editingId={editingId}
        overdue={filter === 'overdue'}
        onToggle={toggleDone}
        onEdit={startEdit}
        onDelete={removeTask}
      />
      <TaskForm
        value={form}
        editing={Boolean(editingId)}
        error={error}
        onChange={changeForm}
        onSubmit={handleSubmit}
        onCancel={cancelEdit}
      />
    </div>
  )
}

function dueValue(value) {
  return typeof value === 'string' ? value : ''
}
