import assert from 'node:assert/strict'
import test from 'node:test'
import { recommendNext } from './recommendNext.js'

test('prioritizes overdue tasks over decorative goals', () => {
  const next = recommendNext({ overdue: 2, openMistakes: 5, reviewsDue: 3, goal: 60, studied: 0 })
  assert.equal(next.reason, 'overdue_tasks')
  assert.equal(next.to, '/tasks?filter=overdue')
})

test('uses reviews due when available', () => {
  const next = recommendNext({ overdue: 0, reviewsDue: 4, openMistakes: 1 })
  assert.equal(next.reason, 'reviews_due')
})

test('does not invent reviews when null', () => {
  const next = recommendNext({ overdue: 0, reviewsDue: null, openMistakes: 0, goal: 0, openTasks: 0 })
  assert.equal(next.reason, 'planner')
})
