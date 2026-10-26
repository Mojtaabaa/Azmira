import { useSyncExternalStore } from 'react'
import { getSettings, subscribe } from '../utils/storage'

export function useSettings() {
  return useSyncExternalStore(subscribe, getSettings, getSettings)
}
