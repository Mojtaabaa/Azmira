import { listSubjects } from '../data/subjects'
import { buildAnalytics, dayKeyFromValue, studyStreak } from './analytics'
import { formatMinutes, todayInputValue } from './clock'
import { saturdayOf, shiftDay } from './week'

function round1(value) {
  return Math.round(value * 10) / 10
}

export function weekRangeLabel(today = todayInputValue()) {
  const start = saturdayOf(today)
  const end = shiftDay(start, 6)
  const fmt = (day) => {
    const [year, month, date] = day.split('-').map(Number)
    return new Date(year, month - 1, date).toLocaleDateString('fa-IR', {
      month: 'short',
      day: 'numeric',
    })
  }
  return `${fmt(start)} تا ${fmt(end)}`
}

export function buildWeeklyReport({ sessions = [], tests = [], mistakes = [], today = todayInputValue() } = {}) {
  const start = saturdayOf(today)
  const end = today < shiftDay(start, 6) ? today : shiftDay(start, 6)
  const weekSessions = sessions.filter((session) => {
    const day = dayKeyFromValue(session.startTime)
    return day && day >= start && day <= end
  })
  const weekTests = tests.filter((test) => {
    const day = dayKeyFromValue(test.date)
    return day && day >= start && day <= end
  })
  const report = buildAnalytics({
    sessions: weekSessions,
    tests: weekTests,
    mistakes,
    grain: 'day',
    questionGrain: 'day',
    shareGrain: 'week',
    today: end,
  })
  const bySubject = listSubjects().map((subject) => {
    const minutes = weekSessions
      .filter((session) => session.subject === subject.id)
      .reduce((sum, session) => sum + (Number(session.durationMinutes) || 0), 0)
    return { id: subject.id, name: subject.name, minutes, color: subject.color }
  }).filter((row) => row.minutes > 0)
    .sort((a, b) => b.minutes - a.minutes)

  return {
    rangeLabel: weekRangeLabel(today),
    streak: studyStreak(sessions, today),
    totalMinutes: weekSessions.reduce((sum, session) => sum + (Number(session.durationMinutes) || 0), 0),
    testCount: weekTests.length,
    questionCount: report.questionCount,
    subjects: bySubject,
    hours: report.hours,
  }
}

function fillRoundRect(ctx, x, y, w, h, r) {
  const radius = Math.min(r, w / 2, h / 2)
  ctx.beginPath()
  ctx.moveTo(x + radius, y)
  ctx.arcTo(x + w, y, x + w, y + h, radius)
  ctx.arcTo(x + w, y + h, x, y + h, radius)
  ctx.arcTo(x, y + h, x, y, radius)
  ctx.arcTo(x, y, x + w, y, radius)
  ctx.closePath()
  ctx.fill()
}

export async function downloadWeeklyReportPng(data, { fileName } = {}) {
  const width = 900
  const height = 1180
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error('canvas')

  const bg = '#121211'
  const surface = '#1b1b19'
  const text = '#f0efec'
  const muted = '#9a9890'
  const accent = '#e8924a'
  const line = '#2c2c29'

  ctx.fillStyle = bg
  ctx.fillRect(0, 0, width, height)

  ctx.fillStyle = surface
  fillRoundRect(ctx, 40, 40, width - 80, height - 80, 28)

  ctx.fillStyle = accent
  ctx.font = '700 42px Estedad, Vazirmatn, Tahoma, sans-serif'
  ctx.textAlign = 'right'
  ctx.direction = 'rtl'
  ctx.fillText('رتبه', width - 72, 120)

  ctx.fillStyle = text
  ctx.font = '700 36px Estedad, Vazirmatn, Tahoma, sans-serif'
  ctx.fillText('گزارش هفته', width - 72, 175)

  ctx.fillStyle = muted
  ctx.font = '500 22px Estedad, Vazirmatn, Tahoma, sans-serif'
  ctx.fillText(data.rangeLabel, width - 72, 215)

  const cards = [
    { label: 'مطالعه', value: formatMinutes(data.totalMinutes) },
    { label: 'روز پیاپی', value: `${data.streak} روز` },
    { label: 'آزمون', value: String(data.testCount) },
    { label: 'تست', value: String(data.questionCount) },
  ]
  cards.forEach((card, index) => {
    const x = 72 + (index % 2) * 380
    const y = 260 + Math.floor(index / 2) * 150
    ctx.fillStyle = bg
    fillRoundRect(ctx, x, y, 350, 120, 18)
    ctx.fillStyle = muted
    ctx.font = '500 20px Estedad, Vazirmatn, Tahoma, sans-serif'
    ctx.fillText(card.label, x + 320, y + 42)
    ctx.fillStyle = text
    ctx.font = '700 34px Estedad, Vazirmatn, Tahoma, sans-serif'
    ctx.fillText(card.value, x + 320, y + 88)
  })

  ctx.fillStyle = text
  ctx.font = '700 26px Estedad, Vazirmatn, Tahoma, sans-serif'
  ctx.fillText('سهم درس‌ها', width - 72, 600)

  const maxMinutes = Math.max(...data.subjects.map((row) => row.minutes), 1)
  data.subjects.slice(0, 6).forEach((row, index) => {
    const y = 640 + index * 70
    const barWidth = Math.max(12, (row.minutes / maxMinutes) * 520)
    ctx.fillStyle = muted
    ctx.font = '500 20px Estedad, Vazirmatn, Tahoma, sans-serif'
    ctx.fillText(row.name, width - 72, y)
    ctx.fillStyle = line
    fillRoundRect(ctx, 72, y + 12, 520, 18, 9)
    ctx.fillStyle = row.color || accent
    fillRoundRect(ctx, 72 + (520 - barWidth), y + 12, barWidth, 18, 9)
    ctx.fillStyle = text
    ctx.font = '600 18px Estedad, Vazirmatn, Tahoma, sans-serif'
    ctx.fillText(`${round1(row.minutes / 60)} س`, 72 + 500, y)
  })

  if (data.subjects.length === 0) {
    ctx.fillStyle = muted
    ctx.font = '500 20px Estedad, Vazirmatn, Tahoma, sans-serif'
    ctx.fillText('این هفته جلسه‌ای ثبت نشده.', width - 72, 660)
  }

  ctx.fillStyle = muted
  ctx.font = '500 18px Estedad, Vazirmatn, Tahoma, sans-serif'
  ctx.fillText('ساخته‌شده در رتبه', width - 72, height - 70)

  const blob = await new Promise((resolve, reject) => {
    canvas.toBlob((value) => (value ? resolve(value) : reject(new Error('blob'))), 'image/png')
  })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName || `rotbe-week-${todayInputValue()}.png`
  link.click()
  URL.revokeObjectURL(url)
}
