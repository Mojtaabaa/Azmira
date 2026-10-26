import { useSyncExternalStore } from 'react'
import { getTheme, subscribeTheme, setTheme, toggleTheme } from '../utils/theme'

export function useTheme() {
  const theme = useSyncExternalStore(subscribeTheme, getTheme, () => 'dark')
  return { theme, setTheme, toggleTheme, isLight: theme === 'light' }
}
