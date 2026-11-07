// @ts-nocheck
import { loadVault, putFolder, putImage, putNote, putSetting, putTrash } from './db'
import { getLegacyNotesDb, loadLegacyVault } from './legacyNotesDb'

export async function migrateGuestNotes(userId: string) {
  const targetId = userId ? String(userId) : ''
  if (!targetId || targetId === 'anon') return { migrated: false, count: 0 }

  const current = await loadVault(targetId)
  if ((current.notes || []).length > 0 || (current.folders || []).length > 0) {
    return { migrated: false, count: 0 }
  }

  const sources = await listGuestSources(targetId)
  let notes: unknown[] = []
  let folders: unknown[] = []
  let images: unknown[] = []
  let trash: unknown[] = []
  let settings: Record<string, unknown> = {}

  for (const sourceId of sources) {
    const vault = await loadLegacyVault(sourceId)
    if ((vault.notes || []).length === 0 && (vault.folders || []).length === 0) continue
    notes = vault.notes || []
    folders = vault.folders || []
    trash = vault.trash || []
    settings = vault.settings || {}
    try {
      images = await getLegacyNotesDb(sourceId).table('images').toArray()
    } catch {
      images = []
    }
    break
  }

  if (!notes.length && !folders.length) return { migrated: false, count: 0 }

  for (const folder of folders) await putFolder(targetId, folder)
  for (const note of notes) await putNote(targetId, note)
  for (const image of images) await putImage(targetId, image)
  for (const item of trash) await putTrash(targetId, item)
  for (const [key, value] of Object.entries(settings)) await putSetting(targetId, key, value)

  return { migrated: true, count: notes.length }
}

async function listGuestSources(targetId: string) {
  const names = new Set(['anon'])
  try {
    const dbs = await indexedDB.databases()
    for (const db of dbs || []) {
      const name = db.name || ''
      const match = /^rotbe-notes-(.+)$/.exec(name)
      if (!match) continue
      const id = match[1]
      if (id === targetId) continue
      names.add(id)
    }
  } catch {
    void 0
  }
  return [...names]
}
