// @ts-nocheck
import { useEffect, useMemo, useRef, useState } from 'react'
import Fuse from 'fuse.js'
import {
  Bold,
  Italic,
  Strikethrough,
  Code,
  Heading1,
  Heading2,
  Heading3,
  List,
  ListOrdered,
  ListChecks,
  Quote,
  Link2,
  ImagePlus,
  SquareCode,
  Brackets,
} from 'lucide-react'
import { useNotesStore } from '../store/notesStore'

export default function FormatToolbar({
  onWrap,
  onTogglePrefix,
  onInsertSnippet,
  onInsertText,
  onInsertImage,
  disabled,
}) {
  const notes = useNotesStore((state) => state.notes)
  const [linkOpen, setLinkOpen] = useState(false)
  const [linkQuery, setLinkQuery] = useState('')
  const linkRef = useRef(null)

  useEffect(() => {
    if (!linkOpen) return undefined
    function onDocClick(event) {
      if (!linkRef.current?.contains(event.target)) {
        setLinkOpen(false)
        setLinkQuery('')
      }
    }
    document.addEventListener('mousedown', onDocClick)
    return () => document.removeEventListener('mousedown', onDocClick)
  }, [linkOpen])

  const linkResults = useMemo(() => {
    const list = notes || []
    if (!linkQuery.trim()) return list.slice(0, 12)
    return new Fuse(list, { keys: ['title'], threshold: 0.4, ignoreLocation: true })
      .search(linkQuery)
      .slice(0, 12)
      .map((row) => row.item)
  }, [notes, linkQuery])

  function insertWiki(title) {
    onInsertText?.(`[[${title}]]`)
    setLinkOpen(false)
    setLinkQuery('')
  }

  return (
    <div
      className="notes-format-bar"
      role="toolbar"
      aria-label="قالب‌بندی"
      onMouseDown={(event) => {
        if (event.target.closest('input, textarea, select')) return
        event.preventDefault()
      }}
    >
      <FmtBtn label="ضخیم" title="ضخیم (Ctrl+B)" disabled={disabled} onClick={() => onWrap?.('**')}>
        <Bold size={14} />
      </FmtBtn>
      <FmtBtn label="کج" title="کج (Ctrl+I)" disabled={disabled} onClick={() => onWrap?.('*')}>
        <Italic size={14} />
      </FmtBtn>
      <FmtBtn label="خط‌خورده" disabled={disabled} onClick={() => onWrap?.('~~')}>
        <Strikethrough size={14} />
      </FmtBtn>
      <FmtBtn label="کد" disabled={disabled} onClick={() => onWrap?.('`')}>
        <Code size={14} />
      </FmtBtn>
      <span className="notes-format-sep" aria-hidden="true" />
      <FmtBtn label="عنوان ۱" disabled={disabled} onClick={() => onTogglePrefix?.('# ')}>
        <Heading1 size={14} />
      </FmtBtn>
      <FmtBtn label="عنوان ۲" disabled={disabled} onClick={() => onTogglePrefix?.('## ')}>
        <Heading2 size={14} />
      </FmtBtn>
      <FmtBtn label="عنوان ۳" disabled={disabled} onClick={() => onTogglePrefix?.('### ')}>
        <Heading3 size={14} />
      </FmtBtn>
      <span className="notes-format-sep" aria-hidden="true" />
      <FmtBtn label="فهرست" disabled={disabled} onClick={() => onTogglePrefix?.('- ')}>
        <List size={14} />
      </FmtBtn>
      <FmtBtn label="فهرست شماره‌دار" disabled={disabled} onClick={() => onTogglePrefix?.('1. ')}>
        <ListOrdered size={14} />
      </FmtBtn>
      <FmtBtn label="کارها" disabled={disabled} onClick={() => onTogglePrefix?.('- [ ] ')}>
        <ListChecks size={14} />
      </FmtBtn>
      <FmtBtn label="نقل‌قول" disabled={disabled} onClick={() => onTogglePrefix?.('> ')}>
        <Quote size={14} />
      </FmtBtn>
      <span className="notes-format-sep" aria-hidden="true" />
      <FmtBtn
        label="پیوند وب"
        disabled={disabled}
        onClick={() => onInsertSnippet?.('[](https://)', 1)}
      >
        <Link2 size={14} />
      </FmtBtn>
      <div className="notes-link-note" ref={linkRef}>
        <FmtBtn
          label="پیوند یادداشت"
          title="اتصال به یادداشت برای گراف [[...]]"
          disabled={disabled}
          onClick={() => setLinkOpen((open) => !open)}
        >
          <Brackets size={14} />
        </FmtBtn>
        {linkOpen ? (
          <div className="notes-link-pop" role="listbox" aria-label="انتخاب یادداشت">
            <input
              autoFocus
              dir="auto"
              value={linkQuery}
              placeholder="جستجوی یادداشت…"
              onChange={(event) => setLinkQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Escape') setLinkOpen(false)
                if (event.key === 'Enter' && linkResults[0]) {
                  event.preventDefault()
                  insertWiki(linkResults[0].title)
                }
              }}
            />
            <ul>
              {linkResults.map((note) => (
                <li key={note.id}>
                  <button
                    type="button"
                    dir="auto"
                    onMouseDown={(event) => event.preventDefault()}
                    onClick={() => insertWiki(note.title)}
                  >
                    {note.title}
                  </button>
                </li>
              ))}
              {linkResults.length === 0 ? <li className="notes-muted">یادداشتی نیست</li> : null}
            </ul>
            <button
              type="button"
              className="notes-link-new"
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => insertWiki(linkQuery.trim() || 'یادداشت تازه')}
            >
              درج [[{linkQuery.trim() || 'یادداشت تازه'}]]
            </button>
          </div>
        ) : null}
      </div>
      <FmtBtn label="بلوک کد" disabled={disabled} onClick={() => onInsertSnippet?.('\n```\n\n```\n', 5)}>
        <SquareCode size={14} />
      </FmtBtn>
      <FmtBtn label="تصویر" disabled={disabled} onClick={() => onInsertImage?.()}>
        <ImagePlus size={14} />
      </FmtBtn>
      <FmtBtn label="خط افقی" disabled={disabled} onClick={() => onInsertText?.('\n---\n')}>
        ―
      </FmtBtn>
    </div>
  )
}

function FmtBtn({ label, title, disabled, onClick, children }) {
  return (
    <button
      type="button"
      aria-label={label}
      title={title || label}
      disabled={disabled}
      onMouseDown={(event) => event.preventDefault()}
      onClick={onClick}
    >
      {children}
    </button>
  )
}
