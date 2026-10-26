import { isRealDateString } from './clock'

export const WEEKDAY_NAMES = ['شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه']

function parseDay(value) {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

function dayKey(date) {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function shiftDay(day, amount) {
  const date = parseDay(day)
  date.setDate(date.getDate() + amount)
  return dayKey(date)
}

export function saturdayOf(day) {
  const date = parseDay(day)
  const offset = (date.getDay() + 1) % 7
  date.setDate(date.getDate() - offset)
  return dayKey(date)
}

export function daysOfWeek(start) {
  return WEEKDAY_NAMES.map((name, index) => ({
    name,
    date: shiftDay(start, index),
  }))
}

function formatCalendar(day, calendar, withYear) {
  if (!isRealDateString(day)) return ''
  const options = { day: 'numeric', month: 'short' }
  if (withYear) options.year = 'numeric'
  return parseDay(day).toLocaleDateString(`fa-IR-u-ca-${calendar}`, options)
}

export function formatGregorian(day, withYear = false) {
  return formatCalendar(day, 'gregory', withYear)
}

export function formatJalali(day, withYear = false) {
  return formatCalendar(day, 'persian', withYear)
}
