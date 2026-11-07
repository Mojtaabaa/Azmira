// @ts-nocheck
import { getSubjectById } from '../data/subjects'
import { todayInputValue } from './clock'
import { addOne, getAll, getSettings } from './storage'

function padTime(date) {
  const hours = String(date.getHours()).padStart(2, '0')
  const minutes = String(date.getMinutes()).padStart(2, '0')
  return `${hours}:${minutes}`
}

function dayKey(value) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

function endTimeFromStart(startIso, durationMinutes) {
  const start = new Date(startIso)
  if (Number.isNaN(start.getTime())) return ''
  const end = new Date(start.getTime() + Math.max(1, Number(durationMinutes) || 0) * 60000)
  if (end.toDateString() !== start.toDateString()) return '23:59'
  return padTime(end)
}

export function sessionToEventPayload(session) {
  const start = new Date(session.startTime)
  if (Number.isNaN(start.getTime())) return null
  const subject = getSubjectById(session.subject)
  const topic = typeof session.topic === 'string' ? session.topic.trim() : ''
  const title = topic
    ? `مطالعه ${subject?.name || ''} · ${topic}`.trim()
    : `مطالعه ${subject?.name || ''}`.trim() || 'مطالعه'
  const date = dayKey(session.startTime) || todayInputValue(start)
  const startTime = padTime(start)
  const endTime = endTimeFromStart(session.startTime, session.durationMinutes)
  if (!endTime || endTime <= startTime) return null
  return {
    title: title.slice(0, 160),
    type: 'study',
    subject: subject?.id || session.subject || '',
    date,
    startTime,
    endTime,
    done: true,
    sessionId: session.id,
    source: 'timer',
  }
}

export function addPlannerEventForSession(session) {
  if (!session?.id) return null
  const settings = getSettings()
  if (settings.syncSessionsToPlanner === false) return null
  const existing = getAll('events').find((event) => event.sessionId === session.id)
  if (existing) return existing
  const payload = sessionToEventPayload(session)
  if (!payload) return null
  try {
    return addOne('events', payload)
  } catch {
    return null
  }
}

export function startTimerFromEvent(event, { setTimerTarget, selectPhase, navigate } = {}) {
  if (!event) return false
  const subject = event.subject && getSubjectById(event.subject) ? event.subject : ''
  const topicFromTitle = typeof event.title === 'string'
    ? event.title.replace(/^مطالعه\s+[^\s·]+(?:\s*·\s*)?/, '').trim()
    : ''
  const topic = topicFromTitle || event.title || 'مطالعه'
  if (typeof setTimerTarget === 'function') {
    setTimerTarget({
      subject: subject || 'math',
      topic: topic.slice(0, 80),
    })
  }
  if (typeof selectPhase === 'function') selectPhase('focus')
  if (typeof navigate === 'function') navigate('/timer')
  return true
}

export function eventHasTimerTarget(event) {
  return Boolean(event && (event.type === 'study' || event.subject))
}
