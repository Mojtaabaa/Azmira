import { isRealDateString, todayInputValue } from './clock'

export const KONKOOR_DATE = new Date('2028-07-01T08:00:00+03:30')
const IRAN_OFFSET_MS = (3 * 60 + 30) * 60 * 1000

function tehranKey(date) {
  const shifted = new Date(date.getTime() + IRAN_OFFSET_MS)
  const month = String(shifted.getUTCMonth() + 1).padStart(2, '0')
  const day = String(shifted.getUTCDate()).padStart(2, '0')
  return `${shifted.getUTCFullYear()}-${month}-${day}`
}

export const OFFICIAL_KONKOOR_DAY = tehranKey(KONKOOR_DATE)

export function konkoorDay(settings) {
  const stored = settings?.konkoorDate
  return isRealDateString(stored) ? stored : OFFICIAL_KONKOOR_DAY
}

export function examMoment(day) {
  return new Date(`${day}T08:00:00+03:30`)
}

export function countdownToKonkoor(settings, now = new Date()) {
  const day = konkoorDay(settings)
  const today = todayInputValue(now)
  const days = Math.round((Date.parse(`${day}T00:00:00Z`) - Date.parse(`${today}T00:00:00Z`)) / 86400000)
  const started = now.getTime() >= examMoment(day).getTime()
  return { day, days, started }
}
