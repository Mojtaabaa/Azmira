// @ts-nocheck
import JSZip from 'jszip'
import { createId } from './id'

export async function importMarkdownFiles(fileList) {
  const files = [...fileList]
  const notes = []
  const folderMap = new Map()

  for (const file of files) {
    if (file.name.endsWith('.zip')) {
      const zip = await JSZip.loadAsync(file)
      const entries = Object.values(zip.files).filter((entry) => !entry.dir && entry.name.endsWith('.md'))
      for (const entry of entries) {
        const content = await entry.async('string')
        const parts = entry.name.split('/').filter(Boolean)
        const fileName = parts.pop().replace(/\.md$/i, '')
        let parentId = null
        for (const part of parts) {
          const key = `${parentId || 'root'}::${part}`
          if (!folderMap.has(key)) {
            folderMap.set(key, { id: createId(), name: part, parentId, collapsed: false })
          }
          parentId = folderMap.get(key).id
        }
        notes.push(makeNote(fileName, content, parentId))
      }
      continue
    }
    if (!file.name.endsWith('.md')) continue
    const content = await file.text()
    notes.push(makeNote(file.name.replace(/\.md$/i, ''), content, null))
  }

  return { notes, folders: [...folderMap.values()] }
}

function makeNote(title, content, folderId) {
  const now = Date.now()
  return {
    id: createId(),
    title,
    content,
    folderId,
    tags: [],
    createdAt: now,
    updatedAt: now,
    pinned: false,
  }
}
