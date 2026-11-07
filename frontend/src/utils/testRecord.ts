// @ts-nocheck
import { getSubjectById } from '../data/subjects'
import { isFutureDate, isRealDateString } from './clock'
import { formatKonkoorPercentage } from './score'

export const ANSWER_CHOICES = ['1', '2', '3', '4']
const MAX_QUESTIONS = 120
const MAX_NUMBER = 999
const MAX_MINUTES = 1440

export function parseWholeNumber(value) {
  if (typeof value === 'number') {
    if (!Number.isInteger(value) || value < 0) return null
    return value
  }
  if (typeof value !== 'string' || !/^\d+$/.test(value.trim())) return null
  return Number(value.trim())
}

export function normalizeQuestions(value) {
  if (!Array.isArray(value)) return []
  const seen = new Set()
  const next = []

  value.forEach((item) => {
    if (!item || typeof item !== 'object') return
    const number = parseWholeNumber(typeof item.number === 'number' ? item.number : String(item.number ?? ''))
    const choice = String(item.choice ?? '')
    if (number == null || number < 1 || number > MAX_NUMBER || seen.has(number)) return
    if (!ANSWER_CHOICES.includes(choice) && choice !== 'blank') return
    seen.add(number)
    const mark = choice === 'blank' ? 'blank' : item.mark === 'right' || item.mark === 'wrong' ? item.mark : null
    next.push({ number, choice, mark })
  })

  next.sort((a, b) => a.number - b.number)
  return next
}

export function addAnswer(questions, number, choice) {
  const current = normalizeQuestions(questions)
  if (!ANSWER_CHOICES.includes(choice) && choice !== 'blank') return { error: 'یک گزینه انتخاب کن.' }
  if (!Number.isInteger(number) || number < 1) return { error: 'اول شماره سوال را بنویس.' }
  if (number > MAX_NUMBER) return { error: `شماره سوال نمی‌تواند بیشتر از ${MAX_NUMBER} باشد.` }
  if (current.length >= MAX_QUESTIONS && !current.some((item) => item.number === number)) {
    return { error: `هر آزمون حداکثر ${MAX_QUESTIONS} سوال دارد.` }
  }

  const exists = current.some((item) => item.number === number)
  const next = exists
    ? current.map((item) => {
        if (item.number !== number) return item
        return {
          number,
          choice,
          mark: choice === 'blank' ? 'blank' : item.choice === choice ? item.mark : null,
        }
      })
    : [...current, { number, choice, mark: choice === 'blank' ? 'blank' : null }]

  next.sort((a, b) => a.number - b.number)
  return { questions: next, updated: exists }
}

export function setAnswerMark(questions, number, mark) {
  if (mark !== 'right' && mark !== 'wrong') return normalizeQuestions(questions)
  return normalizeQuestions(questions).map((item) => {
    if (item.number !== number || item.choice === 'blank') return item
    return { ...item, mark }
  })
}

export function removeAnswer(questions, number) {
  return normalizeQuestions(questions).filter((item) => item.number !== number)
}

export function tally(questions) {
  const list = normalizeQuestions(questions)
  const correct = list.filter((item) => item.mark === 'right').length
  const wrong = list.filter((item) => item.mark === 'wrong').length
  const blank = list.filter((item) => item.mark === 'blank').length
  const open = list.length - correct - wrong - blank
  return {
    total: list.length,
    correct,
    wrong,
    blank,
    open,
    score: open === 0 && list.length > 0 ? formatKonkoorPercentage(correct, wrong, list.length) : null,
  }
}

export function buildSession(meta, questions) {
  const date = typeof meta.date === 'string' ? meta.date.trim() : ''
  const subject = typeof meta.subject === 'string' ? meta.subject.trim() : ''
  const topic = typeof meta.topic === 'string' ? meta.topic.trim() : ''
  const source = typeof meta.source === 'string' ? meta.source.trim() : ''
  const list = normalizeQuestions(questions)
  const counts = tally(list)

  if (!date) return { error: 'تاریخ را انتخاب کن.' }
  if (!isRealDateString(date)) return { error: 'یک تاریخ واقعی وارد کن.' }
  if (isFutureDate(date)) return { error: 'تاریخ نمی‌تواند در آینده باشد.' }
  if (!getSubjectById(subject)) return { error: 'درس را انتخاب کن.' }
  if (!topic) return { error: 'مبحث را بنویس.' }
  if (!source) return { error: 'منبع را بنویس.' }
  if (list.length < 1) return { error: 'حداقل یک سوال اضافه کن.' }
  if (counts.open > 0) return { error: 'قبل از ذخیره، همه پاسخ‌ها را درست یا غلط علامت بزن.' }

  const duration = parseWholeNumber(meta.duration)
  if (duration == null || duration < 1) return { error: 'دقیقه باید یک عدد درست مثبت باشد.' }
  if (duration > MAX_MINUTES) return { error: `دقیقه نمی‌تواند بیشتر از ${MAX_MINUTES} باشد.` }

  return {
    test: {
      date,
      subject,
      topic,
      source,
      totalQuestions: counts.total,
      correct: counts.correct,
      wrong: counts.wrong,
      blank: counts.blank,
      durationMinutes: duration,
      questions: list,
    },
  }
}

function countField(value, label) {
  if (value === '' || value == null) return { value: 0 }
  const count = parseWholeNumber(typeof value === 'number' ? value : String(value))
  if (count == null) return { error: `${label} باید یک عدد درست باشد.` }
  return { value: count }
}

export function buildManualTest(meta) {
  const date = typeof meta.date === 'string' ? meta.date.trim() : ''
  const subject = typeof meta.subject === 'string' ? meta.subject.trim() : ''
  const topic = typeof meta.topic === 'string' ? meta.topic.trim() : ''
  const source = typeof meta.source === 'string' ? meta.source.trim() : ''

  if (!date) return { error: 'تاریخ را انتخاب کن.' }
  if (!isRealDateString(date)) return { error: 'یک تاریخ واقعی وارد کن.' }
  if (isFutureDate(date)) return { error: 'تاریخ نمی‌تواند در آینده باشد.' }
  if (!getSubjectById(subject)) return { error: 'درس را انتخاب کن.' }
  if (!topic) return { error: 'مبحث را بنویس.' }
  if (!source) return { error: 'منبع را بنویس.' }

  const correct = countField(meta.correct, 'درست')
  if (correct.error) return { error: correct.error }
  const wrong = countField(meta.wrong, 'غلط')
  if (wrong.error) return { error: wrong.error }
  const blank = countField(meta.blank, 'سفید')
  if (blank.error) return { error: blank.error }

  const total = correct.value + wrong.value + blank.value
  if (total < 1) return { error: 'حداقل یک تست بنویس.' }
  if (total > MAX_NUMBER) return { error: `تعداد تست نمی‌تواند بیشتر از ${MAX_NUMBER} باشد.` }

  const duration = parseWholeNumber(meta.duration)
  if (duration == null || duration < 1) return { error: 'دقیقه باید یک عدد درست مثبت باشد.' }
  if (duration > MAX_MINUTES) return { error: `دقیقه نمی‌تواند بیشتر از ${MAX_MINUTES} باشد.` }

  return {
    test: {
      date,
      subject,
      topic,
      source,
      totalQuestions: total,
      correct: correct.value,
      wrong: wrong.value,
      blank: blank.value,
      durationMinutes: duration,
      questions: [],
      entry: 'manual',
    },
  }
}
