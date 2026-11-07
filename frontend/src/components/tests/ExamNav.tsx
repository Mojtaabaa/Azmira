// @ts-nocheck
import { NavLink } from 'react-router-dom'

export default function ExamNav() {
  return (
    <nav className="exam-nav" aria-label="آزمون‌ها و اشتباهات">
      <NavLink to="/tests" end className={({ isActive }) => (isActive ? 'is-active' : undefined)}>
        آزمون‌ها
      </NavLink>
      <NavLink to="/mistakes" className={({ isActive }) => (isActive ? 'is-active' : undefined)}>
        اشتباهات
      </NavLink>
    </nav>
  )
}
