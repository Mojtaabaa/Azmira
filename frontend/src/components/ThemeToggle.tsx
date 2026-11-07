// @ts-nocheck
import { useTheme } from '../hooks/useTheme'

const LABELS = {
  system: 'خودکار',
  light: 'روشن',
  dark: 'تیره',
}

export default function ThemeToggle({ compact = false }) {
  const { preference, toggleTheme } = useTheme()
  const label = LABELS[preference] || LABELS.system

  return (
    <button
      type="button"
      className={compact ? 'theme-toggle is-compact' : 'theme-toggle'}
      onClick={toggleTheme}
      aria-label={`ظاهر برنامه: ${label}`}
      title={`ظاهر برنامه: ${label}`}
    >
      <span className="theme-toggle-icon" aria-hidden="true">
        {preference === 'light' ? (
          <svg viewBox="0 0 24 24" width="18" height="18">
            <path d="M12 3v2M12 19v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M3 12h2M19 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0z" />
          </svg>
        ) : preference === 'system' ? (
          <svg viewBox="0 0 24 24" width="18" height="18">
            <rect x="3" y="4" width="18" height="12" rx="2" />
            <path d="M8 20h8M12 16v4" />
          </svg>
        ) : (
          <svg viewBox="0 0 24 24" width="18" height="18">
            <path d="M20 13.5A7.5 7.5 0 1 1 10.5 4 6 6 0 0 0 20 13.5z" />
          </svg>
        )}
      </span>
      {compact ? null : <span>{label}</span>}
    </button>
  )
}
