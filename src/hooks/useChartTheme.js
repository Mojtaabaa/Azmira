import { useSyncExternalStore } from 'react'
import { getTheme, subscribeTheme } from '../utils/theme'

let cachedTheme = ''
let cached = null

function readVar(name, fallback) {
  if (typeof window === 'undefined') return fallback
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  return value || fallback
}

function snapshot() {
  const theme = getTheme()
  if (cached && cachedTheme === theme) return cached
  cachedTheme = theme
  cached = {
    theme,
    text: readVar('--chart-text', '#f4f6fb'),
    fill: readVar('--chart-fill', '#fdb827'),
    grid: readVar('--chart-grid', '#2a3348'),
    tipBg: readVar('--surface', '#0f1420'),
    tipBorder: readVar('--line', '#2a3348'),
  }
  return cached
}

export function useChartTheme() {
  return useSyncExternalStore(subscribeTheme, snapshot, snapshot)
}
