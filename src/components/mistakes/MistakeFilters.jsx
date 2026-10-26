import { CAUSES } from '../../data/causes'
import { listSubjects } from '../../data/subjects'
import { useSettings } from '../../hooks/useSettings'

export default function MistakeFilters({ filters, topics, onChange }) {
  useSettings()
  const subjects = listSubjects()

  return (
    <div className="card filter-row">
      <label>
        درس
        <select
          value={filters.subject}
          onChange={(event) => onChange({ subject: event.target.value, topic: '' })}
        >
          <option value="">همه</option>
          {subjects.map((subject) => (
            <option key={subject.id} value={subject.id}>
              {subject.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        مبحث
        <select value={filters.topic} onChange={(event) => onChange({ topic: event.target.value })}>
          <option value="">همه</option>
          {topics.map((topic) => (
            <option key={topic} value={topic}>
              {topic}
            </option>
          ))}
        </select>
      </label>
      <label>
        علت
        <select value={filters.cause} onChange={(event) => onChange({ cause: event.target.value })}>
          <option value="">همه</option>
          {CAUSES.map((cause) => (
            <option key={cause.id} value={cause.id}>
              {cause.label}
            </option>
          ))}
        </select>
      </label>
      <label>
        وضعیت مرور
        <select value={filters.reviewed} onChange={(event) => onChange({ reviewed: event.target.value })}>
          <option value="all">همه</option>
          <option value="unreviewed">مرورنشده</option>
          <option value="reviewed">مرورشده</option>
        </select>
      </label>
    </div>
  )
}
