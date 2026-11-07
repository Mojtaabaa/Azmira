// @ts-nocheck
import { getSubjectById, listSubjects } from '../data/subjects'

export const TOPIC_STATUSES = [
  { id: 'new', label: 'نخوانده' },
  { id: 'learning', label: 'در حال خواندن' },
  { id: 'done', label: 'تمام' },
]

export function normalizeTopicName(name) {
  return String(name || '').trim().replace(/\s+/g, ' ')
}

function topicKey(name) {
  return normalizeTopicName(name).toLocaleLowerCase('fa')
}

export function sameTopicName(a, b) {
  return topicKey(a) === topicKey(b)
}

export function topicHideKey(subjectId, name) {
  return `${subjectId}::${topicKey(name)}`
}

export function isTopicHidden(subjectId, name, hiddenTopics = []) {
  if (!subjectId || !name) return false
  const key = topicHideKey(subjectId, name)
  return hiddenTopics.some((item) => item === key)
}

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

/** Saved topics plus names used in sessions/tests, grouped for the topics page. */
export function managedTopicsBySubject(topics, sessions = [], tests = [], hiddenTopics = []) {
  const hidden = new Set(hiddenTopics)
  return listSubjects()
    .map((subject) => {
      const bag = new Map()

      topics.forEach((topic) => {
        if (topic.subject !== subject.id) return
        const name = normalizeTopicName(topic.name)
        if (!name) return
        const key = topicKey(name)
        if (hidden.has(`${subject.id}::${key}`)) return
        const current = bag.get(key)
        if (!current) {
          bag.set(key, {
            key: `${subject.id}::${key}`,
            id: topic.id,
            ids: [topic.id],
            subject: subject.id,
            name,
            status: topic.status || 'new',
            saved: true,
          })
          return
        }
        current.ids.push(topic.id)
        if (!current.id) current.id = topic.id
        current.saved = true
        current.status = topic.status || current.status
        current.name = name
      })

      function addHistory(raw) {
        const name = normalizeTopicName(raw)
        if (!name) return
        const key = topicKey(name)
        if (hidden.has(`${subject.id}::${key}`)) return
        const current = bag.get(key)
        if (!current) {
          bag.set(key, {
            key: `${subject.id}::${key}`,
            id: null,
            ids: [],
            subject: subject.id,
            name,
            status: 'new',
            saved: false,
          })
          return
        }
        if (!current.saved) current.name = current.name || name
      }

      sessions.forEach((session) => {
        if (session.subject === subject.id) addHistory(session.topic)
      })
      tests.forEach((test) => {
        if (test.subject === subject.id) addHistory(test.topic)
      })

      return {
        subject,
        topics: [...bag.values()].sort((a, b) => a.name.localeCompare(b.name, 'fa')),
      }
    })
    .filter((group) => group.topics.length > 0)
}

export function studiedTopics(sessions, tests, saved, hiddenTopics = []) {
  const hidden = new Set(hiddenTopics)
  const seen = new Set(saved.map((topic) => `${topic.subject}::${topicKey(topic.name)}`))
  const found = new Map()

  function add(subject, topic) {
    const name = normalizeTopicName(topic)
    if (!subject || !name || !getSubjectById(subject)) return
    const key = `${subject}::${topicKey(name)}`
    if (hidden.has(key) || seen.has(key) || found.has(key)) return
    found.set(key, { subject, name })
  }

  sessions.forEach((session) => add(session.subject, session.topic))
  tests.forEach((test) => add(test.subject, test.topic))
  return [...found.values()]
}

/** Known topic names for one subject, most-used first (topics list + sessions + tests). */
export function topicNamesForSubject(subjectId, { topics = [], sessions = [], tests = [], hiddenTopics = [] } = {}) {
  if (!subjectId || !getSubjectById(subjectId)) return []
  const hidden = new Set(hiddenTopics)
  const bag = new Map()

  function add(raw, weight = 1, preferName = false) {
    const name = normalizeTopicName(raw)
    if (!name) return
    const key = topicKey(name)
    if (hidden.has(`${subjectId}::${key}`)) return
    const current = bag.get(key)
    if (!current) {
      bag.set(key, { name, count: weight })
      return
    }
    current.count += weight
    if (preferName) current.name = name
  }

  topics.forEach((topic) => {
    if (topic.subject === subjectId) add(topic.name, 5, true)
  })
  sessions.forEach((session) => {
    if (session.subject === subjectId) add(session.topic, 2)
  })
  tests.forEach((test) => {
    if (test.subject === subjectId) add(test.topic, 2)
  })
  ;(getSubjectById(subjectId)?.topics || []).forEach((name) => add(name, 1, true))

  return [...bag.values()]
    .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name, 'fa'))
    .map((row) => row.name)
}

/** Snap free text onto an existing name when it only differs by spaces/case. */
export function resolveTopicName(raw, knownNames = []) {
  const name = normalizeTopicName(raw)
  if (!name) return ''
  const key = topicKey(name)
  const hit = knownNames.find((item) => topicKey(item) === key)
  return hit || name
}

export function buildTopic(input, existing) {
  const name = normalizeTopicName(input.name)
  const subject = typeof input.subject === 'string' ? input.subject.trim() : ''
  const status = TOPIC_STATUSES.some((item) => item.id === input.status) ? input.status : 'new'

  if (!getSubjectById(subject)) return { error: 'درس را بنویس.' }
  if (!name) return { error: 'مبحث را بنویس.' }
  if (name.length > 80) return { error: 'مبحث نمی‌تواند بیشتر از ۸۰ نویسه باشد.' }
  if (existing.some((topic) => topic.subject === subject && topicKey(topic.name) === topicKey(name))) {
    return { error: 'این مبحث را قبلاً نوشته‌ای.' }
  }

  return { topic: { subject, name, status } }
}
