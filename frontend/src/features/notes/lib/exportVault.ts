// @ts-nocheck
import JSZip from 'jszip'
import { saveAs } from 'file-saver'

function safeName(title) {
  return String(title || 'note').replace(/[<>:"/\\|?*\t\n\r\f\v]/g, '_').slice(0, 80)
}

function folderPath(folderId, folders) {
  const map = Object.fromEntries(folders.map((folder) => [folder.id, folder]))
  const parts = []
  let cursor = folderId
  const guard = new Set()
  while (cursor && map[cursor] && !guard.has(cursor)) {
    guard.add(cursor)
    parts.unshift(safeName(map[cursor].name))
    cursor = map[cursor].parentId
  }
  return parts.join('/')
}

export async function exportVault(notes, folders = []) {
  const zip = new JSZip()
  const used = new Set()
  for (const note of notes) {
    const dir = folderPath(note.folderId, folders)
    let base = safeName(note.title)
    let path = dir ? `${dir}/${base}.md` : `${base}.md`
    let n = 2
    while (used.has(path.toLowerCase())) {
      path = dir ? `${dir}/${base}-${n}.md` : `${base}-${n}.md`
      n += 1
    }
    used.add(path.toLowerCase())
    zip.file(path, note.content || '')
  }
  const blob = await zip.generateAsync({ type: 'blob' })
  saveAs(blob, 'rotbe-notes.zip')
}

export function exportNote(note) {
  const blob = new Blob([note.content || ''], { type: 'text/markdown;charset=utf-8' })
  saveAs(blob, `${safeName(note.title)}.md`)
}
