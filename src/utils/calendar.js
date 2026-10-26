import { todayInputValue } from './clock'

const PARTS = new Intl.DateTimeFormat('en-u-ca-persian-nu-latn', {
  year: 'numeric',
  month: 'numeric',
  day: 'numeric',
})

export const WEEKDAY_LETTERS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج']

export function persianParts(date) {
  const parts = PARTS.formatToParts(date)
  const pick = (type) => Number(parts.find((part) => part.type === type).value)
  return { year: pick('year'), month: pick('month'), day: pick('day') }
}

export function addDays(date, count) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + count)
}

export function parseKey(key) {
  const [year, month, day] = key.split('-').map(Number)
  return new Date(year, month - 1, day)
}

export function weekdayIndex(date) {
  return (date.getDay() + 1) % 7
}

export function monthStart(date) {
  return addDays(date, 1 - persianParts(date).day)
}

export function monthLength(start) {
  const month = persianParts(start).month
  let count = 1
  while (persianParts(addDays(start, count)).month === month) count += 1
  return count
}

export function nextMonthAnchor(anchor) {
  const start = monthStart(anchor)
  return addDays(start, monthLength(start))
}

export function previousMonthAnchor(anchor) {
  return addDays(monthStart(anchor), -1)
}

export function buildMonth(anchor) {
  const start = monthStart(anchor)
  const length = monthLength(start)
  const cells = Array.from({ length: weekdayIndex(start) }, () => null)
  for (let index = 0; index < length; index += 1) {
    const date = addDays(start, index)
    cells.push({
      key: todayInputValue(date),
      label: persianParts(date).day.toLocaleString('fa-IR'),
      weekend: weekdayIndex(date) === 6,
    })
  }
  return {
    title: start.toLocaleDateString('fa-IR', { month: 'long', year: 'numeric' }),
    cells,
  }
}

export function weekdayName(date) {
  return date.toLocaleDateString('fa-IR', { weekday: 'long' })
}

export function shortDate(date) {
  return date.toLocaleDateString('fa-IR', { month: 'long', day: 'numeric' })
}

export function dateChipLabel(key, today) {
  if (!key) return 'تاریخ'
  if (key === today) return 'امروز'
  const date = parseKey(key)
  if (key === todayInputValue(addDays(parseKey(today), 1))) return 'فردا'
  return shortDate(date)
}

export function fullDateLabel(key) {
  const date = parseKey(key)
  const parts = new Intl.DateTimeFormat('fa-IR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).formatToParts(date)
  const pick = (type) => parts.find((part) => part.type === type)?.value || ''
  return `${weekdayName(date)} ${pick('day')} ${pick('month')} ${pick('year')}`
}

export function quickDates(today, max = '') {
  const base = parseKey(today)
  const yesterday = addDays(base, -1)
  const tomorrow = addDays(base, 1)
  const toFriday = (6 - weekdayIndex(base) + 7) % 7 || 7
  const toSaturday = 7 - weekdayIndex(base)
  const weekend = addDays(base, toFriday)
  const nextWeek = addDays(base, toSaturday)
  const items = [
    { id: 'today', label: 'امروز', icon: 'today', date: base, side: weekdayName(base) },
    { id: 'yesterday', label: 'دیروز', icon: 'yesterday', date: yesterday, side: weekdayName(yesterday) },
    { id: 'tomorrow', label: 'فردا', icon: 'tomorrow', date: tomorrow, side: weekdayName(tomorrow) },
    { id: 'weekend', label: 'آخر هفته', icon: 'weekend', date: weekend, side: weekdayName(weekend) },
    {
      id: 'next-week',
      label: 'هفته بعد',
      icon: 'next',
      date: nextWeek,
      side: `${weekdayName(nextWeek)} ${shortDate(nextWeek)}`,
    },
  ]
  if (max) return items.filter((item) => item.id === 'today' || item.id === 'yesterday')
  return items.filter((item) => item.id !== 'yesterday')
}
