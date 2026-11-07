import { Link } from 'react-router-dom'
import { useSettings } from '../../hooks/useSettings'
import { useTimer } from '../../hooks/useTimer'
import { formatClock } from '../../utils/clock'
import { pauseTimer, startTimer } from '../../utils/timerStore'
import './MiniTimer.css'

const PHASE_LABELS = {
  focus: 'تمرکز',
  shortBreak: 'استراحت کوتاه',
  longBreak: 'استراحت طولانی',
}

export default function MiniTimer() {
  const timer = useTimer()
  useSettings()

  if (!timer.running && !timer.paused) return null

  const remainingMs = timer.running && typeof timer.endsAt === 'number'
    ? Math.max(0, timer.endsAt - timer.now)
    : timer.remainingMs
  const topic = timer.phase === 'focus' && timer.focusSession ? timer.focusSession.topic : ''

  return (
    <div className={timer.running ? 'mini-timer is-running' : 'mini-timer'}>
      <button
        type="button"
        className="mini-timer-toggle"
        aria-label={timer.running ? 'توقف' : 'ادامه'}
        onClick={timer.running ? pauseTimer : () => startTimer()}
      >
        {timer.running ? <PauseIcon /> : <PlayIcon />}
      </button>
      <Link className="mini-timer-body" to="/timer" aria-label="رفتن به زمان‌سنج">
        <span className="mini-timer-clock">{formatClock(remainingMs)}</span>
        <span className="mini-timer-label">{topic || PHASE_LABELS[timer.phase]}</span>
      </Link>
    </div>
  )
}

function PlayIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 2.5v19l16.5-9.5L5 2.5z" />
    </svg>
  )
}

function PauseIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M7 5h3.2v14H7zM13.8 5H17v14h-3.2z" />
    </svg>
  )
}
