// @ts-nocheck
export default function TimerControls({ running, onStart, onPause, onReset, onSkip }) {
  return (
    <div className="timer-controls">
      <button
        type="button"
        className="is-main"
        aria-label={running ? 'توقف' : 'شروع'}
        onClick={running ? onPause : onStart}
      >
        {running ? <PauseIcon /> : <PlayIcon />}
      </button>
      <button type="button" aria-label="از نو" onClick={onReset}>
        <ResetIcon />
      </button>
      <button type="button" aria-label="رد کردن" onClick={onSkip}>
        <SkipIcon />
      </button>
    </div>
  )
}

function PlayIcon() {
  return (
    <svg className="icon-solid" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 2.5v19l16.5-9.5L5 2.5z" />
    </svg>
  )
}

function PauseIcon() {
  return (
    <svg className="icon-solid" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M7 5h3.2v14H7zM13.8 5H17v14h-3.2z" />
    </svg>
  )
}

function ResetIcon() {
  return (
    <svg className="icon-stroke" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M19.2 12a7.2 7.2 0 1 1-2.1-5.1" />
      <path d="M19.2 4.2V9h-4.8" />
    </svg>
  )
}

function SkipIcon() {
  return (
    <svg className="icon-solid" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M5 5.5v13l9-6.5-9-6.5z" />
      <path d="M16.5 5.5h2.2v13h-2.2z" />
    </svg>
  )
}
