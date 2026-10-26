import express from 'express'
import { COLLECTION_KEYS } from '../src/data/defaults.js'
import {
  isPlainObject,
  normalizeCollection,
  normalizeData,
  normalizeSettings,
} from '../src/shared/normalize.js'
import {
  DUMMY_HASH,
  PASSWORD_MAX,
  PASSWORD_MIN,
  USERNAME_PATTERN,
  clearSessionCookie,
  createLimiter,
  hashPassword,
  hashToken,
  newToken,
  readToken,
  sessionExpiry,
  setSessionCookie,
  verifyPassword,
} from './auth.js'
import { createStore } from './store.js'

const MAX_RECORD_BYTES = 262144
const USERNAME_MESSAGE = 'نام کاربری باید 3 تا 32 نویسه باشد و فقط حرف انگلیسی، عدد، نقطه یا زیرخط داشته باشد.'
const PASSWORD_MESSAGE = `رمز عبور باید از ${PASSWORD_MIN} تا ${PASSWORD_MAX} نویسه باشد.`
const LOGIN_FAILED = 'نام کاربری یا رمز عبور درست نیست.'
const TOO_MANY = 'تلاش‌های زیاد. چند دقیقه بعد دوباره امتحان کن.'

function fail(res, status, message) {
  res.status(status).json({ error: message })
}

function validCredentials(body) {
  const username = typeof body?.username === 'string' ? body.username.trim() : ''
  const password = typeof body?.password === 'string' ? body.password : ''
  return { username, password }
}

export function createApi(db) {
  const store = createStore(db)
  const router = express.Router()
  const loginLimiter = createLimiter({ limit: 10, windowMs: 15 * 60 * 1000 })
  const registerLimiter = createLimiter({ limit: 10, windowMs: 60 * 60 * 1000 })

  router.use((req, res, next) => {
    res.setHeader('Cache-Control', 'no-store')
    res.setHeader('X-Content-Type-Options', 'nosniff')
    next()
  })

  router.use((req, res, next) => {
    if (req.method === 'GET' || req.method === 'HEAD') return next()
    const origin = req.headers.origin
    if (origin) {
      let host
      try {
        host = new URL(origin).host
      } catch {
        host = ''
      }
      if (host !== req.headers.host) return fail(res, 403, 'این درخواست از مبدا مجاز نیست.')
    }
    return next()
  })

  router.use(express.json({ limit: '6mb' }))

  router.use((req, res, next) => {
    const token = readToken(req)
    req.tokenHash = token ? hashToken(token) : ''
    req.user = req.tokenHash ? store.findSession(req.tokenHash) : null
    next()
  })

  function requireUser(req, res, next) {
    if (!req.user) return fail(res, 401, 'برای ادامه باید وارد شوی.')
    return next()
  }

  function openSession(req, res, userId) {
    const token = newToken()
    store.createSession(userId, hashToken(token), sessionExpiry())
    setSessionCookie(req, res, token)
  }

  router.post('/auth/register', async (req, res) => {
    const { username, password } = validCredentials(req.body)
    if (!USERNAME_PATTERN.test(username)) return fail(res, 400, USERNAME_MESSAGE)
    if (password.length < PASSWORD_MIN || password.length > PASSWORD_MAX) return fail(res, 400, PASSWORD_MESSAGE)
    if (registerLimiter.blocked(req.ip)) return fail(res, 429, TOO_MANY)
    if (store.findUser(username)) return fail(res, 409, 'این نام کاربری قبلاً گرفته شده.')

    registerLimiter.hit(req.ip)
    let userId
    try {
      userId = store.createUser(username, await hashPassword(password))
    } catch {
      return fail(res, 409, 'این نام کاربری قبلاً گرفته شده.')
    }
    openSession(req, res, userId)
    return res.status(201).json({ user: { id: userId, username } })
  })

  router.post('/auth/login', async (req, res) => {
    const { username, password } = validCredentials(req.body)
    if (!username || !password || password.length > PASSWORD_MAX) return fail(res, 400, LOGIN_FAILED)

    const key = `${req.ip}|${username.toLowerCase()}`
    if (loginLimiter.blocked(key)) return fail(res, 429, TOO_MANY)

    const user = store.findUser(username)
    const ok = await verifyPassword(password, user ? user.password_hash : DUMMY_HASH)
    if (!user || !ok) {
      loginLimiter.hit(key)
      return fail(res, 401, LOGIN_FAILED)
    }

    loginLimiter.clear(key)
    openSession(req, res, user.id)
    return res.json({ user: { id: user.id, username: user.username } })
  })

  router.post('/auth/logout', (req, res) => {
    if (req.tokenHash) store.deleteSession(req.tokenHash)
    clearSessionCookie(res)
    res.json({ ok: true })
  })

  router.get('/auth/me', (req, res) => {
    if (!req.user) return fail(res, 401, 'وارد نشده‌ای.')
    return res.json({ user: req.user })
  })

  router.get('/data', requireUser, (req, res) => {
    res.json(store.loadAll(req.user.id))
  })

  router.put('/data', requireUser, (req, res) => {
    let data
    try {
      data = normalizeData(req.body?.data, true)
    } catch (error) {
      return fail(res, 400, error.message)
    }
    store.replaceAll(req.user.id, data)
    return res.json({ ok: true })
  })

  router.put('/settings', requireUser, (req, res) => {
    let settings
    try {
      settings = normalizeSettings(req.body?.settings, true)
    } catch (error) {
      return fail(res, 400, error.message)
    }
    store.saveSettings(req.user.id, settings)
    return res.json({ ok: true })
  })

  router.param('collection', (req, res, next, name) => {
    if (!COLLECTION_KEYS.includes(name)) return fail(res, 404, 'این بخش وجود ندارد.')
    return next()
  })

  function readRecord(body) {
    if (!isPlainObject(body)) return { error: 'مورد باید یک شیء باشد.' }
    const id = typeof body.id === 'string' ? body.id.trim() : ''
    if (!id || id.length > 100) return { error: 'شناسه درست نیست.' }
    const record = { ...body, id }
    if (JSON.stringify(record).length > MAX_RECORD_BYTES) return { error: 'این مورد بیش از حد بزرگ است.' }
    return { record }
  }

  router.put('/data/:collection', requireUser, (req, res) => {
    let items
    try {
      items = normalizeCollection(req.params.collection, req.body?.items, true)
    } catch (error) {
      return fail(res, 400, error.message)
    }
    store.replaceCollection(req.user.id, req.params.collection, items)
    return res.json({ ok: true })
  })

  router.post('/data/:collection', requireUser, (req, res) => {
    const { record, error } = readRecord(req.body)
    if (error) return fail(res, 400, error)
    const result = store.addRecord(req.user.id, req.params.collection, record)
    if (result === 'exists') return fail(res, 409, 'شناسه تکراری است.')
    if (result === 'full') return fail(res, 413, 'تعداد موردها به سقف رسیده.')
    return res.status(201).json({ ok: true })
  })

  router.patch('/data/:collection/:id', requireUser, (req, res) => {
    const { record, error } = readRecord({ ...req.body, id: req.params.id })
    if (error) return fail(res, 400, error)
    if (!store.saveRecord(req.user.id, req.params.collection, record)) {
      return fail(res, 404, 'این مورد پیدا نشد.')
    }
    return res.json({ ok: true })
  })

  router.delete('/data/:collection/:id', requireUser, (req, res) => {
    if (!store.deleteRecord(req.user.id, req.params.collection, req.params.id)) {
      return fail(res, 404, 'این مورد پیدا نشد.')
    }
    return res.json({ ok: true })
  })

  router.use((req, res) => fail(res, 404, 'این آدرس وجود ندارد.'))

  router.use((error, req, res, next) => {
    void next
    if (error?.type === 'entity.parse.failed') return fail(res, 400, 'درخواست درست نیست.')
    if (error?.type === 'entity.too.large') return fail(res, 413, 'درخواست بیش از حد بزرگ است.')
    return fail(res, 500, 'خطای سرور.')
  })

  return router
}
