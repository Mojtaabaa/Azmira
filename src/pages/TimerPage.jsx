import { useState } from 'react'
import { Link } from 'react-router-dom'
import NavIcon from '../components/NavIcon'
import ManualSessionForm from '../components/timer/ManualSessionForm'
import PhaseTabs from '../components/timer/PhaseTabs'
import StudyTarget from '../components/timer/StudyTarget'
import TimerClock from '../components/timer/TimerClock'
import TimerControls from '../components/timer/TimerControls'
import TodaySessions from '../components/timer/TodaySessions'
import { useSettings } from '../hooks/useSettings'
import { useTimer } from '../hooks/useTimer'
import { finishFocusEarly, pauseTimer, resetTimer, selectPhase, setTimerTarget, skipTimer, startTimer } from '../utils/timerStore'
import './TimerPage.css'

const MINUTE_KEYS = {
  focus: 'pomodoroWork',
  shortBreak: 'pomodoroBreak',
  longBreak: 'longBreak',
}

export default function TimerPage() {
  const timer = useTimer()
  const settings = useSettings()
  const [error, setError] = useState('')
  const focusLocked = timer.phase === 'focus' && (timer.running || timer.paused)
  const showingSession = focusLocked && timer.focusSession
  const subject = showingSession ? timer.focusSession.subject : (timer.targetSubject || 'math')
  const topic = showingSession ? timer.focusSession.topic : (timer.targetTopic || '')
  const remainingMs = timer.running && typeof timer.endsAt === 'number'
    ? Math.max(0, timer.endsAt - timer.now)
    : timer.remainingMs
  const totalMs = (MINUTE_KEYS[timer.phase] ? settings[MINUTE_KEYS[timer.phase]] : settings.pomodoroWork) * 60 * 1000
  const progress = totalMs > 0 ? remainingMs / totalMs : 0

  function handleStart() {
    if (timer.phase === 'focus' && !timer.focusSession && !topic.trim()) {
      setError('قبل از شروع، مبحث را بنویس.')
      return
    }
    if (timer.phase === 'focus' && !timer.focusSession && !subject) {
      setError('قبل از شروع، درس را بنویس.')
      return
    }
    const started = startTimer({ subject, topic })
    if (!started) {
      setError('قبل از شروع، درس و مبحث را بنویس.')
      return
    }
    setError('')
  }

  return (
    <div className="timer-page">
      <section className="timer-stage">
        <StudyTarget
          subject={subject}
          topic={topic}
          locked={focusLocked}
          onSubjectChange={(id) => setTimerTarget({ subject: id })}
          onTopicChange={(value) => setTimerTarget({ topic: value })}
        />
        <PhaseTabs
          phase={timer.phase}
          cycleCount={timer.cycleCount}
          locked={timer.running}
          onSelect={selectPhase}
        />
        <TimerClock remainingMs={remainingMs} progress={progress} />
        <TimerControls
          running={timer.running}
          onStart={handleStart}
          onPause={pauseTimer}
          onReset={() => {
            setError('')
            resetTimer()
          }}
          onSkip={() => {
            setError('')
            skipTimer()
          }}
        />
        {showingSession ? (
          <button
            type="button"
            className="timer-finish"
            onClick={() => {
              setError('')
              finishFocusEarly()
            }}
          >
            پایان و ذخیره
          </button>
        ) : null}
        {error ? (
          <p className="timer-feedback is-error" role="alert">
            {error}
          </p>
        ) : null}
        {!error && timer.notice ? (
          <p className="timer-feedback" role="status">
            {timer.notice}
          </p>
        ) : null}
      </section>

      <Link className="timer-settings-link" to="/settings#timer-lengths" aria-label="تنظیم زمان">
        <NavIcon name="settings" />
      </Link>

      <div className="timer-lower">
        <TodaySessions />
        <ManualSessionForm />
      </div>
    </div>
  )
}
