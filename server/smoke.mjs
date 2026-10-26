import http from 'node:http'
import { createApp } from './app.js'

process.env.KONKOOR_DB = ':memory:'
const app = createApp()
const server = http.createServer(app).listen(0, '127.0.0.1')
await new Promise((resolve) => server.once('listening', resolve))
const base = `http://127.0.0.1:${server.address().port}/api`

let cookie = ''
async function call(method, path, body, as = cookie) {
  const response = await fetch(base + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(as ? { Cookie: as } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const set = response.headers.get('set-cookie')
  if (set) cookie = set.split(';')[0]
  let json = null
  try {
    json = await response.json()
  } catch {
    json = null
  }
  return { status: response.status, json }
}

const results = []
function check(name, ok) {
  results.push(`${ok ? 'PASS' : 'FAIL'} ${name}`)
}

check('me anon 401', (await call('GET', '/auth/me')).status === 401)
check('bad username 400', (await call('POST', '/auth/register', { username: 'a', password: '12345678' })).status === 400)
check('short password 400', (await call('POST', '/auth/register', { username: 'alice', password: '123' })).status === 400)
check('register 201', (await call('POST', '/auth/register', { username: 'alice', password: 'password1' })).status === 201)
check('dup username 409', (await call('POST', '/auth/register', { username: 'ALICE', password: 'password1' }, '')).status === 409)
check('me ok', (await call('GET', '/auth/me')).json?.user?.username === 'alice')
const aliceCookie = cookie
check('add task', (await call('POST', '/data/tasks', { id: 't1', title: 'x', done: false })).status === 201)
check('dup id 409', (await call('POST', '/data/tasks', { id: 't1', title: 'x' })).status === 409)
check('patch', (await call('PATCH', '/data/tasks/t1', { id: 't1', title: 'y', done: true })).status === 200)
check('unknown collection 404', (await call('POST', '/data/nope', { id: 'a' })).status === 404)
check('settings bad 400', (await call('PUT', '/settings', { settings: { dailyGoalMinutes: 7 } })).status === 400)
check('settings ok', (await call('PUT', '/settings', { settings: { dailyGoalMinutes: 120 } })).status === 200)
const mine = await call('GET', '/data')
check('load data', mine.json.tasks.length === 1 && mine.json.tasks[0].title === 'y' && mine.json.settings.dailyGoalMinutes === 120)

cookie = ''
check('register bob', (await call('POST', '/auth/register', { username: 'bob', password: 'password2' })).status === 201)
const bobData = await call('GET', '/data')
check('bob isolated', bobData.json.tasks.length === 0 && bobData.json.settings.dailyGoalMinutes === 240)
check('bob cannot patch alice task', (await call('PATCH', '/data/tasks/t1', { id: 't1', title: 'hack' })).status === 404)
check('bob cannot delete alice task', (await call('DELETE', '/data/tasks/t1')).status === 404)
check('anon data 401', (await call('GET', '/data', undefined, '')).status === 401)

cookie = aliceCookie
const again = await call('GET', '/data')
check('alice intact', again.json.tasks[0].title === 'y')
check('replace all', (await call('PUT', '/data', { data: { ...again.json, tasks: [] } })).status === 200)
check('replace result', (await call('GET', '/data')).json.tasks.length === 0)
check('logout', (await call('POST', '/auth/logout')).status === 200)
check('after logout 401', (await call('GET', '/data')).status === 401)
check('login wrong 401', (await call('POST', '/auth/login', { username: 'alice', password: 'wrongpass' }, '')).status === 401)
check('login ok', (await call('POST', '/auth/login', { username: 'Alice', password: 'password1' }, '')).status === 200)
const bad = await fetch(base + '/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', Origin: 'http://evil.example' },
  body: JSON.stringify({ username: 'alice', password: 'password1' }),
})
check('foreign origin 403', bad.status === 403)

console.log(results.join('\n'))
server.close()
process.exit(results.some((line) => line.startsWith('FAIL')) ? 1 : 0)
