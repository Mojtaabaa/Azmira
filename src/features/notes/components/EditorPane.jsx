import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import CodeMirror from '@uiw/react-codemirror'
import { EditorSelection } from '@codemirror/state'
import { buildEditorExtensions } from '../lib/editorExtensions'
import {
  insertAtCursor,
  insertAtCursorInDoc,
  insertSnippet,
  insertSnippetInDoc,
  toggleLinePrefix,
  toggleLinePrefixInDoc,
  wrapRangeInDoc,
  wrapSelection,
} from '../lib/editorFormat'
import { storeAndInsertImage } from '../lib/insertImage'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'
import EditorMetaBar from './EditorMetaBar'
import FormatToolbar from './FormatToolbar'
import MarkdownPreview from './MarkdownPreview'

export default function EditorPane({ userId, note }) {
  if (!note) {
    return (
      <div className="notes-empty">
        <p>یک یادداشت را از فهرست باز کن یا یادداشت تازه بساز.</p>
      </div>
    )
  }

  return <NoteEditor key={note.id} userId={userId} note={note} />
}

function NoteEditor({ userId, note }) {
  const updateNote = useNotesStore((state) => state.updateNote)
  const notes = useNotesStore((state) => state.notes)
  const createNote = useNotesStore((state) => state.createNote)
  const getNoteByTitle = useNotesStore((state) => state.getNoteByTitle)
  const viewMode = useNotesUiStore((state) => state.viewMode)
  const openNote = useNotesUiStore((state) => state.openNote)
  const setSaveState = useNotesUiStore((state) => state.setSaveState)
  const timer = useRef(null)
  const viewRef = useRef(null)
  const selectionRef = useRef({ from: 0, to: 0 })
  const pendingSelectionRef = useRef(null)
  const imageInputRef = useRef(null)
  const [titleDraft, setTitleDraft] = useState(note.title || '')
  const [contentDraft, setContentDraft] = useState(note.content || '')
  const [dragging, setDragging] = useState(false)
  const [metaOpen, setMetaOpen] = useState(false)
  const extensions = useMemo(() => buildEditorExtensions(userId), [userId])

  function scheduleSave(patch) {
    setSaveState('saving')
    if (timer.current) clearTimeout(timer.current)
    timer.current = setTimeout(async () => {
      await updateNote(userId, note.id, patch)
      setSaveState('saved')
    }, 400)
  }

  const handleEditorChange = useCallback((value) => {
    setContentDraft(value)
    setSaveState('saving')
    if (timer.current) clearTimeout(timer.current)
    timer.current = setTimeout(async () => {
      await updateNote(userId, note.id, { content: value })
      setSaveState('saved')
    }, 400)
  }, [note.id, updateNote, userId, setSaveState])

  const handleEditorUpdate = useCallback((vu) => {
    if (vu.selectionSet || vu.docChanged) {
      selectionRef.current = {
        from: vu.state.selection.main.from,
        to: vu.state.selection.main.to,
      }
    }
  }, [])

  function readSelection() {
    const view = viewRef.current
    if (view) {
      const { from, to } = view.state.selection.main
      selectionRef.current = { from, to }
      return { from, to, doc: view.state.doc.toString() }
    }
    const { from, to } = selectionRef.current
    return { from, to, doc: contentDraft }
  }

  function commitDoc(next, selection) {
    pendingSelectionRef.current = selection || null
    setContentDraft(next)
    scheduleSave({ content: next })
    requestAnimationFrame(() => {
      const view = viewRef.current
      const pending = pendingSelectionRef.current
      if (!view || !pending) return
      const max = view.state.doc.length
      const anchor = Math.min(pending.anchor ?? pending.from ?? 0, max)
      const head = Math.min(pending.head ?? pending.to ?? anchor, max)
      view.dispatch({
        selection: EditorSelection.single(anchor, head),
        scrollIntoView: true,
      })
      view.focus()
      pendingSelectionRef.current = null
    })
  }

  function applyWrap(before, after = before) {
    const { from, to, doc } = readSelection()
    const result = wrapRangeInDoc(doc, from, to, before, after)
    commitDoc(result.next, result.selection)
  }

  function applyTogglePrefix(prefix) {
    const { from, doc } = readSelection()
    const result = toggleLinePrefixInDoc(doc, from, prefix)
    commitDoc(result.next, result.selection)
  }

  function applySnippet(snippet, cursorOffset) {
    const { from, to, doc } = readSelection()
    const result = insertSnippetInDoc(doc, from, to, snippet, cursorOffset)
    commitDoc(result.next, result.selection)
  }

  function applyText(text) {
    const { from, to, doc } = readSelection()
    const result = insertAtCursorInDoc(doc, from, to, text)
    commitDoc(result.next, result.selection)
  }

  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current)
  }, [])

  async function commitTitle() {
    const next = titleDraft.trim() || 'یادداشت بدون عنوان'
    if (timer.current) clearTimeout(timer.current)
    const updated = await updateNote(userId, note.id, { title: next })
    if (updated?.title != null) setTitleDraft(updated.title)
    setSaveState('saved')
  }

  async function openWiki(title) {
    const existing = getNoteByTitle(title)
    if (existing) {
      openNote(existing.id)
      return
    }
    openNote((await createNote(userId, { title })).id)
  }

  async function toggleTask(checked, event) {
    const item = event.target.closest('li')
    if (!item) return
    const label = item.textContent.replace(/^\s*/, '').trim()
    const lines = contentDraft.split('\n')
    const index = lines.findIndex((line) => line.includes(label.slice(0, Math.min(40, label.length))))
    if (index < 0) return
    lines[index] = checked
      ? lines[index].replace(/- \[ \]/, '- [x]')
      : lines[index].replace(/- \[x\]/i, '- [ ]')
    const next = lines.join('\n')
    setContentDraft(next)
    scheduleSave({ content: next })
  }

  async function handleImageFiles(fileList) {
    const files = [...(fileList || [])].filter((file) => file.type.startsWith('image/'))
    if (!files.length) return
    let next = contentDraft
    for (const file of files) {
      const markdown = await storeAndInsertImage(userId, file, viewRef.current)
      if (!viewRef.current && markdown) next += markdown
    }
    if (viewRef.current) next = viewRef.current.state.doc.toString()
    setContentDraft(next)
    scheduleSave({ content: next })
  }

  const showEditor = viewMode === 'edit' || viewMode === 'split'
  const showPreview = viewMode === 'preview' || viewMode === 'split'

  return (
    <div className={`notes-editor-pane is-${viewMode}`}>
      <div className="notes-title-row">
        <input
          className="notes-title-input"
          dir="auto"
          value={titleDraft}
          maxLength={160}
          spellCheck={false}
          aria-label="عنوان یادداشت"
          placeholder="عنوان یادداشت"
          onChange={(event) => {
            setTitleDraft(event.target.value)
            scheduleSave({ title: event.target.value })
          }}
          onBlur={() => commitTitle()}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault()
              event.currentTarget.blur()
            }
          }}
        />
        <button
          type="button"
          className="notes-meta-toggle"
          aria-expanded={metaOpen}
          aria-label="ابزارهای بیشتر"
          onClick={() => setMetaOpen((open) => !open)}
        >
          ⋯
        </button>
      </div>
      {metaOpen ? (
        <EditorMetaBar
          note={note}
          titleDraft={titleDraft}
          contentDraft={contentDraft}
          setTitleDraft={setTitleDraft}
          setContentDraft={setContentDraft}
          updateNote={updateNote}
          userId={userId}
          setSaveState={setSaveState}
        />
      ) : null}
      {showEditor ? (
        <FormatToolbar
          disabled={!showEditor}
          onWrap={applyWrap}
          onTogglePrefix={applyTogglePrefix}
          onInsertSnippet={applySnippet}
          onInsertText={applyText}
          onInsertImage={() => imageInputRef.current?.click()}
        />
      ) : null}
      <input
        ref={imageInputRef}
        type="file"
        accept="image/*"
        multiple
        hidden
        onChange={async (event) => {
          await handleImageFiles(event.target.files)
          event.target.value = ''
        }}
      />
      <div className="notes-split">
        {showEditor ? (
          <div
            className={`notes-source${dragging ? ' is-drop' : ''}`}
            dir="auto"
            onDragOver={(event) => {
              if ([...event.dataTransfer.types].includes('Files')) {
                event.preventDefault()
                setDragging(true)
              }
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={async (event) => {
              event.preventDefault()
              setDragging(false)
              await handleImageFiles(event.dataTransfer.files)
            }}
          >
            {dragging ? <div className="notes-drop-hint">تصویر را اینجا رها کن</div> : null}
            <CodeMirror
              value={contentDraft}
              height="100%"
              theme="none"
              basicSetup={false}
              extensions={extensions}
              onCreateEditor={(view) => {
                viewRef.current = view
                selectionRef.current = {
                  from: view.state.selection.main.from,
                  to: view.state.selection.main.to,
                }
              }}
              onUpdate={handleEditorUpdate}
              onChange={handleEditorChange}
            />
          </div>
        ) : null}
        {showPreview ? (
          <MarkdownPreview
            content={contentDraft}
            notes={notes}
            onOpenWiki={openWiki}
            onToggleTask={toggleTask}
          />
        ) : null}
      </div>
    </div>
  )
}
