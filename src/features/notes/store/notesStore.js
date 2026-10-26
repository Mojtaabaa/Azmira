import { create } from 'zustand'
import {
  deleteFolderRow,
  deleteNoteRow,
  loadVault,
  putFolder,
  putNote,
  putSetting,
  putTrash,
  purgeOldTrash,
  restoreTrash,
} from '../lib/db'
import { createId } from '../lib/id'
import { extractWikiLinks, lineSnippet, renameLinksInContent } from '../lib/parseLinks'
import { extractTags } from '../lib/parseTags'
import { migrateGuestNotes } from '../lib/migrateGuestNotes'
import { useNotesUiStore } from './uiStore'

function withMeta(note) {
  const content = note.content || ''
  return {
    ...note,
    tags: extractTags(content),
    outgoingLinks: extractWikiLinks(content),
  }
}

function titleExists(notes, title, exceptId) {
  const needle = title.trim().toLowerCase()
  return notes.some((note) => note.id !== exceptId && note.title.trim().toLowerCase() === needle)
}

function uniqueTitle(notes, base) {
  let name = base.trim() || 'یادداشت بدون عنوان'
  if (!titleExists(notes, name)) return name
  let i = 2
  while (titleExists(notes, `${name} ${i}`)) i += 1
  return `${name} ${i}`
}

export const useNotesStore = create((set, get) => ({
  notes: [],
  folders: [],
  trash: [],
  loaded: false,

  async hydrate(userId) {
    await migrateGuestNotes(userId)
    await purgeOldTrash(userId)
    const vault = await loadVault(userId)
    set({
      notes: vault.notes.map(withMeta),
      folders: vault.folders,
      trash: vault.trash,
      loaded: true,
    })
    return vault.settings
  },

  getNoteById(id) {
    return get().notes.find((note) => note.id === id) || null
  },

  getNoteByTitle(title) {
    const needle = String(title || '').trim().toLowerCase()
    return get().notes.find((note) => note.title.trim().toLowerCase() === needle) || null
  },

  async createNote(userId, partial = {}) {
    const now = Date.now()
    const title = uniqueTitle(get().notes, partial.title || 'یادداشت جدید')
    const note = withMeta({
      id: createId(),
      title,
      content: partial.content || '',
      folderId: partial.folderId || null,
      tags: [],
      createdAt: now,
      updatedAt: now,
      pinned: Boolean(partial.pinned),
    })
    set({ notes: [note, ...get().notes] })
    await putNote(userId, note)
    return note
  },

  async updateNote(userId, id, patch) {
    const current = get().getNoteById(id)
    if (!current) return null
    let next = { ...current, ...patch, updatedAt: Date.now() }
    if (Object.prototype.hasOwnProperty.call(patch, 'title') && patch.title !== current.title) {
      const raw = String(patch.title ?? '')
      const title = raw.trim()
        ? uniqueTitle(get().notes.filter((note) => note.id !== id), raw)
        : raw
      next.title = title
      const shouldRewrite = Boolean(current.title.trim()) && Boolean(title.trim()) && current.title !== title
      const oldKey = `[[${current.title}`.toLowerCase()
      const updatedNotes = get().notes.map((note) => {
        if (note.id === id) return withMeta({ ...next, title })
        if (!shouldRewrite || !(note.content || '').toLowerCase().includes(oldKey)) return note
        const content = renameLinksInContent(note.content, current.title, title)
        const renamed = withMeta({ ...note, content, updatedAt: Date.now() })
        putNote(userId, renamed)
        return renamed
      })
      set({ notes: updatedNotes })
      await putNote(userId, updatedNotes.find((note) => note.id === id))
      return updatedNotes.find((note) => note.id === id)
    }
    next = withMeta(next)
    set({ notes: get().notes.map((note) => (note.id === id ? next : note)) })
    await putNote(userId, next)
    return next
  },

  async deleteNote(userId, id) {
    const note = get().getNoteById(id)
    if (!note) return
    const trashItem = { id: createId(), kind: 'note', data: note, deletedAt: Date.now() }
    set({
      notes: get().notes.filter((item) => item.id !== id),
      trash: [trashItem, ...get().trash],
    })
    useNotesUiStore.getState().closeTab(id)
    await putTrash(userId, trashItem)
    await deleteNoteRow(userId, id)
  },

  async createFolder(userId, partial = {}) {
    const folder = {
      id: createId(),
      name: partial.name || 'پوشه جدید',
      parentId: partial.parentId || null,
      collapsed: false,
    }
    set({ folders: [...get().folders, folder] })
    await putFolder(userId, folder)
    return folder
  },

  async updateFolder(userId, id, patch) {
    const next = get().folders.map((folder) => (folder.id === id ? { ...folder, ...patch } : folder))
    set({ folders: next })
    const folder = next.find((item) => item.id === id)
    if (folder) await putFolder(userId, folder)
    return folder
  },

  async deleteFolder(userId, id) {
    const folder = get().folders.find((item) => item.id === id)
    if (!folder) return
    const childIds = new Set([id])
    let grew = true
    while (grew) {
      grew = false
      for (const item of get().folders) {
        if (item.parentId && childIds.has(item.parentId) && !childIds.has(item.id)) {
          childIds.add(item.id)
          grew = true
        }
      }
    }
    const movingNotes = get().notes.filter((note) => childIds.has(note.folderId))
    const trashItem = { id: createId(), kind: 'folder', data: folder, deletedAt: Date.now() }
    set({
      folders: get().folders.filter((item) => !childIds.has(item.id)),
      notes: get().notes.map((note) => (
        childIds.has(note.folderId) ? withMeta({ ...note, folderId: null, updatedAt: Date.now() }) : note
      )),
      trash: [trashItem, ...get().trash],
    })
    await putTrash(userId, trashItem)
    for (const childId of childIds) await deleteFolderRow(userId, childId)
    for (const note of movingNotes) {
      const updated = withMeta({ ...note, folderId: null, updatedAt: Date.now() })
      await putNote(userId, updated)
    }
  },

  async moveNote(userId, id, folderId) {
    return get().updateNote(userId, id, { folderId })
  },

  async restoreFromTrash(userId, trashId) {
    const item = await restoreTrash(userId, trashId)
    if (!item) return null
    if (item.kind === 'note') {
      set({
        notes: [withMeta(item.data), ...get().notes.filter((note) => note.id !== item.data.id)],
        trash: get().trash.filter((row) => row.id !== trashId),
      })
    } else if (item.kind === 'folder') {
      set({
        folders: [...get().folders.filter((folder) => folder.id !== item.data.id), item.data],
        trash: get().trash.filter((row) => row.id !== trashId),
      })
    }
    return item
  },

  async importVaultData(userId, payload) {
    const folders = [...get().folders, ...(payload.folders || [])]
    const notes = [...(payload.notes || []).map(withMeta), ...get().notes]
    set({ folders, notes })
    for (const folder of payload.folders || []) await putFolder(userId, folder)
    for (const note of payload.notes || []) await putNote(userId, withMeta(note))
  },

  backlinksFor(title) {
    const needle = String(title || '').trim().toLowerCase()
    if (!needle) return []
    return get().notes
      .filter((note) => (note.outgoingLinks || []).some((link) => link.trim().toLowerCase() === needle))
      .map((note) => {
        const hits = []
        const re = new RegExp(`\\[\\[${String(title || '').trim().replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:\\|[^\\]]*)?\\]\\]`, 'gi')
        let match
        while ((match = re.exec(note.content || ''))) {
          hits.push(lineSnippet(note.content, match.index))
        }
        return { note, snippets: hits }
      })
  },

  unresolvedLinks() {
    const titles = new Set(get().notes.map((note) => note.title.trim().toLowerCase()))
    const unresolved = new Set()
    for (const note of get().notes) {
      for (const link of note.outgoingLinks) {
        if (!titles.has(link.trim().toLowerCase())) unresolved.add(link)
      }
    }
    return [...unresolved]
  },

  tagCounts() {
    const counts = new Map()
    for (const note of get().notes) {
      for (const tag of note.tags || []) {
        counts.set(tag, (counts.get(tag) || 0) + 1)
      }
    }
    return [...counts.entries()]
      .map(([tag, count]) => ({ tag, count }))
      .sort((a, b) => b.count - a.count || a.tag.localeCompare(b.tag, 'fa'))
  },

  async persistUiSettings(userId, settings) {
    for (const [key, value] of Object.entries(settings)) {
      await putSetting(userId, key, value)
    }
  },
}))
