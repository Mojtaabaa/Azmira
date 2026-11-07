import { TASK_FILTERS } from '../../utils/tasks'

export default function TaskFilters({ filter, counts, onChange }) {
  return (
    <div className="todo-views" role="group" aria-label="فیلتر کارها">
      {TASK_FILTERS.map((item) => {
        const active = filter === item.id
        const overdue = item.id === 'overdue' && counts.overdue > 0 && !active
        const className = [active ? 'is-active' : '', overdue ? 'has-overdue' : ''].filter(Boolean).join(' ')
        return (
          <button
            key={item.id}
            type="button"
            className={className || undefined}
            aria-pressed={active}
            onClick={() => onChange(item.id)}
          >
            {item.label}
            {counts[item.id] > 0 ? <span className="todo-view-count">{counts[item.id]}</span> : null}
          </button>
        )
      })}
    </div>
  )
}
