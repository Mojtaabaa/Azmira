// @ts-nocheck
export function formatClock(milliseconds) {
  const totalSeconds = Math.ceil(Math.max(0, milliseconds) / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  const mm = String(minutes).padStart(2, '0')
  const ss = String(seconds).padStart(2, '0')
  return `${hours}:${mm}:${ss}`
}

export function formatHourCount(totalMinutes) {
  const hours = (Number(totalMinutes) || 0) / 60
  const rounded = Math.round(hours * 10) / 10
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1)
}

export function formatMinutes(totalMinutes) {
  const minutes = Math.max(0, Math.round(Number(totalMinutes) || 0))
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  if (hours === 0) return `${rest} دقیقه`
  if (rest === 0) return `${hours} ساعت`
  return `${hours} ساعت و ${rest} دقیقه`
}

export function formatPausedSeconds(totalSeconds) {
  const seconds = Math.max(0, Math.round(Number(totalSeconds) || 0))
  if (seconds < 60) return `${seconds} ثانیه`
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  if (minutes < 60) {
    return rest > 0 ? `${minutes} دقیقه و ${rest} ثانیه` : `${minutes} دقیقه`
  }
  const hours = Math.floor(minutes / 60)
  const minRest = minutes % 60
  if (minRest === 0) return `${hours} ساعت`
  return `${hours} ساعت و ${minRest} دقیقه`
}

export function formatTimeOfDay(value) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('fa-IR', { hour: 'numeric', minute: '2-digit' })
}

export function isSameLocalDay(value, day) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return false
  return (
    date.getFullYear() === day.getFullYear() &&
    date.getMonth() === day.getMonth() &&
    date.getDate() === day.getDate()
  )
}

export function todayInputValue(day = new Date()) {
  const month = String(day.getMonth() + 1).padStart(2, '0')
  const date = String(day.getDate()).padStart(2, '0')
  return `${day.getFullYear()}-${month}-${date}`
}

export function formatDateLabel(value) {
  if (!isRealDateString(value)) return value || ''
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day).toLocaleDateString('fa-IR', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

export function isRealDateString(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
}

export function isFutureDate(value, now = new Date()) {
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  return date > today
}

export function dateAtCurrentTime(value, now = new Date()) {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(
    year,
    month - 1,
    day,
    now.getHours(),
    now.getMinutes(),
    now.getSeconds(),
    0,
  ).toISOString()
}
