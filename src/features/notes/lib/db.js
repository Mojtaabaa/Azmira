import Dexie from 'dexie'

const caches = new Map()

export function getNotesDb(userId) {
  const key = userId ? String(userId) : 'anon'
  if (caches.has(key)) return caches.get(key)
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

export async function loadVault(userId) {
  const db = getNotesDb(userId)
  const [notes, folders, settingsRows, trash] = await Promise.all([
    db.notes.toArray(),
    db.folders.toArray(),
    db.settings.toArray(),
    db.trash.toArray(),
  ])
  const settings = {}
  for (const row of settingsRows) settings[row.key] = row.value
  return { notes, folders, settings, trash }
}

export async function putNote(userId, note) {
  await getNotesDb(userId).notes.put(note)
}

export async function putFolder(userId, folder) {
  await getNotesDb(userId).folders.put(folder)
}

export async function deleteNoteRow(userId, id) {
  await getNotesDb(userId).notes.delete(id)
}

export async function deleteFolderRow(userId, id) {
  await getNotesDb(userId).folders.delete(id)
}

export async function putSetting(userId, key, value) {
  await getNotesDb(userId).settings.put({ key, value })
}

export async function putTrash(userId, item) {
  await getNotesDb(userId).trash.put(item)
}

export async function restoreTrash(userId, id) {
  const db = getNotesDb(userId)
  const item = await db.trash.get(id)
  if (!item) return null
  await db.transaction('rw', db.notes, db.folders, db.trash, async () => {
    if (item.kind === 'note') await db.notes.put(item.data)
    if (item.kind === 'folder') await db.folders.put(item.data)
    await db.trash.delete(id)
  })
  return item
}

export async function putImage(userId, image) {
  await getNotesDb(userId).images.put(image)
}

export async function getImage(userId, id) {
  return getNotesDb(userId).images.get(id)
}

export async function purgeOldTrash(userId, days = 30) {
  const db = getNotesDb(userId)
  const cutoff = Date.now() - days * 24 * 60 * 60 * 1000
  await db.trash.where('deletedAt').below(cutoff).delete()
}
