import { useMemo, useState } from 'react'
import { searchNotes, snippetParts } from '../lib/search'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function SearchPanel() {
  const notes = useNotesStore((state) => state.notes)
  const folders = useNotesStore((state) => state.folders)
  const openNote = useNotesUiStore((state) => state.openNote)
  const setSearchOpen = useNotesUiStore((state) => state.setSearchOpen)
  const [query, setQuery] = useState('')
  const results = useMemo(() => searchNotes(notes, folders, query), [notes, folders, query])

  return (
    <div className="notes-modal-backdrop" onClick={() => setSearchOpen(false)}>
      <div className="notes-modal" role="dialog" aria-label="جستجو" onClick={(event) => event.stopPropagation()}>
        <input
          autoFocus
          dir="auto"
          value={query}
          placeholder="جستجو…  tag:#ریاضی  path:پوشه"
          onChange={(event) => setQuery(event.target.value)}
        />
        <ul className="notes-search-results">
          {results.slice(0, 40).map((row) => (
            <li key={row.item.id}>
              <button
                type="button"
                onClick={() => {
                  openNote(row.item.id)
                  setSearchOpen(false)
                }}
              >
                <strong dir="auto">{row.item.title}</strong>
                <span dir="auto">
                  {snippetParts(row.item, row.matches).map((part, index) => (
                    part.hit
                      ? <mark key={index}>{part.text}</mark>
                      : <span key={index}>{part.text}</span>
                  ))}
                </span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
