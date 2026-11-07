// @ts-nocheck
import { useSyncExternalStore } from 'react'
import {
  getTheme,
  getPreference,
  subscribeTheme,
  setPreference,
  setTheme,
  toggleTheme,
} from '../utils/theme'

export function useTheme() {
  const theme = useSyncExternalStore(subscribeTheme, getTheme, () => 'dark')
  const preference = useSyncExternalStore(subscribeTheme, getPreference, () => 'system')
  return {
    theme,
    preference,
    setPreference,
    setTheme,
    toggleTheme,
    isLight: theme === 'light',
  }
}
