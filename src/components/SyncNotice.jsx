import { useSyncExternalStore } from 'react'
import { clearSyncError, getSyncError, subscribeSync } from '../utils/storage'

export default function SyncNotice() {
  const message = useSyncExternalStore(subscribeSync, getSyncError, getSyncError)

  if (!message) return null

  return (
    <div className="sync-notice" role="alert">
      <span>{message}</span>
      <button type="button" aria-label="بستن" onClick={clearSyncError}>
        ×
      </button>
    </div>
  )
}
