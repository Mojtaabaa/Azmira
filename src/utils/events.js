import { getSubjectById } from '../data/subjects'
import { isRealDateString } from './clock'

export const EVENT_TYPES = [
  { id: 'exam', label: 'آزمون' },
  { id: 'homework', label: 'تکلیف' },
  { id: 'class', label: 'کلاس' },
  { id: 'study', label: 'مطالعه' },
]

const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/

export function eventTypeLabel(type) {
  return EVENT_TYPES.find((item) => item.id === type)?.label || ''
}

function cleanTime(value) {
  return typeof value === 'string' ? value.trim() : ''
}

export function buildEvent(input) {
  const title = typeof input.title === 'string' ? input.title.trim() : ''
  const type = typeof input.type === 'string' ? input.type.trim() : ''
  const subject = typeof input.subject === 'string' ? input.subject.trim() : ''
  const date = typeof input.date === 'string' ? input.date.trim() : ''
  const startTime = cleanTime(input.startTime)
  const endTime = cleanTime(input.endTime)

  if (!title) return { error: 'عنوان را بنویس.' }
  if (!EVENT_TYPES.some((item) => item.id === type)) return { error: 'نوع را انتخاب کن.' }
  if (subject && !getSubjectById(subject)) return { error: 'درس را درست انتخاب کن.' }
  if (!date) return { error: 'تاریخ را انتخاب کن.' }
  if (!isRealDateString(date)) return { error: 'یک تاریخ واقعی وارد کن.' }
  if (startTime && !TIME_PATTERN.test(startTime)) return { error: 'ساعت شروع را مثل 14:30 بنویس.' }
  if (endTime && !TIME_PATTERN.test(endTime)) return { error: 'ساعت پایان را مثل 15:30 بنویس.' }
  if ((startTime && !endTime) || (!startTime && endTime)) return { error: 'ساعت شروع و پایان را با هم بنویس.' }
  if (startTime && endTime && endTime <= startTime) return { error: 'ساعت پایان باید بعد از شروع باشد.' }

  return {
    event: {
      title,
      type,
      subject,
      date,
      startTime,
      endTime,
    },
  }
}

function bySchedule(a, b) {
  const aTime = a.startTime || ''
  const bTime = b.startTime || ''
  if (a.date !== b.date) return a.date < b.date ? -1 : 1
  if (aTime !== bTime) return aTime < bTime ? -1 : 1
  return String(a.title).localeCompare(String(b.title), 'fa')
}

export function eventsOnDay(events, day) {
  return events.filter((event) => event.date === day).slice().sort(bySchedule)
}

export const GRID_START = 6 * 60
export const GRID_END = 24 * 60
export const SLOT_MINUTES = 30
export const SLOT_PX = 28
export const RESIZE_STEP = 15

export function minutesOfTime(value) {
  if (!TIME_PATTERN.test(value || '')) return null
  const [hour, minute] = value.split(':').map(Number)
  return hour * 60 + minute
}

export function formatMinutesAsTime(total) {
  const minutes = Math.max(0, Math.min(23 * 60 + 59, total))
  const hour = Math.floor(minutes / 60)
  const minute = minutes % 60
  return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`
}

export function slotMinutes() {
  const slots = []
  for (let minutes = GRID_START; minutes < GRID_END; minutes += SLOT_MINUTES) slots.push(minutes)
  return slots
}

export function shiftTimes(item, startMinutes) {
  const currentStart = minutesOfTime(item.startTime)
  const currentEnd = minutesOfTime(item.endTime)
  const duration = currentStart != null && currentEnd != null && currentEnd > currentStart
    ? currentEnd - currentStart
    : 60
  let start = Math.max(GRID_START, Math.round(startMinutes / RESIZE_STEP) * RESIZE_STEP)
  let end = start + duration
  if (end >= 24 * 60) {
    end = 23 * 60 + 59
    start = Math.max(GRID_START, end - duration)
    if (end - start < RESIZE_STEP) start = end - RESIZE_STEP
  }
  return {
    startTime: formatMinutesAsTime(start),
    endTime: formatMinutesAsTime(end),
  }
}

export function resizeTimes(start, end, edge, deltaMinutes) {
  if (start == null || end == null || end <= start) return null
  const snapped = Math.round(deltaMinutes / RESIZE_STEP) * RESIZE_STEP
  const latest = 23 * 60 + 59
  let nextStart = start
  let nextEnd = end

  if (edge === 'start') {
    nextStart = Math.min(Math.max(GRID_START, start + snapped), end - RESIZE_STEP)
  } else {
    nextEnd = Math.max(start + RESIZE_STEP, end + snapped)
    if (nextEnd >= 24 * 60) nextEnd = latest
  }

  if (nextEnd <= nextStart) return null
  return {
    startTime: formatMinutesAsTime(nextStart),
    endTime: formatMinutesAsTime(nextEnd),
  }
}

export function layoutTimed(events) {
  const items = events
    .map((event) => {
      const start = minutesOfTime(event.startTime)
      const end = minutesOfTime(event.endTime)
      if (start == null || end == null || end <= start) return null
      return { event, start, end }
    })
    .filter(Boolean)
    .sort((a, b) => a.start - b.start || a.end - b.end)

  const groups = []
  let group = []
  let groupEnd = -1
  items.forEach((item) => {
    if (group.length && item.start >= groupEnd) {
      groups.push(group)
      group = []
      groupEnd = -1
    }
    group.push(item)
    groupEnd = Math.max(groupEnd, item.end)
  })
  if (group.length) groups.push(group)

  return groups.flatMap((cluster) => {
    const lanes = []
    const placed = cluster.map((item) => {
      let lane = lanes.findIndex((end) => end <= item.start)
      if (lane === -1) {
        lane = lanes.length
        lanes.push(item.end)
      } else {
        lanes[lane] = item.end
      }
      return { ...item, lane }
    })
    const laneCount = placed.reduce((max, entry) => Math.max(max, entry.lane + 1), 1)
    return placed.map((item) => ({ ...item, laneCount }))
  })
}

export function upcomingExams(events, today) {
  return events
    .filter((event) => event.type === 'exam' && !event.done && isRealDateString(event.date) && event.date >= today)
    .slice()
    .sort(bySchedule)
}
