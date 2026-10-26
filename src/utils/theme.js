const STORAGE_KEY = 'rotbe-theme'
const listeners = new Set()

function systemTheme() {
  if (typeof window === 'undefined' || !window.matchMedia) return 'dark'
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

export function readTheme() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (saved === 'light' || saved === 'dark') return saved
  } catch {
    return systemTheme()
  }
  return systemTheme()
}

export function applyTheme(theme) {
  const next = theme === 'light' ? 'light' : 'dark'
  if (typeof document !== 'undefined') {
    document.documentElement.dataset.theme = next
    document.documentElement.style.colorScheme = next
    const meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', next === 'light' ? '#eef1f6' : '#05070b')
  }
  return next
}

export function getTheme() {
  if (typeof document !== 'undefined' && document.documentElement.dataset.theme) {
    return document.documentElement.dataset.theme === 'light' ? 'light' : 'dark'
  }
  return readTheme()
}

export function setTheme(theme) {
  const next = applyTheme(theme)
  try {
    localStorage.setItem(STORAGE_KEY, next)
  } catch {
    void 0
  }
  for (const listener of listeners) listener(next)
  return next
}

export function toggleTheme() {
  return setTheme(getTheme() === 'light' ? 'dark' : 'light')
}

export function subscribeTheme(listener) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function bootTheme() {
  applyTheme(readTheme())
}
