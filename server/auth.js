import crypto from 'node:crypto'
import { promisify } from 'node:util'

const scrypt = promisify(crypto.scrypt)
const COOKIE_NAME = 'kh_session'
const SESSION_DAYS = 30
const KEY_LENGTH = 64

export const USERNAME_PATTERN = /^[A-Za-z0-9_.]{3,32}$/
export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 128

export async function hashPassword(password) {
  const salt = crypto.randomBytes(16)
  const key = await scrypt(password, salt, KEY_LENGTH)
  return `${salt.toString('hex')}:${key.toString('hex')}`
}

export async function verifyPassword(password, stored) {
  const [saltHex, keyHex] = String(stored).split(':')
  if (!saltHex || !keyHex) return false
  const expected = Buffer.from(keyHex, 'hex')
  const actual = await scrypt(password, Buffer.from(saltHex, 'hex'), expected.length)
  return crypto.timingSafeEqual(actual, expected)
}

export const DUMMY_HASH = `${'00'.repeat(16)}:${'00'.repeat(KEY_LENGTH)}`

export function newToken() {
  return crypto.randomBytes(32).toString('base64url')
}

export function hashToken(token) {
  return crypto.createHash('sha256').update(token).digest('hex')
}

export function sessionExpiry() {
  return Date.now() + SESSION_DAYS * 24 * 60 * 60 * 1000
}

export function readToken(req) {
  const raw = req.headers.cookie || ''
  for (const part of raw.split(';')) {
    const index = part.indexOf('=')
    if (index < 0) continue
    if (part.slice(0, index).trim() !== COOKIE_NAME) continue
    try {
      return decodeURIComponent(part.slice(index + 1).trim())
    } catch {
      return ''
    }
  }
  return ''
}

export function setSessionCookie(req, res, token) {
  const parts = [
    `${COOKIE_NAME}=${encodeURIComponent(token)}`,
    'HttpOnly',
    'SameSite=Lax',
    'Path=/',
    `Max-Age=${SESSION_DAYS * 24 * 60 * 60}`,
  ]
  if (req.secure || req.headers['x-forwarded-proto'] === 'https') parts.push('Secure')
  res.setHeader('Set-Cookie', parts.join('; '))
}

export function clearSessionCookie(res) {
  res.setHeader('Set-Cookie', `${COOKIE_NAME}=; HttpOnly; SameSite=Lax; Path=/; Max-Age=0`)
}

export function createLimiter({ limit, windowMs }) {
  const hits = new Map()

  return {
    blocked(key) {
      const entry = hits.get(key)
      if (!entry) return false
      if (entry.resetAt < Date.now()) {
        hits.delete(key)
        return false
      }
      return entry.count >= limit
    },
    hit(key) {
      const now = Date.now()
      const entry = hits.get(key)
      if (!entry || entry.resetAt < now) {
        hits.set(key, { count: 1, resetAt: now + windowMs })
        return
      }
      entry.count += 1
    },
    clear(key) {
      hits.delete(key)
    },
  }
}
