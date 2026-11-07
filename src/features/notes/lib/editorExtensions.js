import { markdown, markdownLanguage } from '@codemirror/lang-markdown'
import { languages } from '@codemirror/language-data'
import { HighlightStyle, syntaxHighlighting, bracketMatching, indentOnInput } from '@codemirror/language'
import { tags } from '@lezer/highlight'
import {
  EditorView,
  drawSelection,
  dropCursor,
  highlightActiveLine,
  keymap,
  placeholder,
} from '@codemirror/view'
import { defaultKeymap, history, historyKeymap, indentWithTab } from '@codemirror/commands'
import { searchKeymap } from '@codemirror/search'
import { wrapSelection } from './editorFormat'
import { imageWidgets } from './imageWidgets'
import { collectImageFiles, filesFromHtmlImages, storeAndInsertImage } from './insertImage'
import { wikilinkCompletion } from './wikilinkComplete'
import { useNotesStore } from '../store/notesStore'

const EDITOR_FONT = '"Estedad", "Vazirmatn", Tahoma, sans-serif'

const markdownMarks = HighlightStyle.define([
  { tag: tags.heading, fontWeight: '700' },
  { tag: tags.strong, fontWeight: '700' },
  { tag: tags.emphasis, fontStyle: 'italic' },
  { tag: tags.strikethrough, textDecoration: 'line-through' },
  { tag: tags.monospace, fontFamily: 'ui-monospace, SFMono-Regular, Consolas, monospace' },
  { tag: tags.link, color: 'var(--link)' },
])

export function buildEditorExtensions(userId) {
  return [
    markdown({ base: markdownLanguage, codeLanguages: languages }),
    syntaxHighlighting(markdownMarks),
    imageWidgets(userId),
    wikilinkCompletion(() => useNotesStore.getState().notes),
    history(),
    drawSelection(),
    dropCursor(),
    indentOnInput(),
    bracketMatching(),
    highlightActiveLine(),
    EditorView.lineWrapping,
    EditorView.contentAttributes.of({ dir: 'rtl', spellcheck: 'false' }),
    placeholder('بنویس…  [[پیوند]]  #برچسب  $ریاضی$'),
    keymap.of([
      indentWithTab,
      ...defaultKeymap,
      ...historyKeymap,
      ...searchKeymap,
      {
        key: 'Mod-b',
        preventDefault: true,
        run: (view) => {
          wrapSelection(view, '**')
          return true
        },
      },
      {
        key: 'Mod-i',
        preventDefault: true,
        run: (view) => {
          wrapSelection(view, '*')
          return true
        },
      },
      {
        key: 'Mod-Shift-s',
        preventDefault: true,
        run: (view) => {
          wrapSelection(view, '~~')
          return true
        },
      },
    ]),
    EditorView.domEventHandlers({
      paste(event, view) {
        const data = event.clipboardData
        if (!data) return false
        const files = collectImageFiles(data)
        const html = data.getData('text/html') || ''
        if (!files.length && !html.includes('<img')) return false
        event.preventDefault()
        void (async () => {
          const images = files.length ? files : await filesFromHtmlImages(html)
          for (const file of images) {
            await storeAndInsertImage(userId, file, view)
          }
        })()
        return true
      },
    }),
    EditorView.theme({
      '&': {
        height: '100%',
        fontFamily: EDITOR_FONT,
        fontSize: '16.5px',
        backgroundColor: 'transparent',
        direction: 'rtl',
      },
      '.cm-scroller': {
        fontFamily: EDITOR_FONT,
        lineHeight: '1.75',
        overflow: 'auto',
      },
      '.cm-content': {
        caretColor: 'var(--accent)',
        padding: '12px 8px 64px',
        minHeight: '100%',
        textAlign: 'start',
      },
      '.cm-line': { padding: '0 2px' },
      '&.cm-focused .cm-cursor': {
        borderLeftColor: 'var(--accent)',
        borderLeftWidth: '2px',
      },
      '&.cm-focused .cm-selectionBackground, .cm-selectionBackground, ::selection': {
        backgroundColor: 'var(--accent-soft) !important',
      },
      '.cm-activeLine': { backgroundColor: 'var(--hover)' },
      '.cm-gutters': { background: 'transparent', border: 'none', color: 'var(--muted)' },
      '.cm-placeholder': { color: 'var(--muted)', opacity: 0.65 },
      '.notes-cm-image-wrap': {
        padding: '6px 0 10px',
        maxWidth: '100%',
      },
      '.notes-cm-image-wrap.is-inline': {
        display: 'inline-block',
        verticalAlign: 'middle',
        padding: '2px 0',
      },
      '.notes-cm-image-wrap.is-block': {
        display: 'block',
      },
      '.notes-cm-image': {
        display: 'block',
        maxWidth: 'min(100%, 520px)',
        maxHeight: '280px',
        width: 'auto',
        height: 'auto',
        objectFit: 'contain',
        borderRadius: '8px',
        border: '1px solid var(--line)',
        background: 'var(--surface-2)',
      },
      '.cm-tooltip': {
        fontFamily: EDITOR_FONT,
        border: '1px solid var(--line)',
        backgroundColor: 'var(--surface)',
        color: 'var(--text)',
        borderRadius: '10px',
        boxShadow: 'var(--shadow)',
      },
      '.cm-tooltip.cm-tooltip-autocomplete > ul > li[aria-selected]': {
        backgroundColor: 'var(--accent-soft)',
        color: 'var(--text)',
      },
    }),
  ]
}
