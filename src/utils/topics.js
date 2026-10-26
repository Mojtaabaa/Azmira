import { getSubjectById, listSubjects } from '../data/subjects'

export const TOPIC_STATUSES = [
  { id: 'new', label: 'نخوانده' },
  { id: 'learning', label: 'در حال خواندن' },
  { id: 'done', label: 'تمام' },
]

const CREDIT = {
  new: 0,
  learning: 0.5,
  done: 1,
}

export function nextTopicStatus(status) {
  const index = TOPIC_STATUSES.findIndex((item) => item.id === status)
  return TOPIC_STATUSES[(index + 1) % TOPIC_STATUSES.length].id
}

export function topicStatusLabel(status) {
  return TOPIC_STATUSES.find((item) => item.id === status)?.label || TOPIC_STATUSES[0].label
}

export function coverageOf(topics) {
  if (!topics.length) return 0
  const credit = topics.reduce((sum, topic) => sum + (CREDIT[topic.status] || 0), 0)
  return Math.round((credit / topics.length) * 100)
}

export function topicsBySubject(topics) {
  return listSubjects()
    .map((subject) => ({
      subject,
      topics: topics
        .filter((topic) => topic.subject === subject.id)
        .slice()
        .sort((a, b) => String(a.name).localeCompare(String(b.name), 'fa')),
    }))
    .filter((group) => group.topics.length > 0)
}

export function studiedTopics(sessions, tests, saved) {
  const seen = new Set(saved.map((topic) => `${topic.subject}::${String(topic.name).trim()}`))
  const found = new Map()

  function add(subject, topic) {
    const name = typeof topic === 'string' ? topic.trim() : ''
    if (!subject || !name || !getSubjectById(subject)) return
    const key = `${subject}::${name}`
    if (seen.has(key) || found.has(key)) return
    found.set(key, { subject, name })
  }

  sessions.forEach((session) => add(session.subject, session.topic))
  tests.forEach((test) => add(test.subject, test.topic))
  return [...found.values()]
}

export function buildTopic(input, existing) {
  const name = typeof input.name === 'string' ? input.name.trim().replace(/\s+/g, ' ') : ''
  const subject = typeof input.subject === 'string' ? input.subject.trim() : ''
  const status = TOPIC_STATUSES.some((item) => item.id === input.status) ? input.status : 'new'

  if (!getSubjectById(subject)) return { error: 'درس را بنویس.' }
  if (!name) return { error: 'مبحث را بنویس.' }
  if (name.length > 80) return { error: 'مبحث نمی‌تواند بیشتر از 80 نویسه باشد.' }
  if (existing.some((topic) => topic.subject === subject && topic.name === name)) {
    return { error: 'این مبحث را قبلاً نوشته‌ای.' }
  }

  return { topic: { subject, name, status } }
}
