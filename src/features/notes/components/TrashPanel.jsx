import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function TrashPanel() {
  const trash = useNotesStore((state) => state.trash)
  const restoreFromTrash = useNotesStore((state) => state.restoreFromTrash)
  const userId = useNotesUiStore((state) => state.userId)
  const setTrashOpen = useNotesUiStore((state) => state.setTrashOpen)
  const openNote = useNotesUiStore((state) => state.openNote)

  return (
    <div className="notes-modal-backdrop" onClick={() => setTrashOpen(false)}>
      <div className="notes-modal" role="dialog" aria-label="سطل زباله" onClick={(event) => event.stopPropagation()}>
        <strong>سطل زباله (۳۰ روز)</strong>
        {trash.length === 0 ? <p className="notes-muted">خالی است.</p> : (
          <ul className="notes-search-results">
            {trash.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  onClick={async () => {
                    const restored = await restoreFromTrash(userId, item.id)
                    if (restored?.kind === 'note') openNote(restored.data.id)
                  }}
                >
                  <strong dir="auto">
                    {item.kind === 'note' ? item.data.title : `پوشه: ${item.data.name}`}
                  </strong>
                  <span>{new Date(item.deletedAt).toLocaleDateString('fa-IR')}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
