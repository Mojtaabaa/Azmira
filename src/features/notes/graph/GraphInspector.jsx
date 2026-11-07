import { useMemo } from 'react'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function GraphInspector({ node, onCloseGraph }) {
  const notes = useNotesStore((state) => state.notes)
  const updateNote = useNotesStore((state) => state.updateNote)
  const createNote = useNotesStore((state) => state.createNote)
  const getNoteByTitle = useNotesStore((state) => state.getNoteByTitle)
  const openNote = useNotesUiStore((state) => state.openNote)
  const activeNoteId = useNotesUiStore((state) => state.activeNoteId)
  const userId = useNotesUiStore((state) => state.userId)
  const setGraphOpen = useNotesUiStore((state) => state.setGraphOpen)

  const note = useMemo(
    () => (node && !node.unresolved ? notes.find((item) => item.id === node.id) : null),
    [node, notes],
  )
  const active = notes.find((item) => item.id === activeNoteId)
  const alreadyLinked = Boolean(
    active
    && note
    && (active.outgoingLinks || []).some((title) => title.trim().toLowerCase() === note.title.trim().toLowerCase()),
  )

  if (!node) {
    return (
      <aside className="notes-graph-inspector">
        <h3>گره</h3>
        <p className="notes-muted">روی یک گره بزن تا یادداشت باز شود. Shift+کلیک از یادداشت فعلی پیوند می‌سازد.</p>
      </aside>
    )
  }

  return (
    <aside className="notes-graph-inspector">
      <h3 dir="auto">{node.title}</h3>
      {node.unresolved ? (
        <p className="notes-muted">این عنوان هنوز فایل ندارد.</p>
      ) : (
        <>
          <p className="notes-muted">
            {(note?.outgoingLinks || []).length} خروجی
            {note?.tags?.length ? ` · ${note.tags.map((tag) => `#${tag}`).join(' ')}` : ''}
          </p>
          {(note?.outgoingLinks || []).length ? (
            <ul className="notes-link-list">
              {note.outgoingLinks.slice(0, 8).map((title) => (
                <li key={title}>
                  <button type="button" dir="auto" onClick={() => openTitle(title, getNoteByTitle, createNote, userId, openNote)}>
                    {title}
                  </button>
                </li>
              ))}
            </ul>
          ) : null}
        </>
      )}
      <div className="notes-graph-inspector-actions">
        {node.unresolved ? (
          <button
            type="button"
            className="button"
            onClick={async () => {
              const created = await createNote(userId, { title: node.title })
              openNote(created.id)
              setGraphOpen(false)
              onCloseGraph?.()
            }}
          >
            ساخت یادداشت
          </button>
        ) : (
          <button
            type="button"
            className="button"
            onClick={() => {
              openNote(node.id)
              setGraphOpen(false)
              onCloseGraph?.()
            }}
          >
            باز کردن
          </button>
        )}
        {active && note && active.id !== note.id ? (
          <button
            type="button"
            className="button"
            disabled={alreadyLinked}
            onClick={async () => {
              const next = `${active.content || ''}${active.content?.endsWith('\n') ? '' : '\n'}[[${note.title}]]\n`
              await updateNote(userId, active.id, { content: next })
            }}
          >
            {alreadyLinked ? 'پیوند دارد' : `پیوند از «${active.title}»`}
          </button>
        ) : null}
      </div>
    </aside>
  )
}

async function openTitle(title, getNoteByTitle, createNote, userId, openNote) {
  const existing = getNoteByTitle(title)
  if (existing) {
    openNote(existing.id)
    return
  }
  openNote((await createNote(userId, { title })).id)
}
