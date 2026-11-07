// @ts-nocheck
import { useRef } from 'react'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function NoteTabs() {
  const notes = useNotesStore((state) => state.notes)
  const openTabs = useNotesUiStore((state) => state.openTabs)
  const activeNoteId = useNotesUiStore((state) => state.activeNoteId)
  const setActiveNoteId = useNotesUiStore((state) => state.setActiveNoteId)
  const closeTab = useNotesUiStore((state) => state.closeTab)
  const reorderTabs = useNotesUiStore((state) => state.reorderTabs)
  const dragIndex = useRef(null)

  return (
    <div className="notes-tabs" role="tablist" aria-label="یادداشت‌های باز">
      {openTabs.map((id, index) => {
        const note = notes.find((item) => item.id === id)
        if (!note) return null
        return (
          <div
            key={id}
            className={id === activeNoteId ? 'notes-tab is-active' : 'notes-tab'}
            role="tab"
            aria-selected={id === activeNoteId}
            draggable
            onDragStart={() => { dragIndex.current = index }}
            onDragOver={(event) => event.preventDefault()}
            onDrop={() => {
              if (dragIndex.current == null || dragIndex.current === index) return
              const next = openTabs.slice()
              const [moved] = next.splice(dragIndex.current, 1)
              next.splice(index, 0, moved)
              reorderTabs(next)
              dragIndex.current = null
            }}
          >
            <button type="button" className="notes-tab-main" onClick={() => setActiveNoteId(id)}>
              <span dir="auto">{note.title}</span>
            </button>
            <button type="button" className="notes-tab-close" aria-label="بستن" onClick={() => closeTab(id)}>
              ×
            </button>
          </div>
        )
      })}
    </div>
  )
}
