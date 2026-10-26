import { putImage } from './db'
import { createId } from './id'
import { insertAtCursor } from './editorFormat'

export async function storeAndInsertImage(userId, file, view) {
  if (!file || !file.type.startsWith('image/')) return false
  const dataUrl = await readAsDataUrl(file)
  const id = createId()
  await putImage(userId, { id, dataUrl, createdAt: Date.now() })
  const markdown = `\n![تصویر](notes-image:${id})\n`
  if (view) insertAtCursor(view, markdown)
  return markdown
}

function readAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result)
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}
