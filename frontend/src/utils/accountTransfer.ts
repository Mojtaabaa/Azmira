// @ts-nocheck
import { useNotesStore } from '../features/notes/store/notesStore'
import { request } from './api'
import { downloadText, stamp } from './csv'
import { loadData } from './storage'

export function backupCounts(backup) {
  const data = backup?.data && typeof backup.data === 'object' ? backup.data : backup
  const vault = backup?.vault && typeof backup.vault === 'object' ? backup.vault : null
  const lengthOf = (value) => (Array.isArray(value) ? value.length : 0)
  return {
    sessions: lengthOf(data?.sessions),
    tests: lengthOf(data?.tests),
    mistakes: lengthOf(data?.mistakes),
    topics: lengthOf(data?.topics),
    tasks: lengthOf(data?.tasks),
    events: lengthOf(data?.events),
    notes: lengthOf(vault?.notes),
    folders: lengthOf(vault?.folders),
    images: lengthOf(vault?.images),
    hasVault: Boolean(vault),
    legacy: !backup?.data,
  }
}

export async function downloadAccountExport() {
  const payload = await request('GET', '/account/export')
  downloadText(
    `konkoor-help-${stamp()}.json`,
    JSON.stringify(payload, null, 2),
    'application/json',
  )
  return backupCounts(payload)
}

export async function importAccountBackup(backup, mode, userId) {
  await request('POST', '/account/import', { mode, backup })
  await loadData()
  if (userId) await useNotesStore.getState().hydrate(userId)
}
