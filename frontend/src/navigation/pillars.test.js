import assert from 'node:assert/strict'
import test from 'node:test'
import {
  MOBILE_PILLAR_PATHS,
  NAV_GROUPS,
  PILLARS,
  findActivePillar,
  listPillarIds,
} from './pillars.js'

test('exposes four pillars', () => {
  assert.deepEqual(listPillarIds().sort(), ['achieve', 'improve', 'learn', 'plan'])
  assert.equal(PILLARS.plan.labelEn, 'PLAN')
  assert.equal(PILLARS.learn.labelEn, 'LEARN')
  assert.equal(PILLARS.improve.labelEn, 'IMPROVE')
  assert.equal(PILLARS.achieve.labelEn, 'ACHIEVE')
})

test('nav groups cover pillar hubs without duplicate routes', () => {
  const routes = NAV_GROUPS.flatMap((g) => g.items.map((i) => i.to))
  assert.equal(new Set(routes).size, routes.length)
  assert.ok(routes.includes('/'))
  assert.ok(routes.includes('/learn'))
  assert.ok(routes.includes('/improve'))
  assert.ok(routes.includes('/achieve'))
  assert.ok(routes.includes('/planner'))
  assert.ok(routes.includes('/mastery'))
  assert.ok(routes.includes('/journey'))
  assert.ok(routes.includes('/progress'))
  assert.ok(routes.includes('/missions'))
  assert.ok(routes.includes('/improvement'))
})

test('improvement routes resolve to IMPROVE pillar', () => {
  assert.equal(findActivePillar('/improvement'), 'improve')
  assert.equal(findActivePillar('/ai-coach'), 'improve')
})

test('mobile tabs map to four pillar hubs', () => {
  assert.deepEqual(MOBILE_PILLAR_PATHS, ['/', '/learn', '/improve', '/achieve'])
})

test('findActivePillar resolves functional routes', () => {
  assert.equal(findActivePillar('/'), 'plan')
  assert.equal(findActivePillar('/timer'), 'plan')
  assert.equal(findActivePillar('/reviews'), 'learn')
  assert.equal(findActivePillar('/journey'), 'learn')
  assert.equal(findActivePillar('/analytics'), 'improve')
  assert.equal(findActivePillar('/store'), 'achieve')
  assert.equal(findActivePillar('/settings'), null)
})
