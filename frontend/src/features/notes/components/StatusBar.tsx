// @ts-nocheck
import { useNotesUiStore } from '../store/uiStore'

export default function StatusBar({ note }) {
  const saveState = useNotesUiStore((state) => state.saveState)
  const lastSavedAt = useNotesUiStore((state) => state.lastSavedAt)
  const content = note?.content || ''
  const words = content.trim() ? content.trim().split(/\s+/).length : 0
  const chars = content.length
  const savedLabel = saveState === 'saving'
    ? 'در حال ذخیره…'
    : lastSavedAt
      ? `ذخیره شد ${new Date(lastSavedAt).toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' })}`
      : 'ذخیره شد'

  return (
    <footer className="notes-status">
      <span>{words} واژه</span>
      <span>{chars} نویسه</span>
      <span>{savedLabel}</span>
    </footer>
  )
}
