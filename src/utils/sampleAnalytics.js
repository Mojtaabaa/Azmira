import { getAll, setAll } from './storage'

export function hasSampleData(sessions, tests, mistakes) {
  return [...sessions, ...tests, ...mistakes].some((item) => String(item?.id || '').startsWith('sample-'))
}

export function removeSampleAnalytics() {
  for (const name of ['mistakes', 'tests', 'sessions']) {
    const next = getAll(name).filter((item) => !String(item.id).startsWith('sample-'))
    setAll(name, next)
  }
}
