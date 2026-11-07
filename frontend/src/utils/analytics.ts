// @ts-nocheck
import { CAUSES } from '../data/causes'
import { getSubjectById, listSubjects } from '../data/subjects'
import { formatMinutes, isRealDateString } from './clock'
import { konkoorPercentage } from './score'
import { countTaskBuckets } from './tasks'
import { subjectWeekProgress } from './subjectGoals'

export const GRAINS = [
  { id: 'day', label: 'روز' },
  { id: 'week', label: 'هفته' },
  { id: 'month', label: 'ماه' },
]

const WEEKDAYS = ['شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه']

const BUCKETS = [
  { id: 'dawn', label: 'بامداد', from: 0, to: 5 },
  { id: 'morning', label: 'صبح', from: 5, to: 12 },
  { id: 'afternoon', label: 'بعدازظهر', from: 12, to: 17 },
  { id: 'evening', label: 'عصر', from: 17, to: 21 },
  { id: 'night', label: 'شب', from: 21, to: 24 },
]

function round1(value) {
  return Math.round(value * 10) / 10
}

function minutesOf(session) {
  const minutes = Number(session?.durationMinutes)
  if (!Number.isFinite(minutes) || minutes <= 0) return 0
  return minutes
}

function dayKeyFromDate(date) {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function dayKeyFromValue(value) {
  if (typeof value === 'string' && isRealDateString(value)) return value
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return dayKeyFromDate(date)
}

function shiftDay(day, amount) {
  const [year, month, date] = day.split('-').map(Number)
  const value = new Date(year, month - 1, date)
  value.setDate(value.getDate() + amount)
  return dayKeyFromDate(value)
}

function spanDays(start, end) {
  const [startYear, startMonth, startDate] = start.split('-').map(Number)
  const [endYear, endMonth, endDate] = end.split('-').map(Number)
  const from = new Date(startYear, startMonth - 1, startDate)
  const to = new Date(endYear, endMonth - 1, endDate)
  return Math.round((to - from) / 86400000)
}

function weekStartKey(day) {
  const [year, month, date] = day.split('-').map(Number)
  const value = new Date(year, month - 1, date)
  const offset = (value.getDay() + 1) % 7
  value.setDate(value.getDate() - offset)
  return dayKeyFromDate(value)
}

function formatShortDay(day) {
  const [year, month, date] = day.split('-').map(Number)
  return new Date(year, month - 1, date).toLocaleDateString('fa-IR', {
    month: 'short',
    day: 'numeric',
  })
}

function formatWeek(saturday) {
  return `شنبه ${formatShortDay(saturday)} تا جمعه ${formatShortDay(shiftDay(saturday, 6))}`
}

function formatMonth(key) {
  const [year, month] = key.split('-').map(Number)
  return new Date(year, month - 1, 1).toLocaleDateString('fa-IR', {
    month: 'short',
    year: 'numeric',
  })
}

function upToToday(day, today) {
  return Boolean(day) && day <= today
}

function dayKeys(start, end) {
  if (!start || start > end || spanDays(start, end) > 100) return []
  const keys = []
  let cursor = start
  while (cursor <= end) {
    keys.push(cursor)
    cursor = shiftDay(cursor, 1)
  }
  return keys
}

function weekKeys(start, end) {
  const keys = []
  let cursor = weekStartKey(start)
  const last = weekStartKey(end)
  while (cursor <= last) {
    keys.push(cursor)
    cursor = shiftDay(cursor, 7)
  }
  return keys
}

function monthKeys(start, end) {
  const keys = []
  let cursor = start.slice(0, 7)
  const last = end.slice(0, 7)
  while (cursor <= last) {
    keys.push(cursor)
    const [year, month] = cursor.split('-').map(Number)
    const next = new Date(year, month, 1)
    cursor = `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, '0')}`
  }
  return keys
}

function weekdayName(day) {
  const saturday = weekStartKey(day)
  return WEEKDAYS[spanDays(saturday, day)] || formatShortDay(day)
}

function bucketLabel(key, grain) {
  if (grain === 'month') return formatMonth(key)
  if (grain === 'week') return formatWeek(key)
  return `${weekdayName(key)} ${formatShortDay(key)}`
}

function currentWeekKeys(today) {
  const saturday = weekStartKey(today)
  return WEEKDAYS.map((_, index) => shiftDay(saturday, index))
}

function questionCountOf(test) {
  const count = Number(test?.totalQuestions)
  if (!Number.isFinite(count) || count <= 0) return 0
  return count
}

function filledKeys(start, today, grain, totals) {
  const filled = grain === 'week'
    ? weekKeys(start, today)
    : grain === 'month'
      ? monthKeys(start, today)
      : dayKeys(start, today)
  return filled.length > 0 ? filled : [...totals.keys()].sort()
}

function studyBuckets(sessions, grain, today) {
  const totals = new Map()
  const counts = new Map()
  const days = []
  sessions.forEach((session) => {
    const day = dayKeyFromValue(session.startTime)
    if (!upToToday(day, today)) return
    days.push(day)
    const key = grain === 'month' ? day.slice(0, 7) : grain === 'week' ? weekStartKey(day) : day
    totals.set(key, (totals.get(key) || 0) + minutesOf(session))
    counts.set(key, (counts.get(key) || 0) + 1)
  })

  const first = days.sort()[0] || ''
  const keys = grain === 'day'
    ? currentWeekKeys(today)
    : first
      ? filledKeys(grain === 'week' ? weekStartKey(first) : first, today, grain, totals)
      : []
  if (keys.length === 0) return []

  return keys.map((key) => {
    const minutes = totals.get(key) || 0
    return {
      key,
      label: bucketLabel(key, grain),
      axis: grain === 'day' ? weekdayName(key) : bucketLabel(key, grain),
      hours: round1(minutes / 60),
      sessions: counts.get(key) || 0,
    }
  })
}

function sharePeriod(grain, today) {
  if (grain === 'day') return { start: today, end: today }
  if (grain === 'week') return { start: weekStartKey(today), end: today }
  return { start: `${today.slice(0, 7)}-01`, end: today }
}

function subjectShare(sessions, grain, today) {
  const { start, end } = sharePeriod(grain || 'week', today)
  const totals = new Map()
  sessions.forEach((session) => {
    const subject = getSubjectById(session.subject)
    const day = dayKeyFromValue(session.startTime)
    if (!subject || !day || day < start || day > end) return
    totals.set(subject.id, (totals.get(subject.id) || 0) + minutesOf(session))
  })

  const total = [...totals.values()].reduce((sum, minutes) => sum + minutes, 0)
  if (total <= 0) return []

  return [...totals.keys()]
    .map((id) => getSubjectById(id))
    .filter((subject) => subject && totals.get(subject.id) > 0)
    .map((subject) => {
      const minutes = totals.get(subject.id)
      const share = round1((minutes / total) * 100)
      return {
        id: subject.id,
        label: subject.name,
        hours: round1(minutes / 60),
        detail: `${share.toFixed(1)}% از مطالعه`,
      }
    })
    .sort((a, b) => b.hours - a.hours)
}

function questionBuckets(tests, grain, today) {
  const totals = new Map()
  const testTotals = new Map()
  const days = []
  tests.forEach((test) => {
    const day = dayKeyFromValue(test.date)
    const count = questionCountOf(test)
    if (!upToToday(day, today) || count <= 0) return
    days.push(day)
    const key = grain === 'month' ? day.slice(0, 7) : grain === 'week' ? weekStartKey(day) : day
    totals.set(key, (totals.get(key) || 0) + count)
    testTotals.set(key, (testTotals.get(key) || 0) + 1)
  })

  const earliest = days.sort()[0] || ''
  const keys = grain === 'day'
    ? currentWeekKeys(today)
    : earliest
      ? filledKeys(grain === 'week' ? weekStartKey(earliest) : earliest, today, grain, totals)
      : []
  if (keys.length === 0) return []

  return keys.map((key) => ({
    key,
    label: bucketLabel(key, grain),
    axis: grain === 'day' ? weekdayName(key) : bucketLabel(key, grain),
    count: totals.get(key) || 0,
    tests: testTotals.get(key) || 0,
  }))
}

function questionSubjects(tests) {
  const totals = new Map()
  tests.forEach((test) => {
    const subject = getSubjectById(test.subject)
    const count = questionCountOf(test)
    if (!subject || count <= 0) return
    totals.set(subject.id, (totals.get(subject.id) || 0) + count)
  })

  return listSubjects()
    .filter((subject) => totals.get(subject.id) > 0)
    .map((subject) => ({
      id: subject.id,
      label: subject.name,
      count: totals.get(subject.id),
    }))
    .sort((a, b) => b.count - a.count)
}

function scoreSeries(tests) {
  const byDay = new Map()
  tests.forEach((test) => {
    const day = dayKeyFromValue(test.date)
    const score = konkoorPercentage(test.correct, test.wrong, test.totalQuestions)
    if (!day || score == null || !getSubjectById(test.subject)) return
    if (!byDay.has(day)) byDay.set(day, {})
    const bucket = byDay.get(day)
    if (!bucket[test.subject]) bucket[test.subject] = []
    bucket[test.subject].push(score)
  })

  const days = [...byDay.keys()].sort()
  const subjects = listSubjects().filter((subject) => days.some((day) => byDay.get(day)[subject.id]))
  const rows = days.map((day) => {
    const row = { key: day, label: formatShortDay(day) }
    subjects.forEach((subject) => {
      const scores = byDay.get(day)[subject.id]
      row[subject.id] = scores
        ? round1(scores.reduce((sum, value) => sum + value, 0) / scores.length)
        : null
    })
    return row
  })

  return { subjects, rows }
}

function topicRows(sessions, tests) {
  const rows = new Map()

  function entry(subjectId, topic) {
    const subject = getSubjectById(subjectId)
    const name = typeof topic === 'string' && topic.trim() ? topic.trim() : 'بدون مبحث'
    const key = `${subjectId}::${name}`
    if (!rows.has(key)) {
      rows.set(key, {
        key,
        topic: name,
        subjectName: subject ? subject.name : subjectId,
        minutes: 0,
        scores: [],
      })
    }
    return rows.get(key)
  }

  sessions.forEach((session) => {
    if (!session?.subject) return
    entry(session.subject, session.topic).minutes += minutesOf(session)
  })

  tests.forEach((test) => {
    const score = konkoorPercentage(test.correct, test.wrong, test.totalQuestions)
    if (!test?.subject || score == null) return
    entry(test.subject, test.topic).scores.push(score)
  })

  return [...rows.values()]
    .map((row) => ({
      key: row.key,
      topic: row.topic,
      subjectName: row.subjectName,
      minutes: row.minutes,
      tests: row.scores.length,
      percentage: row.scores.length
        ? round1(row.scores.reduce((sum, value) => sum + value, 0) / row.scores.length)
        : null,
    }))
    .sort((a, b) => {
      if (a.percentage == null && b.percentage == null) return b.minutes - a.minutes
      if (a.percentage == null) return 1
      if (b.percentage == null) return -1
      if (a.percentage !== b.percentage) return a.percentage - b.percentage
      return b.minutes - a.minutes
    })
}

function causeCounts(mistakes, tests) {
  const ids = new Set(tests.map((test) => test.id))
  const counts = new Map(CAUSES.map((cause) => [cause.id, 0]))
  mistakes.forEach((mistake) => {
    if (!ids.has(mistake.testId) || !counts.has(mistake.cause)) return
    counts.set(mistake.cause, counts.get(mistake.cause) + 1)
  })
  return CAUSES.map((cause) => ({
    id: cause.id,
    label: cause.label,
    count: counts.get(cause.id),
  }))
}

export function studyStreak(sessions, today) {
  const days = new Set(sessions.map((session) => dayKeyFromValue(session.startTime)).filter(Boolean))
  let cursor = today
  if (!days.has(cursor)) {
    cursor = shiftDay(today, -1)
    if (!days.has(cursor)) return 0
  }

  let count = 0
  while (days.has(cursor)) {
    count += 1
    cursor = shiftDay(cursor, -1)
  }
  return count
}

function hourOf(value) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return date.getHours()
}

function bestStudyTime(sessions, tests) {
  const buckets = BUCKETS.map((bucket) => ({ ...bucket, minutes: 0, days: new Set() }))

  sessions.forEach((session) => {
    const hour = hourOf(session.startTime)
    const day = dayKeyFromValue(session.startTime)
    if (hour == null || !day) return
    const bucket = buckets.find((item) => hour >= item.from && hour < item.to)
    if (!bucket) return
    bucket.minutes += minutesOf(session)
    bucket.days.add(day)
  })

  const studied = buckets.filter((bucket) => bucket.minutes > 0)
  if (studied.length === 0) return null

  const best = studied.slice().sort((a, b) => b.minutes - a.minutes)[0]
  const scores = tests
    .map((test) => ({
      day: dayKeyFromValue(test.date),
      score: konkoorPercentage(test.correct, test.wrong, test.totalQuestions),
    }))
    .filter((test) => test.day && test.score != null && best.days.has(test.day))
  const average = scores.length
    ? round1(scores.reduce((sum, test) => sum + test.score, 0) / scores.length)
    : null

  const detail = average == null
    ? `${formatMinutes(best.minutes)} مطالعه کردی.`
    : `${formatMinutes(best.minutes)} مطالعه کردی. میانگین آزمون این روزها ${average.toFixed(1)}٪.`

  return { label: best.label, detail }
}

function recordedSessions(sessions, today) {
  return sessions.filter((session) => upToToday(dayKeyFromValue(session.startTime), today))
}

function recordedTests(tests, today) {
  return tests.filter((test) => upToToday(dayKeyFromValue(test.date), today))
}

function taskDoneDay(task) {
  if (!task?.done) return ''
  return dayKeyFromValue(task.completedAt) || dayKeyFromValue(task.createdAt) || ''
}

function taskSummary(tasks, today) {
  const counts = countTaskBuckets(tasks, today)
  const total = tasks.length
  const done = counts.done
  return {
    ...counts,
    total,
    open: Math.max(0, total - done),
    rate: total > 0 ? Math.round((done / total) * 100) : 0,
  }
}

function taskCompletionBuckets(tasks, grain, today) {
  const totals = new Map()
  const days = []
  tasks.forEach((task) => {
    const day = taskDoneDay(task)
    if (!upToToday(day, today)) return
    days.push(day)
    const key = grain === 'month' ? day.slice(0, 7) : grain === 'week' ? weekStartKey(day) : day
    totals.set(key, (totals.get(key) || 0) + 1)
  })

  const first = days.sort()[0] || ''
  const keys = grain === 'day'
    ? currentWeekKeys(today)
    : first
      ? filledKeys(grain === 'week' ? weekStartKey(first) : first, today, grain, totals)
      : grain === 'day'
        ? currentWeekKeys(today)
        : []
  if (keys.length === 0 && grain === 'day') {
    return currentWeekKeys(today).map((key) => ({
      key,
      label: bucketLabel(key, grain),
      axis: weekdayName(key),
      count: 0,
    }))
  }
  if (keys.length === 0) return []

  return keys.map((key) => ({
    key,
    label: bucketLabel(key, grain),
    axis: grain === 'day' ? weekdayName(key) : bucketLabel(key, grain),
    done: totals.get(key) || 0,
  }))
}

function weekConsistency(sessions, today) {
  const saturday = weekStartKey(today)
  const days = new Set()
  sessions.forEach((session) => {
    const day = dayKeyFromValue(session.startTime)
    if (!day || day < saturday || day > today) return
    if (minutesOf(session) > 0) days.add(day)
  })
  const span = spanDays(saturday, today) + 1
  return {
    hit: days.size,
    span,
    rate: span > 0 ? Math.round((days.size / span) * 100) : 0,
  }
}

function averageScore(tests) {
  const scores = tests
    .map((test) => konkoorPercentage(test.correct, test.wrong, test.totalQuestions))
    .filter((score) => score != null)
  if (scores.length === 0) return null
  return round1(scores.reduce((sum, score) => sum + score, 0) / scores.length)
}

export function buildAnalytics({
  sessions,
  tests,
  mistakes,
  tasks = [],
  goals = {},
  grain,
  questionGrain,
  shareGrain,
  taskGrain,
  today,
}) {
  const countGrain = questionGrain || grain
  const subjectGrain = shareGrain || grain || 'week'
  const doneGrain = taskGrain || grain || 'week'
  const visibleSessions = recordedSessions(sessions, today)
  const visibleTests = recordedTests(tests, today)
  const totalMinutes = visibleSessions.reduce((sum, session) => sum + minutesOf(session), 0)
  const unreviewed = mistakes.filter((mistake) => !mistake.reviewed).length
  const tasksInfo = taskSummary(tasks, today)
  const consistency = weekConsistency(sessions, today)
  const subjectGoals = subjectWeekProgress(sessions, goals, today)
    .filter((row) => row.goal > 0)
    .map((row) => ({
      id: row.id,
      label: row.name,
      done: round1(row.done / 60),
      goal: round1(row.goal / 60),
      ratio: row.ratio == null ? 0 : Math.min(1.35, row.ratio),
      percent: row.ratio == null ? 0 : Math.round(Math.min(135, row.ratio * 100)),
      over: row.over,
      low: row.low,
    }))
    .sort((a, b) => (a.ratio ?? 0) - (b.ratio ?? 0))

  return {
    hours: studyBuckets(visibleSessions, grain, today),
    subjects: subjectShare(visibleSessions, subjectGrain, today),
    questions: questionBuckets(visibleTests, countGrain, today),
    questionSubjects: questionSubjects(visibleTests),
    scores: scoreSeries(visibleTests),
    topics: topicRows(visibleSessions, visibleTests),
    causes: causeCounts(mistakes, visibleTests),
    tasks: taskCompletionBuckets(tasks, doneGrain, today),
    taskSummary: tasksInfo,
    subjectGoals,
    consistency,
    streak: studyStreak(sessions, today),
    best: bestStudyTime(visibleSessions, visibleTests),
    averageScore: averageScore(visibleTests),
    unreviewedMistakes: unreviewed,
    totalMinutes,
    testCount: visibleTests.length,
    questionCount: visibleTests.reduce((sum, test) => sum + questionCountOf(test), 0),
  }
}
