import { formatClock } from './clock'
import { createId } from './id'
import { askNotifyPermission, notifyPhaseEnd, unlockSound } from './notify'
import { addOne, getSettings } from './storage'
import { nextPhaseAfterComplete, nextPhaseAfterSkip } from './timerPhases'

const LEGACY_TIMER_KEY = 'konkoor-help-timer'
const PHASE_LABELS = {
  focus: 'تمرکز',
  shortBreak: 'استراحت کوتاه',
  longBreak: 'استراحت طولانی',
}

let timerKey = null
let ticker = null
let completing = false
const listeners = new Set()

function readSettings() {
  return getSettings()
}

function fullDuration(phase) {
  const settings = readSettings()
  const minutes = phase === 'shortBreak'
    ? settings.pomodoroBreak
    : phase === 'longBreak'
      ? settings.longBreak
      : settings.pomodoroWork
  return minutes * 60 * 1000
}

function createIdleState() {
  return {
    phase: 'focus',
    running: false,
    paused: false,
    remainingMs: fullDuration('focus'),
    endsAt: null,
    now: Date.now(),
    cycleCount: 0,
    focusSession: null,
    targetSubject: 'math',
    targetTopic: '',
    notice: '',
  }
}

function isFocusSession(value) {
  return Boolean(
    value &&
    typeof value.id === 'string' &&
    typeof value.subject === 'string' &&
    typeof value.topic === 'string' &&
    typeof value.startedAt === 'string' &&
    Number.isInteger(value.durationMinutes) &&
    value.durationMinutes > 0,
  )
}

function pausedMsOf(focusSession) {
  const base = Number(focusSession?.pausedMs)
  const started = Number(focusSession?.pauseStartedAt)
  let total = Number.isFinite(base) && base > 0 ? base : 0
  if (Number.isFinite(started) && started > 0) {
    total += Math.max(0, Date.now() - started)
  }
  return total
}

function withClosedPause(focusSession) {
  if (!focusSession) return null
  return {
    ...focusSession,
    pausedMs: pausedMsOf(focusSession),
    pauseStartedAt: null,
  }
}

function readSavedRaw() {
  if (!timerKey || typeof localStorage === 'undefined') return null
  try {
    return localStorage.getItem(timerKey)
  } catch {
    return null
  }
}

function loadState() {
  const fallback = createIdleState()
  const raw = readSavedRaw()
  if (!raw) return fallback

  try {
    const saved = JSON.parse(raw)
    const phase = saved.phase === 'shortBreak' || saved.phase === 'longBreak' ? saved.phase : 'focus'
    const remainingMs = Number(saved.remainingMs)
    const focusSession = isFocusSession(saved.focusSession) ? saved.focusSession : null
    const targetSubject = typeof saved.targetSubject === 'string' && saved.targetSubject
      ? saved.targetSubject
      : (focusSession?.subject || 'math')
    const targetTopic = typeof saved.targetTopic === 'string'
      ? saved.targetTopic
      : (focusSession?.topic || '')
    const paused = Boolean(saved.paused)
    const endsAt = !paused && typeof saved.endsAt === 'number' ? saved.endsAt : null
    return {
      phase,
      running: !paused && Boolean(saved.running && endsAt),
      paused,
      remainingMs: Number.isFinite(remainingMs) && remainingMs > 0 ? remainingMs : fullDuration(phase),
      endsAt,
      now: Date.now(),
      cycleCount: Number.isInteger(saved.cycleCount)
        ? Math.min(4, Math.max(0, saved.cycleCount))
        : 0,
      focusSession,
      targetSubject,
      targetTopic,
      notice: typeof saved.notice === 'string' ? saved.notice : '',
    }
  } catch {
    return fallback
  }
}

let state = createIdleState()

function emit() {
  for (const listener of listeners) listener()
}

function persist() {
  if (!timerKey || typeof localStorage === 'undefined') return
  const saved = { ...state }
  delete saved.now
  try {
    localStorage.setItem(timerKey, JSON.stringify(saved))
  } catch {
    return
  }
}

function updateTitle() {
  if (typeof document === 'undefined') return
  const label = PHASE_LABELS[state.phase] || 'تمرکز'
  if (state.paused) {
    document.title = `توقف ${formatClock(state.remainingMs)} · ${label}`
    return
  }
  if (!state.running || typeof state.endsAt !== 'number') {
    document.title = 'رتبه'
    return
  }
  document.title = `${formatClock(Math.max(0, state.endsAt - Date.now()))} · ${label}`
}

function stopTicker() {
  if (!ticker) return
  clearInterval(ticker)
  ticker = null
}

function tick() {
  if (!state.running || state.paused || typeof state.endsAt !== 'number') {
    stopTicker()
    return
  }
  checkComplete()
  if (!state.running || state.paused) return
  state = { ...state, now: Date.now() }
  emit()
  updateTitle()
}

function ensureTicker() {
  if (ticker || !state.running) return
  ticker = setInterval(tick, 250)
}

function saveFocusSession(focusSession) {
  try {
    const pausedMs = pausedMsOf(focusSession)
    const session = {
      id: focusSession.id,
      subject: focusSession.subject,
      topic: focusSession.topic,
      startTime: focusSession.startedAt,
      durationMinutes: focusSession.durationMinutes,
      pausedSeconds: Math.round(pausedMs / 1000),
      type: 'pomodoro',
      note: '',
    }
    addOne('sessions', session)
    try {
      // Lazy import avoids circular load with storage helpers.
      import('./timerPlanner').then(({ addPlannerEventForSession }) => {
        addPlannerEventForSession(session)
      })
    } catch {
      /* ignore planner sync errors */
    }
    return 'جلسه تمرکز ذخیره شد.'
  } catch (error) {
    if (String(error.message).includes('تکراری') || String(error.message).includes('already exists')) {
      return 'جلسه تمرکز ذخیره شد.'
    }
    return 'تمرکز تمام شد، ولی ذخیره نشد.'
  }
}

function checkComplete() {
  if (completing || !state.running || typeof state.endsAt !== 'number') return
  if (Date.now() < state.endsAt) return

  completing = true
  stopTicker()
  const finishedPhase = state.phase
  const finishedSession = withClosedPause(state.focusSession)
  const cycle = nextPhaseAfterComplete(finishedPhase, state.cycleCount)
  const notice = finishedPhase === 'focus' && finishedSession
    ? saveFocusSession(finishedSession)
    : ''

  state = {
    ...state,
    phase: cycle.phase,
    running: false,
    paused: false,
    remainingMs: fullDuration(cycle.phase),
    endsAt: null,
    now: Date.now(),
    cycleCount: cycle.cycleCount,
    focusSession: null,
    notice,
  }
  persist()
  emit()
  updateTitle()
  completing = false
  notifyPhaseEnd(finishedPhase, cycle.phase)
}

function commit(next) {
  state = next
  persist()
  emit()
  updateTitle()
}

export function getTimerState() {
  return state
}

export function subscribeTimer(listener) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function syncSettings() {
  if (state.running || state.paused) return
  const remainingMs = fullDuration(state.phase)
  if (remainingMs === state.remainingMs) return
  commit({ ...state, remainingMs })
}

export function setTimerTarget({ subject, topic } = {}) {
  const targetSubject = typeof subject === 'string' ? subject : state.targetSubject
  const targetTopic = typeof topic === 'string' ? topic.slice(0, 80) : state.targetTopic
  if (targetSubject === state.targetSubject && targetTopic === state.targetTopic) return
  commit({ ...state, targetSubject, targetTopic })
}

export function selectPhase(phase) {
  if (state.running) return
  if (phase !== 'focus' && phase !== 'shortBreak' && phase !== 'longBreak') return
  if (phase === state.phase) return
  commit({
    ...state,
    phase,
    running: false,
    paused: false,
    endsAt: null,
    remainingMs: fullDuration(phase),
    focusSession: null,
    notice: '',
  })
}

export function startTimer({ subject, topic } = {}) {
  if (state.running) return true

  let focusSession = state.focusSession
  let targetSubject = state.targetSubject
  let targetTopic = state.targetTopic
  if (state.phase === 'focus' && !focusSession) {
    const cleanTopic = typeof topic === 'string' ? topic.trim() : ''
    if (!subject || !cleanTopic) return false
    targetSubject = subject
    targetTopic = cleanTopic
    focusSession = {
      id: createId(),
      subject,
      topic: cleanTopic,
      startedAt: new Date().toISOString(),
      durationMinutes: readSettings().pomodoroWork,
      pausedMs: 0,
      pauseStartedAt: null,
    }
  } else if (focusSession) {
    focusSession = withClosedPause(focusSession)
  }

  unlockSound()
  askNotifyPermission()
  commit({
    ...state,
    focusSession,
    targetSubject,
    targetTopic,
    running: true,
    paused: false,
    endsAt: Date.now() + state.remainingMs,
    now: Date.now(),
    notice: '',
  })
  ensureTicker()
  return true
}

export function pauseTimer() {
  if (!state.running || typeof state.endsAt !== 'number') return
  stopTicker()
  const remainingMs = Math.max(0, state.endsAt - Date.now())
  const focusSession = state.focusSession
    ? {
        ...state.focusSession,
        pauseStartedAt: Date.now(),
      }
    : null
  commit({
    ...state,
    focusSession,
    running: false,
    paused: true,
    remainingMs,
    endsAt: null,
    now: Date.now(),
  })
}

export function finishFocusEarly() {
  if (state.phase !== 'focus' || !isFocusSession(state.focusSession)) return
  if (!state.running && !state.paused) return

  stopTicker()
  const closed = withClosedPause(state.focusSession)
  const plannedMs = closed.durationMinutes * 60 * 1000
  const remainingMs = state.running && typeof state.endsAt === 'number'
    ? Math.max(0, state.endsAt - Date.now())
    : Math.max(0, state.remainingMs)
  const elapsedMs = Math.max(0, plannedMs - remainingMs)
  const rounded = Math.round(elapsedMs / 60000)
  const minutes = rounded > 0 ? rounded : (elapsedMs >= 1000 ? 1 : 0)
  let notice = 'زمانی نگذشته که ذخیره شود.'

  if (minutes > 0) {
    const saved = saveFocusSession({ ...closed, durationMinutes: minutes })
    if (saved !== 'جلسه تمرکز ذخیره شد.') {
      commit({
        ...state,
        focusSession: closed,
        running: false,
        paused: true,
        remainingMs,
        endsAt: null,
        now: Date.now(),
        notice: saved,
      })
      return
    }
    notice = `${minutes} دقیقه مطالعه ذخیره شد.`
  }

  commit({
    ...state,
    running: false,
    paused: false,
    endsAt: null,
    remainingMs: fullDuration('focus'),
    now: Date.now(),
    focusSession: null,
    notice,
  })
}

export function clearTimer() {
  stopTicker()
  completing = false
  state = createIdleState()
  persist()
  emit()
  updateTitle()
}

export function resetTimer() {
  stopTicker()
  commit({
    ...state,
    running: false,
    paused: false,
    endsAt: null,
    remainingMs: fullDuration(state.phase),
    focusSession: null,
    notice: '',
  })
}

export function skipTimer() {
  stopTicker()
  const cycle = nextPhaseAfterSkip(state.phase, state.cycleCount)
  commit({
    ...state,
    phase: cycle.phase,
    running: false,
    paused: false,
    endsAt: null,
    remainingMs: fullDuration(cycle.phase),
    cycleCount: cycle.cycleCount,
    focusSession: null,
    now: Date.now(),
    notice: '',
  })
}

function onWake() {
  checkComplete()
  if (!state.running) return
  state = { ...state, now: Date.now() }
  emit()
  updateTitle()
}

function restoreTicker() {
  if (state.running && typeof state.endsAt === 'number' && Date.now() >= state.endsAt) {
    checkComplete()
    return
  }
  if (state.running) {
    ensureTicker()
    updateTitle()
  }
}

export function bindTimer(userId) {
  stopTicker()
  completing = false
  timerKey = `${LEGACY_TIMER_KEY}:${userId}`
  state = loadState()
  emit()
  updateTitle()
  restoreTicker()
}

export function unbindTimer() {
  stopTicker()
  completing = false
  timerKey = null
  state = createIdleState()
  emit()
  updateTitle()
}

export function adoptLegacyTimer(userId) {
  if (typeof localStorage === 'undefined') return
  try {
    const legacy = localStorage.getItem(LEGACY_TIMER_KEY)
    const key = `${LEGACY_TIMER_KEY}:${userId}`
    if (!legacy || localStorage.getItem(key)) return
    localStorage.setItem(key, legacy)
  } catch {
    return
  }
  bindTimer(userId)
}

if (typeof window !== 'undefined') {
  window.addEventListener('focus', onWake)
  document.addEventListener('visibilitychange', onWake)
  window.addEventListener('storage', (event) => {
    if (!timerKey || event.key !== timerKey) return
    stopTicker()
    state = loadState()
    emit()
    updateTitle()
    restoreTicker()
  })
}
