import { putImage } from './db'
import { createId } from './id'
import { insertAtCursor } from './editorFormat'
import { rememberImage } from './imageCache'

const IMAGE_EXT = /\.(png|jpe?g|gif|webp|bmp|svg|heic|tif{1,2})$/i

export function isImageFile(file) {
  if (!file) return false
  if (file.type?.startsWith('image/')) return true
  return IMAGE_EXT.test(file.name || '')
}

export function collectImageFiles(dataTransfer) {
  if (!dataTransfer) return []
  const files = []
  const seen = new Set()
  function add(file) {
    if (!file || !isImageFile(file) || seen.has(file)) return
    seen.add(file)
    files.push(file)
  }
  for (const file of dataTransfer.files || []) add(file)
  for (const item of dataTransfer.items || []) {
    if (item.kind === 'file') add(item.getAsFile())
  }
  return files
}

export async function filesFromHtmlImages(html) {
  if (!html || !html.includes('<img')) return []
  const matches = [...html.matchAll(/<img[^>]+src=["']([^"']+)["']/gi)]
  const files = []
  for (const match of matches) {
    const src = match[1]
    if (src.startsWith('data:image/')) {
      const file = dataUrlToFile(src)
      if (file) files.push(file)
    }
  }
  return files
}

export function parseNotesImageId(src) {
  if (!src) return ''
  try {
    const raw = decodeURIComponent(String(src))
    const colon = /notes-image:([A-Za-z0-9_-]+)/.exec(raw)
    if (colon) return colon[1]
    const slash = /notes-image\/([A-Za-z0-9_-]+)/.exec(raw)
    if (slash) return slash[1]
  } catch {
    return ''
  }
  return ''
}

export function imageMarkdown(id, alt = 'تصویر') {
  return `\n![${alt}](notes-image/${id})\n`
}

export async function storeAndInsertImage(userId, file, view) {
  if (!isImageFile(file)) return ''
  const dataUrl = await readAsDataUrl(file)
  const id = createId()
  rememberImage(id, dataUrl)
  await putImage(userId, { id, dataUrl, createdAt: Date.now() })
  const markdown = imageMarkdown(id, file.name?.replace(IMAGE_EXT, '') || 'تصویر')
  if (view) {
    const before = view.state.doc.toString()
    insertAtCursor(view, markdown)
    if (view.state.doc.toString() === before) {
      insertAtCursor(view, markdown)
    }
  }
  return markdown
}

function dataUrlToFile(dataUrl) {
  const match = /^data:(image\/[a-zA-Z0-9.+-]+);base64,(.+)$/.exec(dataUrl)
  if (!match) return null
  const binary = atob(match[2])
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i += 1) bytes[i] = binary.charCodeAt(i)
  return new File([bytes], 'paste.png', { type: match[1] })
}

function readAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result)
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}
