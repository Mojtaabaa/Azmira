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
    text: readVar('--text-muted', '#9a9890'),
    fill: readVar('--accent', '#e8924a'),
    grid: readVar('--border', '#2c2c29'),
    tipBg: readVar('--surface', '#131312'),
    tipBorder: readVar('--border', '#2c2c29'),
    danger: readVar('--danger', '#e07171'),
    warning: readVar('--warning', '#e0b35c'),
    info: readVar('--info', '#6ea0d4'),
    muted: readVar('--text-muted', '#9a9890'),
  }
  return cached
}

export function useChartTheme() {
  return useSyncExternalStore(subscribeTheme, snapshot, snapshot)
}
