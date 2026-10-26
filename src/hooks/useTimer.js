import { useEffect } from 'react'
import { useSyncExternalStore } from 'react'
import { getTimerState, subscribeTimer, syncSettings } from '../utils/timerStore'
import { useSettings } from './useSettings'

export function useTimer() {
  const settings = useSettings()
  const timer = useSyncExternalStore(subscribeTimer, getTimerState, getTimerState)

  useEffect(() => {
    syncSettings(settings)
  }, [settings])

  return timer
}
