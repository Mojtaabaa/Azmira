import { useMemo, useState } from 'react'
import Fuse from 'fuse.js'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function QuickSwitcher() {
  const notes = useNotesStore((state) => state.notes)
  const openNote = useNotesUiStore((state) => state.openNote)
  const setSwitcherOpen = useNotesUiStore((state) => state.setSwitcherOpen)
  const [query, setQuery] = useState('')
  const fuse = useMemo(() => new Fuse(notes, { keys: ['title'], threshold: 0.35 }), [notes])
  const results = query.trim() ? fuse.search(query).map((row) => row.item) : notes.slice().sort((a, b) => b.updatedAt - a.updatedAt)

  return (
    <div className="notes-modal-backdrop" onClick={() => setSwitcherOpen(false)}>
      <div className="notes-modal" role="dialog" aria-label="جابه‌جایی سریع" onClick={(event) => event.stopPropagation()}>
        <input
          autoFocus
          dir="auto"
          value={query}
          placeholder="پرش به یادداشت…"
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && results[0]) {
              openNote(results[0].id)
              setSwitcherOpen(false)
            }
          }}
        />
        <ul className="notes-search-results">
          {results.slice(0, 30).map((note) => (
            <li key={note.id}>
              <button
                type="button"
                onClick={() => {
                  openNote(note.id)
                  setSwitcherOpen(false)
                }}
              >
                <strong dir="auto">{note.title}</strong>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
