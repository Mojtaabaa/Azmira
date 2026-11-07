// @ts-nocheck
import { useSyncExternalStore } from 'react'
import { getAll, subscribe } from '../utils/storage'

export function useCollection(collection) {
  return useSyncExternalStore(
    subscribe,
    () => getAll(collection),
    () => getAll(collection),
  )
}
