// @ts-nocheck
import { useRef } from 'react'
import { Columns2, Eye, Network, HelpCircle, Pencil, Search, Command, Trash2, Upload } from 'lucide-react'
import { importMarkdownFiles } from '../lib/importVault'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function NotesToolbar({ onDaily }) {
  const viewMode = useNotesUiStore((state) => state.viewMode)
  const setViewMode = useNotesUiStore((state) => state.setViewMode)
  const setSearchOpen = useNotesUiStore((state) => state.setSearchOpen)
  const setCommandOpen = useNotesUiStore((state) => state.setCommandOpen)
  const setGraphOpen = useNotesUiStore((state) => state.setGraphOpen)
  const setHelpOpen = useNotesUiStore((state) => state.setHelpOpen)
  const setTrashOpen = useNotesUiStore((state) => state.setTrashOpen)
  const userId = useNotesUiStore((state) => state.userId)
  const importVaultData = useNotesStore((state) => state.importVaultData)
  const fileRef = useRef(null)

  return (
    <div className="notes-toolbar">
      <div className="notes-view-toggle" role="group" aria-label="حالت نمایش">
        <button type="button" className={viewMode === 'edit' ? 'is-active' : ''} onClick={() => setViewMode('edit')} aria-label="ویرایش">
          <Pencil size={15} />
        </button>
        <button type="button" className={viewMode === 'preview' ? 'is-active' : ''} onClick={() => setViewMode('preview')} aria-label="پیش‌نمایش">
          <Eye size={15} />
        </button>
        <button type="button" className={viewMode === 'split' ? 'is-active' : ''} onClick={() => setViewMode('split')} aria-label="دوتکه">
          <Columns2 size={15} />
        </button>
      </div>
      <div className="notes-toolbar-actions">
        <button type="button" onClick={onDaily}>یادداشت امروز</button>
        <button type="button" aria-label="جستجو" onClick={() => setSearchOpen(true)}><Search size={15} /></button>
        <button type="button" aria-label="فرمان‌ها" onClick={() => setCommandOpen(true)}><Command size={15} /></button>
        <button type="button" aria-label="گراف" onClick={() => setGraphOpen(true)}><Network size={15} /></button>
        <button type="button" aria-label="سطل زباله" onClick={() => setTrashOpen(true)}><Trash2 size={15} /></button>
        <button type="button" aria-label="ورود فایل" onClick={() => fileRef.current?.click()}><Upload size={15} /></button>
        <button type="button" aria-label="راهنما" onClick={() => setHelpOpen(true)}><HelpCircle size={15} /></button>
        <input
          ref={fileRef}
          type="file"
          accept=".md,.zip"
          multiple
          hidden
          onChange={async (event) => {
            const files = event.target.files
            if (!files?.length) return
            const payload = await importMarkdownFiles(files)
            await importVaultData(userId, payload)
            event.target.value = ''
          }}
        />
      </div>
    </div>
  )
}
