import { useEffect, useMemo, useState } from 'react'
import Fuse from 'fuse.js'
import { useNavigate } from 'react-router-dom'
import { useCollection } from '../hooks/useCollection'
import { getSubjectById } from '../data/subjects'
import { toggleTheme } from '../utils/theme'
import './AppCommandPalette.css'

const PAGES = [
  { id: 'page-home', label: 'خانه', to: '/', group: 'صفحه' },
  { id: 'page-timer', label: 'زمان‌سنج', to: '/timer', group: 'صفحه' },
  { id: 'page-tests', label: 'آزمون‌ها', to: '/tests', group: 'صفحه' },
  { id: 'page-mistakes', label: 'اشتباهات', to: '/mistakes', group: 'صفحه' },
  { id: 'page-analytics', label: 'آمار', to: '/analytics', group: 'صفحه' },
  { id: 'page-tasks', label: 'کارها', to: '/tasks', group: 'صفحه' },
  { id: 'page-planner', label: 'برنامه', to: '/planner', group: 'صفحه' },
  { id: 'page-topics', label: 'مباحث', to: '/topics', group: 'صفحه' },
  { id: 'page-notes', label: 'یادداشت‌ها', to: '/notes', group: 'صفحه' },
  { id: 'page-settings', label: 'تنظیمات', to: '/settings', group: 'صفحه' },
  { id: 'page-settings-goals', label: 'هدف مطالعه', to: '/settings#daily-goal', group: 'صفحه' },
  { id: 'page-settings-timer', label: 'تنظیم زمان‌سنج', to: '/settings#timer-lengths', group: 'صفحه' },
]

export default function AppCommandPalette({ open, onClose }) {
  const navigate = useNavigate()
  const tasks = useCollection('tasks')
  const topics = useCollection('topics')
  const tests = useCollection('tests')
  const [query, setQuery] = useState('')

  useEffect(() => {
    if (!open) setQuery('')
  }, [open])

  const items = useMemo(() => {
    const list = [
      ...PAGES.map((page) => ({
        ...page,
        run: () => navigate(page.to),
      })),
      {
        id: 'action-theme',
        label: 'عوض کردن ظاهر',
        group: 'میان‌بر',
        run: () => toggleTheme(),
      },
      {
        id: 'action-notes-search',
        label: 'جستجوی یادداشت‌ها',
        group: 'میان‌بر',
        run: () => navigate('/notes'),
      },
      ...tasks.slice(0, 80).map((task) => ({
        id: `task:${task.id}`,
        label: task.title || 'کار بدون عنوان',
        group: 'کار',
        hint: task.done ? 'انجام‌شده' : 'باز',
        run: () => navigate('/tasks'),
      })),
      ...topics.slice(0, 80).map((topic) => ({
        id: `topic:${topic.id}`,
        label: topic.name || topic.title || 'مبحث',
        group: 'مبحث',
        hint: getSubjectById(topic.subject)?.name || '',
        run: () => navigate('/topics'),
      })),
      ...tests.slice(0, 80).map((test) => ({
        id: `test:${test.id}`,
        label: `${getSubjectById(test.subject)?.name || test.subject} · ${test.topic || 'بدون مبحث'}`,
        group: 'آزمون',
        hint: test.date || '',
        run: () => navigate('/tests'),
      })),
    ]
    return list
  }, [navigate, tasks, topics, tests])

  const fuse = useMemo(
    () => new Fuse(items, { keys: ['label', 'group', 'hint'], threshold: 0.38, ignoreLocation: true }),
    [items],
  )
  const results = query.trim() ? fuse.search(query).map((row) => row.item) : items.slice(0, 18)

  if (!open) return null

  return (
    <div className="app-cmd-backdrop" onClick={onClose}>
      <div
        className="app-cmd-modal"
        role="dialog"
        aria-label="جستجو در همه بخش‌ها"
        onClick={(event) => event.stopPropagation()}
      >
        <input
          autoFocus
          dir="auto"
          value={query}
          placeholder="صفحه، کار، مبحث، آزمون یا تنظیمات…"
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') onClose()
            if (event.key === 'Enter' && results[0]) {
              results[0].run()
              onClose()
            }
          }}
        />
        <ul className="app-cmd-list">
          {results.slice(0, 24).map((item) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => {
                  item.run()
                  onClose()
                }}
              >
                <span className="app-cmd-group">{item.group}</span>
                <strong dir="auto">{item.label}</strong>
                {item.hint ? <span className="app-cmd-hint">{item.hint}</span> : null}
              </button>
            </li>
          ))}
          {results.length === 0 ? <li className="app-cmd-empty">چیزی پیدا نشد.</li> : null}
        </ul>
        <p className="app-cmd-hint-line">Ctrl/⌘ + K</p>
      </div>
    </div>
  )
}
