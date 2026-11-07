// @ts-nocheck
import { useMemo } from 'react'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

const TABS = [
  { id: 'backlinks', label: 'پیوندهای ورودی' },
  { id: 'outgoing', label: 'خروجی' },
  { id: 'outline', label: 'طرح' },
  { id: 'tags', label: 'برچسب' },
]

export default function RightPanel({ note }) {
  const rightTab = useNotesUiStore((state) => state.rightTab)
  const setRightTab = useNotesUiStore((state) => state.setRightTab)
  const setTagFilter = useNotesUiStore((state) => state.setTagFilter)
  const openNote = useNotesUiStore((state) => state.openNote)
  const backlinksFor = useNotesStore((state) => state.backlinksFor)
  const getNoteByTitle = useNotesStore((state) => state.getNoteByTitle)
  const tagCounts = useNotesStore((state) => state.tagCounts)
  const createNote = useNotesStore((state) => state.createNote)
  const userId = useNotesUiStore((state) => state.userId)

  const backlinks = useMemo(() => (note ? backlinksFor(note.title) : []), [note, backlinksFor])
  const outline = useMemo(() => buildOutline(note?.content || ''), [note])
  const tags = tagCounts()

  async function openLink(title) {
    const existing = getNoteByTitle(title)
    if (existing) {
      openNote(existing.id)
      return
    }
    const created = await createNote(userId, { title })
    openNote(created.id)
  }

  return (
    <aside className="notes-right">
      <div className="notes-right-tabs">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={rightTab === tab.id ? 'is-active' : ''}
            onClick={() => setRightTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div className="notes-right-body">
        {!note && rightTab !== 'tags' ? <p className="notes-muted">یادداشتی انتخاب نشده.</p> : null}
        {rightTab === 'backlinks' && note ? (
          backlinks.length === 0 ? <p className="notes-muted">پیوند ورودی نیست.</p> : (
            <ul className="notes-link-list">
              {backlinks.map(({ note: item, snippets }) => (
                <li key={item.id}>
                  <button type="button" onClick={() => openNote(item.id)} dir="auto">{item.title}</button>
                  {snippets.map((snippet) => (
                    <p key={snippet} className="notes-snippet" dir="auto">{snippet}</p>
                  ))}
                </li>
              ))}
            </ul>
          )
        ) : null}
        {rightTab === 'outgoing' && note ? (
          (note.outgoingLinks || []).length === 0 ? <p className="notes-muted">پیوند خروجی نیست.</p> : (
            <ul className="notes-link-list">
              {note.outgoingLinks.map((title) => {
                const exists = Boolean(getNoteByTitle(title))
                return (
                  <li key={title}>
                    <button
                      type="button"
                      className={exists ? '' : 'is-unresolved'}
                      onClick={() => openLink(title)}
                      dir="auto"
                    >
                      {title}
                    </button>
                  </li>
                )
              })}
            </ul>
          )
        ) : null}
        {rightTab === 'outline' && note ? (
          outline.length === 0 ? <p className="notes-muted">عنوانی نیست.</p> : (
            <ul className="notes-outline">
              {outline.map((item) => (
                <li key={`${item.level}-${item.text}`} style={{ paddingInlineStart: (item.level - 1) * 12 }}>
                  <button type="button" onClick={() => scrollToHeading(item.text)} dir="auto">
                    {item.text}
                  </button>
                </li>
              ))}
            </ul>
          )
        ) : null}
        {rightTab === 'tags' ? (
          tags.length === 0 ? <p className="notes-muted">برچسبی نیست.</p> : (
            <ul className="notes-tag-list">
              {tags.map(({ tag, count }) => (
                <li key={tag}>
                  <button type="button" onClick={() => setTagFilter(tag)}>
                    #{tag} <span>{count}</span>
                  </button>
                </li>
              ))}
              <li>
                <button type="button" onClick={() => setTagFilter('')}>پاک کردن فیلتر</button>
              </li>
            </ul>
          )
        ) : null}
      </div>
    </aside>
  )
}

function buildOutline(content) {
  return content
    .split('\n')
    .map((line) => {
      const match = /^(#{1,6})\s+(.+)$/.exec(line.trim())
      if (!match) return null
      return { level: match[1].length, text: match[2].trim() }
    })
    .filter(Boolean)
}

function scrollToHeading(text) {
  const preview = [...document.querySelectorAll('.notes-preview h1, .notes-preview h2, .notes-preview h3, .notes-preview h4, .notes-preview h5, .notes-preview h6')]
  const hit = preview.find((node) => node.textContent.trim() === text)
  if (hit) {
    hit.scrollIntoView({ behavior: 'smooth', block: 'start' })
    return
  }
  const lines = [...document.querySelectorAll('.notes-source .cm-line')]
  const sourceHit = lines.find((line) => {
    const value = line.textContent || ''
    return /^#{1,6}\s+/.test(value) && value.replace(/^#{1,6}\s+/, '').trim() === text
  })
  sourceHit?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}
