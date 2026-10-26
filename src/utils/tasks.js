import { getSubjectById } from '../data/subjects'
import { isRealDateString } from './clock'

export const TASK_FILTERS = [
  { id: 'today', label: 'امروز' },
  { id: 'upcoming', label: 'آینده' },
  { id: 'overdue', label: 'عقب‌افتاده' },
  { id: 'done', label: 'انجام‌شده' },
]

const EMPTY_COUNTS = {
  today: 0,
  upcoming: 0,
  overdue: 0,
  done: 0,
}

export function dueDay(task) {
  return isRealDateString(task?.dueDate) ? task.dueDate : ''
}

export function taskBucket(task, today) {
  if (task?.done) return 'done'
  const due = dueDay(task)
  if (!due || due === today) return 'today'
  if (due < today) return 'overdue'
  return 'upcoming'
}

export function countTaskBuckets(tasks, today) {
  const counts = { ...EMPTY_COUNTS }
  for (const task of tasks) {
    counts[taskBucket(task, today)] += 1
  }
  return counts
}

export function tasksForFilter(tasks, filter, today) {
  const matched = tasks.filter((task) => taskBucket(task, today) === filter)
  if (filter === 'done') {
    return matched
      .slice()
      .sort((a, b) => String(b.createdAt).localeCompare(String(a.createdAt)))
  }
  return matched.slice().sort((a, b) => {
    const aDate = dueDay(a) || '9999-99-99'
    const bDate = dueDay(b) || '9999-99-99'
    if (aDate !== bDate) return aDate < bDate ? -1 : 1
    return String(a.createdAt).localeCompare(String(b.createdAt))
  })
}

export function buildTask(input) {
  const title = typeof input.title === 'string' ? input.title.trim() : ''
  const subject = typeof input.subject === 'string' ? input.subject.trim() : ''
  const dueDate = typeof input.dueDate === 'string' ? input.dueDate.trim() : ''

  if (!title) return { error: 'عنوان را بنویس.' }
  if (subject && !getSubjectById(subject)) return { error: 'درس را درست انتخاب کن.' }
  if (dueDate && !isRealDateString(dueDate)) return { error: 'یک تاریخ واقعی وارد کن.' }

  return {
    task: {
      title,
      subject,
      dueDate,
    },
  }
}
