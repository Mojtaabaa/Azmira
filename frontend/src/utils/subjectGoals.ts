// @ts-nocheck
import { getSubjectById, listSubjects } from '../data/subjects'
import { formatHourCount, formatMinutes, todayInputValue } from './clock'
import { saturdayOf, shiftDay } from './week'

function minutesOf(session) {
  const value = Number(session?.durationMinutes)
  return Number.isFinite(value) && value > 0 ? value : 0
}

function dayKeyFromValue(value) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function subjectWeeklyGoalMinutes(goals, subjectId) {
  const value = Number(goals?.[subjectId])
  if (!Number.isInteger(value) || value < 15) return 0
  return value
}

export function weekSubjectMinutes(sessions, today = todayInputValue()) {
  const start = saturdayOf(today)
  const end = shiftDay(start, 6)
  const totals = new Map()

  sessions.forEach((session) => {
    const day = dayKeyFromValue(session.startTime)
    if (!day || day < start || day > end) return
    if (!session.subject || !getSubjectById(session.subject)) return
    totals.set(session.subject, (totals.get(session.subject) || 0) + minutesOf(session))
  })

  return totals
}

export function subjectWeekProgress(sessions, goals, today = todayInputValue()) {
  const studied = weekSubjectMinutes(sessions, today)
  return listSubjects().map((subject) => {
    const goal = subjectWeeklyGoalMinutes(goals, subject.id)
    const done = studied.get(subject.id) || 0
    const ratio = goal > 0 ? done / goal : null
    return {
      id: subject.id,
      name: subject.name,
      goal,
      done,
      ratio,
      remaining: goal > 0 ? Math.max(0, goal - done) : 0,
      over: goal > 0 && done >= goal,
      low: goal > 0 && done < goal * 0.35,
    }
  })
}

export function studyGoalAdvice(subjectId, sessions, goals, today = todayInputValue()) {
  const rows = subjectWeekProgress(sessions, goals, today)
  const current = rows.find((row) => row.id === subjectId)
  if (!current) return null

  if (current.goal > 0 && current.over) {
    return {
      tone: 'over',
      message: `${current.name} این هفته ${formatMinutes(current.done)} مطالعه کردی و از هدف ${formatHourCount(current.goal)} ساعت رد شدی. بهتر است درس دیگری را برداری.`,
    }
  }

  // Already catching up on a weak/unstudied subject — don't redirect to another weak one.
  if (current.goal > 0 && current.low) {
    return {
      tone: 'ok',
      message: `${current.name} این هفته هنوز عقب است (${formatMinutes(current.done)} از ${formatHourCount(current.goal)} ساعت). همین را جلو ببر.`,
    }
  }

  const neglected = rows
    .filter((row) => row.id !== subjectId && row.goal > 0 && row.low)
    .sort((a, b) => (a.ratio ?? 1) - (b.ratio ?? 1) || b.remaining - a.remaining)

  if (neglected[0]) {
    const row = neglected[0]
    return {
      tone: 'nudge',
      message: `${row.name} این هفته فقط ${formatMinutes(row.done)} از ${formatHourCount(row.goal)} ساعت است. شاید اول آن را جلو ببری.`,
    }
  }

  if (current.goal > 0) {
    return {
      tone: 'ok',
      message: `${current.name}: ${formatMinutes(current.done)} از ${formatHourCount(current.goal)} ساعت هدف این هفته.`,
    }
  }

  return null
}
