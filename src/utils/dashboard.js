import { getSubjectById, listSubjects } from '../data/subjects'
import { isSameLocalDay, todayInputValue } from './clock'
import { minutesOfTime } from './events'
import { konkoorPercentage } from './score'
import { taskBucket, tasksForFilter } from './tasks'
import { saturdayOf, shiftDay } from './week'

function round1(value) {
  return Math.round(value * 10) / 10
}

function minutesOf(session) {
  const value = Number(session?.durationMinutes)
  return Number.isFinite(value) && value > 0 ? value : 0
}

function scoreOf(test) {
  return konkoorPercentage(test?.correct, test?.wrong, test?.totalQuestions)
}

export function todayStudyMinutes(sessions, now = new Date()) {
  return sessions.reduce((sum, session) => (
    isSameLocalDay(session.startTime, now) ? sum + minutesOf(session) : sum
  ), 0)
}

export function recentTests(tests, limit = 5) {
  return tests
    .slice()
    .sort((a, b) => String(b.date).localeCompare(String(a.date)) || String(b.id).localeCompare(String(a.id)))
    .slice(0, limit)
}

export function unreviewedMistakes(mistakes) {
  return mistakes.filter((mistake) => mistake?.reviewed !== true)
}

export function subjectWeight(weights, subjectId) {
  const saved = weights?.[subjectId]
  return Number.isInteger(saved) ? saved : 1
}

export function rankProjection(tests, weights) {
  const buckets = new Map()
  tests.forEach((test) => {
    const score = scoreOf(test)
    if (!test?.subject || score == null || !getSubjectById(test.subject)) return
    const bucket = buckets.get(test.subject) || []
    bucket.push(score)
    buckets.set(test.subject, bucket)
  })

  const rows = listSubjects().map((subject) => {
    const scores = buckets.get(subject.id) || []
    const weight = subjectWeight(weights, subject.id)
    return {
      id: subject.id,
      name: subject.name,
      weight,
      tests: scores.length,
      average: scores.length
        ? round1(scores.reduce((sum, value) => sum + value, 0) / scores.length)
        : null,
    }
  }).filter((row) => row.tests > 0 || row.weight !== 1)

  const used = rows.filter((row) => row.average != null && row.weight > 0)
  const weightSum = used.reduce((sum, row) => sum + row.weight, 0)
  const score = weightSum > 0
    ? round1(used.reduce((sum, row) => sum + row.average * row.weight, 0) / weightSum)
    : null

  return { score, rows, weightSum }
}

function inSpan(day, start, end) {
  return Boolean(day) && day >= start && day <= end
}

function eventMinutes(event, dailyGoal) {
  const start = minutesOfTime(event.startTime)
  const end = minutesOfTime(event.endTime)
  if (start != null && end != null && end > start) return end - start
  if (!event.startTime && !event.endTime) return dailyGoal
  return 0
}

export function weekWindow(today = todayInputValue()) {
  const start = saturdayOf(today)
  return { start, end: shiftDay(start, 6), previousStart: shiftDay(start, -7), previousEnd: shiftDay(start, -1) }
}

export function weekReview({ sessions, tests, events, dailyGoal, weeklyGoal, today = todayInputValue() }) {
  const window = weekWindow(today)
  const activeDays = new Set()
  const studied = sessions.reduce((sum, session) => {
    const day = todayInputValue(new Date(session.startTime))
    if (Number.isNaN(new Date(session.startTime).getTime())) return sum
    if (!inSpan(day, window.start, window.end)) return sum
    activeDays.add(day)
    return sum + minutesOf(session)
  }, 0)

  const studyEvents = events.filter((event) => event?.type === 'study' && inSpan(event.date, window.start, window.end))
  const plannerMinutes = studyEvents.reduce((sum, event) => sum + eventMinutes(event, dailyGoal), 0)
  const fromPlanner = studyEvents.length > 0 && plannerMinutes > 0
  const planned = Number.isInteger(weeklyGoal) && weeklyGoal > 0 ? weeklyGoal : dailyGoal * 7

  const thisTests = tests.filter((test) => inSpan(test.date, window.start, window.end))
  const lastTests = tests.filter((test) => inSpan(test.date, window.previousStart, window.previousEnd))

  return {
    ...window,
    studied,
    daysHit: activeDays.size,
    planned,
    plannerMinutes,
    fromPlanner,
    tests: thisTests,
    thisAverage: averageOf(thisTests),
    lastAverage: averageOf(lastTests),
  }
}

function averageOf(tests) {
  const scores = tests.map(scoreOf).filter((score) => score != null)
  if (scores.length === 0) return null
  return round1(scores.reduce((sum, score) => sum + score, 0) / scores.length)
}

export function weakestTopics(sessions, tests, limit = 3) {
  const rows = new Map()

  function entry(subjectId, topic) {
    const name = typeof topic === 'string' ? topic.trim() : ''
    if (!subjectId || !name || !getSubjectById(subjectId)) return null
    const key = `${subjectId}::${name}`
    if (!rows.has(key)) {
      rows.set(key, {
        key,
        topic: name,
        subjectId,
        subjectName: getSubjectById(subjectId).name,
        scores: [],
      })
    }
    return rows.get(key)
  }

  sessions.forEach((session) => entry(session.subject, session.topic))
  tests.forEach((test) => {
    const row = entry(test.subject, test.topic)
    const score = scoreOf(test)
    if (row && score != null) row.scores.push(score)
  })

  return [...rows.values()]
    .map((row) => ({
      ...row,
      percentage: row.scores.length
        ? round1(row.scores.reduce((sum, score) => sum + score, 0) / row.scores.length)
        : null,
    }))
    .filter((row) => row.percentage != null)
    .sort((a, b) => a.percentage - b.percentage || a.topic.localeCompare(b.topic, 'fa'))
    .slice(0, limit)
}

export function todayTasks(tasks, today) {
  return tasksForFilter(tasks, 'today', today)
}

export function overdueCount(tasks, today) {
  return tasks.filter((task) => taskBucket(task, today) === 'overdue').length
}
