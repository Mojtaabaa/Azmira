// @ts-nocheck
import { formatClock } from '../../utils/clock'

export default function TimerClock({ remainingMs, progress }) {
  const width = `${Math.min(100, Math.max(0, progress * 100))}%`

  return (
    <div className="timer-face">
      <p className="timer-clock" aria-live="off">
        {formatClock(remainingMs)}
      </p>
      <div className="timer-progress" aria-hidden="true">
        <span style={{ width }} />
      </div>
    </div>
  )
}
