import { useEffect, useState } from 'react'
import { getImage } from '../lib/db'
import { useNotesUiStore } from '../store/uiStore'

export default function NotesImage({ src, alt }) {
  const userId = useNotesUiStore((state) => state.userId)
  const isStored = Boolean(src?.startsWith('notes-image:'))
  const [url, setUrl] = useState('')

  useEffect(() => {
    if (!isStored) return undefined
    let alive = true
    const id = src.slice('notes-image:'.length)
    getImage(userId, id).then((row) => {
      if (!alive) return
      setUrl(row?.dataUrl || '')
    })
    return () => {
      alive = false
    }
  }, [isStored, src, userId])

  if (!isStored) {
    if (!src) return <span className="notes-muted">[تصویر]</span>
    return <img src={src} alt={alt || ''} loading="lazy" />
  }

  if (!url) return <span className="notes-muted">[تصویر]</span>
  return <img src={url} alt={alt || ''} loading="lazy" />
}
