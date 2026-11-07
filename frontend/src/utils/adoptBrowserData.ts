// @ts-nocheck
import {
  markLegacyImported,
  readBrowserLegacy,
  wasLegacyImported,
} from './legacyData'
import { exportAllData, hasRecords, replaceAllData } from './storage'
import { adoptLegacyTimer } from './timerStore'

export async function adoptBrowserDataIfNeeded(userId, { force = false } = {}) {
  if (!userId) return { imported: false, reason: 'no-user' }
  if (!force && wasLegacyImported(userId)) return { imported: false, reason: 'already-imported' }
  if (hasRecords(exportAllData())) return { imported: false, reason: 'account-has-data' }

  const legacy = readBrowserLegacy()
  if (!legacy) return { imported: false, reason: 'no-legacy' }

  await replaceAllData(legacy)
  adoptLegacyTimer(userId)
  markLegacyImported(userId)
  return { imported: true, summary: legacy }
}
