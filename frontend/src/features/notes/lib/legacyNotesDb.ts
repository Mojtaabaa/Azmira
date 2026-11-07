// @ts-nocheck
import Dexie from 'dexie'

const caches = new Map<string, Dexie>()

export function getLegacyNotesDb(userId: string) {
  const key = userId ? String(userId) : 'anon'
  const cached = caches.get(key)
  if (cached) return cached
  const db = new Dexie(`rotbe-notes-${key}`)
  db.version(1).stores({
    notes: 'id, title, folderId, updatedAt, createdAt, pinned',
    folders: 'id, parentId, name',
    settings: 'key',
    images: 'id',
    trash: 'id, deletedAt',
  })
  caches.set(key, db)
  return db
}

export async function loadLegacyVault(userId: string) {
  const db = getLegacyNotesDb(userId)
  const [notes, folders, settingsRows, trash] = await Promise.all([
    db.table('notes').toArray(),
    db.table('folders').toArray(),
    db.table('settings').toArray(),
    db.table('trash').toArray(),
  ])
  const settings: Record<string, unknown> = {}
  for (const row of settingsRows) settings[row.key] = row.value
  return { notes, folders, settings, trash }
}
