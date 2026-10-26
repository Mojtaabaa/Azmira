import { normalizeData } from '../shared/normalize'
import { hasRecords } from './storage'

export const LEGACY_KEY = 'konkoor-help'
export const SKIP_PREFIX = 'konkoor-help-legacy-skip:'
export const IMPORTED_PREFIX = 'konkoor-help-legacy-imported:'

const LEGACY_KEYS = [
  'konkoor-help',
  'rotbe-help',
  'rotbe-data',
  'konkoorHelp',
  'konkoor_help',
]

const LABELS = [
  ['sessions', 'جلسه'],
  ['tests', 'آزمون'],
  ['mistakes', 'اشتباه'],
  ['topics', 'مبحث'],
  ['tasks', 'کار'],
  ['events', 'رویداد'],
]

function parseLegacyRaw(raw) {
  if (!raw) return null
  try {
    const data = normalizeData(JSON.parse(raw), false)
    if (!hasRecords(data)) return null
    return data
  } catch {
    return null
  }
}

export function readBrowserLegacy() {
  if (typeof localStorage === 'undefined') return null
  for (const key of LEGACY_KEYS) {
    try {
      const data = parseLegacyRaw(localStorage.getItem(key))
      if (data) return data
    } catch {
      continue
    }
  }
  return scanLooseLegacy()
}

function scanLooseLegacy() {
  try {
    for (let i = 0; i < localStorage.length; i += 1) {
      const key = localStorage.key(i)
      if (!key) continue
      if (key.startsWith(SKIP_PREFIX) || key.startsWith(IMPORTED_PREFIX)) continue
      if (key.includes('timer') || key.includes('theme') || key.includes('draft')) continue
      if (!/konkoor|rotbe|help/i.test(key)) continue
      const data = parseLegacyRaw(localStorage.getItem(key))
      if (data) return data
    }
  } catch {
    return null
  }
  return null
}

export function wasLegacySkipped(userId) {
  try {
    return Boolean(localStorage.getItem(`${SKIP_PREFIX}${userId}`))
  } catch {
    return false
  }
}

export function wasLegacyImported(userId) {
  try {
    return Boolean(localStorage.getItem(`${IMPORTED_PREFIX}${userId}`))
  } catch {
    return false
  }
}

export function markLegacyHandled(userId) {
  try {
    localStorage.setItem(`${SKIP_PREFIX}${userId}`, 'done')
  } catch {
    return
  }
}

export function markLegacyImported(userId) {
  try {
    localStorage.setItem(`${IMPORTED_PREFIX}${userId}`, 'done')
    localStorage.setItem(`${SKIP_PREFIX}${userId}`, 'done')
  } catch {
    return
  }
}

export function clearLegacySkip(userId) {
  try {
    localStorage.removeItem(`${SKIP_PREFIX}${userId}`)
    localStorage.removeItem(`${IMPORTED_PREFIX}${userId}`)
  } catch {
    return
  }
}

export function summarizeLegacy(data) {
  if (!data) return ''
  return LABELS
    .filter(([key]) => data[key]?.length > 0)
    .map(([key, label]) => `${data[key].length} ${label}`)
    .join('، ')
}
