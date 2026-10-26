import { useMemo, useState } from 'react'
import Fuse from 'fuse.js'
import { toggleTheme } from '../../../utils/theme'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'
import { exportVault } from '../lib/exportVault'

export default function CommandPalette() {
  const notes = useNotesStore((state) => state.notes)
  const folders = useNotesStore((state) => state.folders)
  const createNote = useNotesStore((state) => state.createNote)
  const userId = useNotesUiStore((state) => state.userId)
  const openNote = useNotesUiStore((state) => state.openNote)
  const setCommandOpen = useNotesUiStore((state) => state.setCommandOpen)
  const cycleViewMode = useNotesUiStore((state) => state.cycleViewMode)
  const setGraphOpen = useNotesUiStore((state) => state.setGraphOpen)
  const setSearchOpen = useNotesUiStore((state) => state.setSearchOpen)
  const setTrashOpen = useNotesUiStore((state) => state.setTrashOpen)
  const [query, setQuery] = useState('')

  const commands = useMemo(() => [
    {
      id: 'new',
      label: 'یادداشت تازه',
      run: async () => {
        const note = await createNote(userId)
        openNote(note.id)
      },
    },
    { id: 'theme', label: 'تعویض تم', run: () => toggleTheme() },
    { id: 'view', label: 'تعویض حالت ویرایش/پیش‌نمایش', run: () => cycleViewMode() },
    { id: 'search', label: 'جستجو', run: () => setSearchOpen(true) },
    { id: 'graph', label: 'نمای گراف', run: () => setGraphOpen(true) },
    { id: 'trash', label: 'سطل زباله', run: () => setTrashOpen(true) },
    { id: 'daily', label: 'یادداشت امروز', run: async () => {
      const title = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
      }).format(new Date())
      const existing = notes.find((note) => note.title === title)
      if (existing) openNote(existing.id)
      else openNote((await createNote(userId, { title })).id)
    } },
    { id: 'export', label: 'خروجی کل مخزن', run: () => exportVault(notes, folders) },
    ...notes.map((note) => ({
      id: `open:${note.id}`,
      label: `باز کردن: ${note.title}`,
      run: () => openNote(note.id),
    })),
  ], [notes, folders, userId, createNote, openNote, cycleViewMode, setSearchOpen, setGraphOpen, setTrashOpen])

  const fuse = useMemo(() => new Fuse(commands, { keys: ['label'], threshold: 0.4 }), [commands])
  const results = query.trim() ? fuse.search(query).map((row) => row.item) : commands.slice(0, 20)

  return (
    <div className="notes-modal-backdrop" onClick={() => setCommandOpen(false)}>
      <div className="notes-modal" role="dialog" aria-label="فرمان‌ها" onClick={(event) => event.stopPropagation()}>
        <input
          autoFocus
          dir="auto"
          value={query}
          placeholder="فرمان یا نام یادداشت…"
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={async (event) => {
            if (event.key === 'Enter' && results[0]) {
              await results[0].run()
              setCommandOpen(false)
            }
          }}
        />
        <ul className="notes-search-results">
          {results.slice(0, 25).map((command) => (
            <li key={command.id}>
              <button
                type="button"
                onClick={async () => {
                  await command.run()
                  setCommandOpen(false)
                }}
              >
                <strong dir="auto">{command.label}</strong>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
