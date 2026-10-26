import { COLLECTION_KEYS, DEFAULT_SETTINGS } from '../data/defaults.js'
import { createId } from '../utils/id.js'

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/
const NUMBER_SETTINGS = ['pomodoroWork', 'pomodoroBreak', 'longBreak']
const DEFAULT_SUBJECT_NAMES = ['ریاضی', 'فیزیک', 'شیمی', 'هندسه', 'ادبیات']
const MAX_SUBJECT_NAME = 40

export function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
}

function collectionLabel(name) {
  const labels = {
    sessions: 'جلسه‌ها',
    tests: 'آزمون‌ها',
    mistakes: 'اشتباهات',
    topics: 'مبحث‌ها',
    tasks: 'کارها',
    events: 'رویدادها',
  }
  return labels[name] || name
}

function isIsoDate(value) {
  if (typeof value !== 'string' || !DATE_PATTERN.test(value)) return false
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day))
  return (
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
  )
}

export function createEmptyData() {
  return {
    sessions: [],
    tests: [],
    mistakes: [],
    topics: [],
    tasks: [],
    events: [],
    settings: { ...DEFAULT_SETTINGS, customSubjects: [], hiddenSubjects: [], subjectWeights: {} },
  }
}

function normalizeCustomSubjects(value, strict) {
  if (value === undefined) return []
  if (!Array.isArray(value)) {
    if (strict) throw new Error('درس‌های سفارشی باید یک فهرست باشد.')
    return []
  }

  const names = new Set(DEFAULT_SUBJECT_NAMES)
  const ids = new Set()
  const next = []

  value.forEach((item, index) => {
    const place = `درس سفارشی ${index + 1}`
    if (!isPlainObject(item)) {
      if (strict) throw new Error(`${place} باید یک شیء باشد.`)
      return
    }

    const id = typeof item.id === 'string' ? item.id.trim() : ''
    const name = typeof item.name === 'string' ? item.name.trim().replace(/\s+/g, ' ') : ''
    if (!/^custom-[A-Za-z0-9-]{8,80}$/.test(id)) {
      if (strict) throw new Error(`${place} شناسه درستی ندارد.`)
      return
    }
    if (!name || name.length > MAX_SUBJECT_NAME) {
      if (strict) throw new Error(`${place} نام درستی ندارد.`)
      return
    }
    if (names.has(name) || ids.has(id)) {
      if (strict) throw new Error(`${place} تکراری است.`)
      return
    }

    names.add(name)
    ids.add(id)
    next.push({ id, name })
  })

  return next
}

export function assertCollection(name) {
  if (!COLLECTION_KEYS.includes(name)) {
    throw new Error(`Unknown collection: ${name}`)
  }
}

function normalizeWeights(value, strict) {
  if (value === undefined) return {}
  if (!isPlainObject(value)) {
    if (strict) throw new Error('وزن درس‌ها باید یک شیء باشد.')
    return {}
  }

  const next = {}
  Object.entries(value).forEach(([key, raw]) => {
    if (key === '__proto__' || key === 'constructor' || key === 'prototype') return
    const weight = Number(raw)
    const idOk = /^[A-Za-z0-9-]{1,90}$/.test(key)
    if (!idOk || !Number.isInteger(weight) || weight < 0 || weight > 100) {
      if (strict) throw new Error('وزن هر درس باید یک عدد از 0 تا 100 باشد.')
      return
    }
    next[key] = weight
  })
  return next
}

const BUILTIN_SUBJECT_IDS = new Set(['math', 'physics', 'chemistry', 'geometry', 'literature'])

function normalizeHiddenSubjects(value, strict) {
  if (value === undefined) return []
  if (!Array.isArray(value)) {
    if (strict) throw new Error('درس‌های پنهان باید یک فهرست باشد.')
    return []
  }
  const next = []
  const seen = new Set()
  value.forEach((item) => {
    if (typeof item !== 'string' || !BUILTIN_SUBJECT_IDS.has(item) || seen.has(item)) {
      if (strict) throw new Error('شناسه درس پنهان درست نیست.')
      return
    }
    seen.add(item)
    next.push(item)
  })
  return next
}

export function normalizeSettings(settings, strict) {
  if (!isPlainObject(settings)) {
    if (strict) throw new Error('تنظیمات باید یک شیء باشد.')
    return { ...DEFAULT_SETTINGS, customSubjects: [], hiddenSubjects: [], subjectWeights: {} }
  }

  const next = { ...DEFAULT_SETTINGS, customSubjects: [], hiddenSubjects: [], subjectWeights: {} }

  for (const key of NUMBER_SETTINGS) {
    if (settings[key] === undefined) continue
    const value = Number(settings[key])
    if (!Number.isInteger(value) || value <= 0) {
      if (strict) throw new Error(`${key} باید یک عدد درست مثبت باشد.`)
      continue
    }
    next[key] = value
  }

  if (settings.konkoorDate === undefined || settings.konkoorDate === '') {
    next.konkoorDate = ''
  } else if (isIsoDate(settings.konkoorDate)) {
    next.konkoorDate = settings.konkoorDate
  } else if (strict) {
    throw new Error('تاریخ کنکور باید مثل 2028-07-01 باشد.')
  }

  if (settings.dailyGoalMinutes !== undefined) {
    let goal = Number(settings.dailyGoalMinutes)
    if (goal === 180) goal = 240
    if (Number.isInteger(goal) && goal >= 60 && goal <= 1440 && goal % 60 === 0) next.dailyGoalMinutes = goal
    else if (strict) throw new Error('هدف روزانه باید یک عدد از 1 تا 24 ساعت باشد.')
  }

  if (settings.weeklyGoalMinutes !== undefined) {
    let goal = Number(settings.weeklyGoalMinutes)
    if (goal === 1260) goal = 1680
    if (Number.isInteger(goal) && goal >= 60 && goal <= 10080 && goal % 60 === 0) next.weeklyGoalMinutes = goal
    else if (strict) throw new Error('هدف هفته باید یک عدد از 1 تا 168 ساعت باشد.')
  }

  next.customSubjects = normalizeCustomSubjects(settings.customSubjects, strict)
  next.hiddenSubjects = normalizeHiddenSubjects(settings.hiddenSubjects, strict)
  next.subjectWeights = normalizeWeights(settings.subjectWeights, strict)
  return next
}

export function normalizeCollection(name, items, strict) {
  const label = collectionLabel(name)

  if (!Array.isArray(items)) {
    if (strict) throw new Error(`${label} باید یک فهرست باشد.`)
    return []
  }

  const seen = new Set()
  const next = []

  items.forEach((item, index) => {
    if (!isPlainObject(item)) {
      if (strict) throw new Error(`مورد ${index + 1} در ${label} باید یک شیء باشد.`)
      return
    }

    let id = typeof item.id === 'string' ? item.id.trim() : ''
    if (!id) {
      if (strict) throw new Error(`مورد ${index + 1} در ${label} شناسه ندارد.`)
      id = createId()
    }

    if (seen.has(id)) {
      if (strict) throw new Error(`${label} شناسه تکراری دارد.`)
      return
    }

    seen.add(id)
    next.push({ ...item, id })
  })

  return next
}

export function normalizeData(incoming, strict) {
  if (!isPlainObject(incoming)) {
    if (strict) throw new Error('فایل پشتیبان باید یک شیء JSON باشد.')
    return createEmptyData()
  }

  const data = {}

  for (const key of COLLECTION_KEYS) {
    if (strict && !(key in incoming)) {
      throw new Error(`فایل پشتیبان «${key}» را ندارد.`)
    }
    data[key] = normalizeCollection(key, incoming[key], strict)
  }

  data.settings = normalizeSettings(incoming.settings, strict)
  return data
}
