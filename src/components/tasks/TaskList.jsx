import TaskCard from './TaskCard'

export default function TaskList({ tasks, empty, editingId, overdue, onToggle, onEdit, onDelete }) {
  if (tasks.length === 0) {
    return <p className="todo-empty">{empty}</p>
  }

  return (
    <ul className="todo-list">
      {tasks.map((task) => (
        <TaskCard
          key={task.id}
          task={task}
          overdue={overdue}
          editing={editingId === task.id}
          onToggle={onToggle}
          onEdit={onEdit}
          onDelete={onDelete}
        />
      ))}
    </ul>
  )
}
