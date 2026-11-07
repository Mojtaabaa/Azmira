// @ts-nocheck
import { useEffect, useState } from 'react'
import { getImage } from '../lib/db'
import { peekImage, rememberImage } from '../lib/imageCache'
import { parseNotesImageId } from '../lib/insertImage'
import { useNotesUiStore } from '../store/uiStore'

export default function NotesImage({ src, alt }) {
  const userId = useNotesUiStore((state) => state.userId)
  const storedId = parseNotesImageId(src)
  const [url, setUrl] = useState(() => (storedId ? peekImage(storedId) : (src || '')))

  useEffect(() => {
    if (!storedId) {
      setUrl(src || '')
      return undefined
    }
    const cached = peekImage(storedId)
    if (cached) {
      setUrl(cached)
      return undefined
    }
    let alive = true
    getImage(userId, storedId).then((row) => {
      if (!alive) return
      if (row?.dataUrl) rememberImage(storedId, row.dataUrl)
      setUrl(row?.dataUrl || '')
    })
    return () => {
      alive = false
    }
  }, [storedId, src, userId])

  if (!url) return <span className="notes-muted">[تصویر]</span>
  return <img src={url} alt={alt || ''} loading="lazy" />
}
