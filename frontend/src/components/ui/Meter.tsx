// @ts-nocheck
import './Meter.css'

export default function Meter({
  value = 0,
  max = 100,
  label,
  tone = 'accent',
}) {
  const safeMax = max > 0 ? max : 100
  const clamped = Math.max(0, Math.min(safeMax, Number(value) || 0))
  const ratio = Math.round((clamped / safeMax) * 100)

  return (
    <div
      className={`ui-meter ui-meter--${tone}`}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={safeMax}
      aria-valuenow={clamped}
      aria-label={label || 'پیشرفت'}
    >
      <span style={{ width: `${ratio}%` }} />
    </div>
  )
}
