// @ts-nocheck
import { useTheme } from '../../hooks/useTheme'

const OPTIONS = [
  { id: 'system', label: 'خودکار' },
  { id: 'light', label: 'روشن' },
  { id: 'dark', label: 'تیره' },
]

export default function ThemeSettings() {
  const { preference, theme, setPreference } = useTheme()
  const activeLabel = OPTIONS.find((option) => option.id === preference)?.label || 'خودکار'
  const resolvedLabel = theme === 'light' ? 'روشن' : 'تیره'

  return (
    <section className="card">
      <h2>ظاهر برنامه</h2>
      <p className="card-copy">
        انتخاب فعلی: {activeLabel}
        {preference === 'system' ? ` (الان: ${resolvedLabel})` : ''}.
        روی این دستگاه ذخیره می‌شود.
      </p>
      <div className="segment theme-preference" role="group" aria-label="ظاهر برنامه">
        {OPTIONS.map((option) => (
          <button
            key={option.id}
            type="button"
            className={preference === option.id ? 'is-active' : ''}
            onClick={() => setPreference(option.id)}
          >
            {option.label}
          </button>
        ))}
      </div>
    </section>
  )
}
