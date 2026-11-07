import { useMemo, useState } from 'react'
import { FilePlus, FolderPlus, Pin, Trash2 } from 'lucide-react'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function Explorer({ userId }) {
  const notes = useNotesStore((state) => state.notes)
  const folders = useNotesStore((state) => state.folders)
  const createNote = useNotesStore((state) => state.createNote)
  const createFolder = useNotesStore((state) => state.createFolder)
  const updateFolder = useNotesStore((state) => state.updateFolder)
  const deleteNote = useNotesStore((state) => state.deleteNote)
  const deleteFolder = useNotesStore((state) => state.deleteFolder)
  const moveNote = useNotesStore((state) => state.moveNote)
  const updateNote = useNotesStore((state) => state.updateNote)
  const openNote = useNotesUiStore((state) => state.openNote)
  const closeTab = useNotesUiStore((state) => state.closeTab)
  const activeNoteId = useNotesUiStore((state) => state.activeNoteId)
  const sortBy = useNotesUiStore((state) => state.sortBy)
  const setSortBy = useNotesUiStore((state) => state.setSortBy)
  const collapsedFolders = useNotesUiStore((state) => state.collapsedFolders)
  const toggleFolder = useNotesUiStore((state) => state.toggleFolder)
  const tagFilter = useNotesUiStore((state) => state.tagFilter)
  const setTagFilter = useNotesUiStore((state) => state.setTagFilter)
  const [menu, setMenu] = useState(null)

  const tree = useMemo(() => buildTree(folders, notes, sortBy, tagFilter), [folders, notes, sortBy, tagFilter])

  async function handleNewNote(folderId = null) {
    const note = await createNote(userId, { folderId })
    if (note?.id) openNote(note.id)
  }

  return (
    <aside className="notes-explorer">
      <div className="notes-explorer-head">
        <strong>یادداشت‌ها</strong>
        <div className="notes-explorer-actions">
          <button type="button" aria-label="یادداشت تازه" onClick={() => handleNewNote(null)}>
            <FilePlus size={16} />
          </button>
          <button type="button" aria-label="پوشه تازه" onClick={() => createFolder(userId)}>
            <FolderPlus size={16} />
          </button>
        </div>
      </div>
      <label className="notes-sort">
        مرتب‌سازی
        <select value={sortBy} onChange={(event) => setSortBy(event.target.value)}>
          <option value="modified">آخرین تغییر</option>
          <option value="created">تاریخ ساخت</option>
          <option value="name">نام</option>
        </select>
      </label>
      {tagFilter ? (
        <div className="notes-tag-filter">
          <span>#{tagFilter}</span>
          <button type="button" onClick={() => setTagFilter('')}>پاک کردن</button>
        </div>
      ) : null}
      <ul
        className="notes-tree"
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => {
          const noteId = event.dataTransfer.getData('text/note-id')
          if (noteId) moveNote(userId, noteId, null)
        }}
      >
        {tree.pinned.map((note) => (
          <NoteRow
            key={note.id}
            note={note}
            active={note.id === activeNoteId}
            onOpen={() => openNote(note.id)}
            onMenu={(event) => setMenu({ type: 'note', id: note.id, x: event.clientX, y: event.clientY })}
          />
        ))}
        {tree.roots.map((node) => (
          <FolderNode
            key={node.folder.id}
            node={node}
            depth={0}
            activeNoteId={activeNoteId}
            collapsedFolders={collapsedFolders}
            onToggle={toggleFolder}
            onOpenNote={openNote}
            onMenu={setMenu}
            onNewNote={handleNewNote}
            onDropNote={(noteId, folderId) => moveNote(userId, noteId, folderId)}
          />
        ))}
        {tree.loose.map((note) => (
          <NoteRow
            key={note.id}
            note={note}
            active={note.id === activeNoteId}
            onOpen={() => openNote(note.id)}
            onMenu={(event) => setMenu({ type: 'note', id: note.id, x: event.clientX, y: event.clientY })}
          />
        ))}
      </ul>
      {menu ? (
        <ContextMenu
          menu={menu}
          onClose={() => setMenu(null)}
          onRenameNote={async (id) => {
            const note = notes.find((item) => item.id === id)
            const title = window.prompt('عنوان تازه', note?.title || '')
            if (title) await updateNote(userId, id, { title })
          }}
          onDeleteNote={async (id) => {
            await deleteNote(userId, id)
            closeTab(id)
          }}
          onPinNote={async (id) => {
            const note = notes.find((item) => item.id === id)
            if (note) await updateNote(userId, id, { pinned: !note.pinned })
          }}
          onRenameFolder={async (id) => {
            const folder = folders.find((item) => item.id === id)
            const name = window.prompt('نام پوشه', folder?.name || '')
            if (name) await updateFolder(userId, id, { name })
          }}
          onDeleteFolder={async (id) => {
            await deleteFolder(userId, id)
          }}
          onNewInFolder={(id) => handleNewNote(id)}
        />
      ) : null}
    </aside>
  )
}

function FolderNode({ node, depth, activeNoteId, collapsedFolders, onToggle, onOpenNote, onMenu, onNewNote, onDropNote }) {
  const collapsed = Boolean(collapsedFolders[node.folder.id])
  return (
    <li className="notes-folder" style={{ paddingInlineStart: depth * 12 }}>
      <div
        className="notes-folder-row"
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => {
          const noteId = event.dataTransfer.getData('text/note-id')
          if (noteId) onDropNote(noteId, node.folder.id)
        }}
        onContextMenu={(event) => {
          event.preventDefault()
          onMenu({ type: 'folder', id: node.folder.id, x: event.clientX, y: event.clientY })
        }}
      >
        <button type="button" onClick={() => onToggle(node.folder.id)} aria-expanded={!collapsed}>
          <span className="notes-chevron" aria-hidden="true">{collapsed ? '›' : '⌄'}</span>
          <span dir="auto">{node.folder.name}</span>
        </button>
      </div>
      {collapsed ? null : (
        <ul>
          {node.children.map((child) => (
            <FolderNode
              key={child.folder.id}
              node={child}
              depth={depth + 1}
              activeNoteId={activeNoteId}
              collapsedFolders={collapsedFolders}
              onToggle={onToggle}
              onOpenNote={onOpenNote}
              onMenu={onMenu}
              onNewNote={onNewNote}
              onDropNote={onDropNote}
            />
          ))}
          {node.notes.map((note) => (
            <NoteRow
              key={note.id}
              note={note}
              active={note.id === activeNoteId}
              depth={depth + 1}
              onOpen={() => onOpenNote(note.id)}
              onMenu={(event) => onMenu({ type: 'note', id: note.id, x: event.clientX, y: event.clientY })}
            />
          ))}
        </ul>
      )}
    </li>
  )
}

function NoteRow({ note, active, onOpen, onMenu, depth = 0 }) {
  return (
    <li style={{ paddingInlineStart: depth * 12 }}>
      <button
        type="button"
        className={active ? 'notes-file is-active' : 'notes-file'}
        draggable
        onDragStart={(event) => event.dataTransfer.setData('text/note-id', note.id)}
        onClick={onOpen}
        onContextMenu={(event) => {
          event.preventDefault()
          onMenu(event)
        }}
      >
        {note.pinned ? <Pin size={12} /> : null}
        <span dir="auto">{note.title}</span>
      </button>
    </li>
  )
}

function ContextMenu({ menu, onClose, onRenameNote, onDeleteNote, onPinNote, onRenameFolder, onDeleteFolder, onNewInFolder }) {
  return (
    <div className="notes-menu-backdrop" onClick={onClose} onContextMenu={(event) => { event.preventDefault(); onClose() }}>
      <ul className="notes-menu" style={{ left: menu.x, top: menu.y }} role="menu">
        {menu.type === 'note' ? (
          <>
            <li><button type="button" onClick={() => { onRenameNote(menu.id); onClose() }}>تغییر نام</button></li>
            <li><button type="button" onClick={() => { onPinNote(menu.id); onClose() }}>سنجاق</button></li>
            <li><button type="button" onClick={() => { onDeleteNote(menu.id); onClose() }}><Trash2 size={14} /> حذف</button></li>
          </>
        ) : (
          <>
            <li><button type="button" onClick={() => { onNewInFolder(menu.id); onClose() }}>یادداشت تازه</button></li>
            <li><button type="button" onClick={() => { onRenameFolder(menu.id); onClose() }}>تغییر نام</button></li>
            <li><button type="button" onClick={() => { onDeleteFolder(menu.id); onClose() }}>حذف پوشه</button></li>
          </>
        )}
      </ul>
    </div>
  )
}

function buildTree(folders, notes, sortBy, tagFilter) {
  const sorted = sortNotes(notes, sortBy).filter((note) => !tagFilter || (note.tags || []).includes(tagFilter))
  const pinned = sorted.filter((note) => note.pinned)
  const unpinned = sorted.filter((note) => !note.pinned)
  const byParent = new Map()
  for (const folder of folders) {
    const key = folder.parentId || 'root'
    if (!byParent.has(key)) byParent.set(key, [])
    byParent.get(key).push(folder)
  }
  function nest(parentId) {
    return (byParent.get(parentId || 'root') || []).map((folder) => ({
      folder,
      children: nest(folder.id),
      notes: unpinned.filter((note) => note.folderId === folder.id),
    }))
  }
  return {
    pinned,
    roots: nest(null),
    loose: unpinned.filter((note) => !note.folderId),
  }
}

function sortNotes(notes, sortBy) {
  const list = notes.slice()
  if (sortBy === 'name') return list.sort((a, b) => a.title.localeCompare(b.title, 'fa'))
  if (sortBy === 'created') return list.sort((a, b) => b.createdAt - a.createdAt)
  return list.sort((a, b) => b.updatedAt - a.updatedAt)
}
