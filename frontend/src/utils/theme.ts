// @ts-nocheck
const STORAGE_KEY = 'rotbe-theme'
const listeners = new Set()
let mediaBound = false

function systemTheme() {
  if (typeof window === 'undefined' || !window.matchMedia) return 'dark'
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

function normalizePreference(value) {
  if (value === 'light' || value === 'dark' || value === 'system') return value
  return 'system'
}

export function readPreference() {
  try {
    return normalizePreference(localStorage.getItem(STORAGE_KEY))
  } catch {
    return 'system'
  }
}

export function resolveTheme(preference = readPreference()) {
  const pref = normalizePreference(preference)
  return pref === 'system' ? systemTheme() : pref
}

function themeColorMeta(resolved) {
  return resolved === 'light' ? '#e8e9ee' : '#070708'
}

export function applyResolvedTheme(resolved) {
  const next = resolved === 'light' ? 'light' : 'dark'
  if (typeof document !== 'undefined') {
    document.documentElement.dataset.theme = next
    document.documentElement.style.colorScheme = next
    const meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', themeColorMeta(next))
  }
  return next
}

export function applyTheme(preference = readPreference()) {
  return applyResolvedTheme(resolveTheme(preference))
}

export function getPreference() {
  return readPreference()
}

export function getTheme() {
  if (typeof document !== 'undefined' && document.documentElement.dataset.theme) {
    return document.documentElement.dataset.theme === 'light' ? 'light' : 'dark'
  }
  return resolveTheme()
}

function bindSystemListener() {
  if (mediaBound || typeof window === 'undefined' || !window.matchMedia) return
  mediaBound = true
  const media = window.matchMedia('(prefers-color-scheme: light)')
  const onChange = () => {
    if (readPreference() !== 'system') return
    const next = applyResolvedTheme(systemTheme())
    for (const listener of listeners) listener(next)
  }
  if (media.addEventListener) media.addEventListener('change', onChange)
  else media.addListener(onChange)
}

export function setPreference(preference) {
  const pref = normalizePreference(preference)
  try {
    localStorage.setItem(STORAGE_KEY, pref)
  } catch {
    void 0
  }
  const next = applyTheme(pref)
  for (const listener of listeners) listener(next)
  return next
}

export function setTheme(theme) {
  if (theme === 'system' || theme === 'light' || theme === 'dark') return setPreference(theme)
  return setPreference(theme === 'light' ? 'light' : 'dark')
}

export function toggleTheme() {
  const order = ['system', 'light', 'dark']
  const current = readPreference()
  const index = order.indexOf(current)
  return setPreference(order[(index + 1) % order.length])
}

export function subscribeTheme(listener) {
  listeners.add(listener)
  bindSystemListener()
  return () => listeners.delete(listener)
}

export function bootTheme() {
  applyTheme(readPreference())
  bindSystemListener()
}
