import ThemeToggle from '../ThemeToggle'
import { useTheme } from '../../hooks/useTheme'

export default function ThemeSettings() {
  const { theme } = useTheme()

  return (
    <section className="card">
      <h2>ظاهر برنامه</h2>
      <p className="card-copy">
        الان حالت {theme === 'light' ? 'روشن' : 'تیره'} است. هر وقت خواستی عوض کن؛ روی این دستگاه ذخیره می‌شود.
      </p>
      <div className="button-row">
        <ThemeToggle />
      </div>
    </section>
  )
}
