// @ts-nocheck
export default function JalaliToggle({ on, onChange }) {
  return (
    <button
      type="button"
      className={on ? 'button jalali-toggle is-on' : 'button jalali-toggle'}
      aria-pressed={on}
      onClick={() => onChange(!on)}
    >
      تاریخ شمسی
    </button>
  )
}
