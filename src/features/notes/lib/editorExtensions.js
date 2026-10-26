import { markdown, markdownLanguage } from '@codemirror/lang-markdown'
import { languages } from '@codemirror/language-data'
import {
  EditorView,
  drawSelection,
  dropCursor,
  highlightActiveLine,
  keymap,
  placeholder,
} from '@codemirror/view'
import { defaultKeymap, history, historyKeymap, indentWithTab } from '@codemirror/commands'
import { bracketMatching, indentOnInput } from '@codemirror/language'
import { searchKeymap } from '@codemirror/search'
import { wrapSelection } from './editorFormat'
import { storeAndInsertImage } from './insertImage'
import { wikilinkCompletion } from './wikilinkComplete'
import { useNotesStore } from '../store/notesStore'

const EDITOR_FONT = '"Estedad", "Vazirmatn", Tahoma, sans-serif'

export function buildEditorExtensions(userId) {
  return [
    markdown({ base: markdownLanguage, codeLanguages: languages }),
    wikilinkCompletion(() => useNotesStore.getState().notes),
    history(),
    drawSelection(),
    dropCursor(),
    indentOnInput(),
    bracketMatching(),
    highlightActiveLine(),
    EditorView.lineWrapping,
    placeholder('بنویس…  [[پیوند]]  #برچسب  $ریاضی$'),
    keymap.of([
      indentWithTab,
      ...defaultKeymap,
      ...historyKeymap,
      ...searchKeymap,
      {
        key: 'Mod-b',
        run: (view) => {
          wrapSelection(view, '**')
          return true
        },
      },
      {
        key: 'Mod-i',
        run: (view) => {
          wrapSelection(view, '*')
          return true
        },
      },
      {
        key: 'Mod-Shift-k',
        run: (view) => {
          wrapSelection(view, '~~')
          return true
        },
      },
    ]),
    EditorView.domEventHandlers({
      paste(event, view) {
        const items = [...(event.clipboardData?.items || [])]
        const image = items.find((item) => item.type.startsWith('image/'))
        if (!image) return false
        event.preventDefault()
        const file = image.getAsFile()
        if (!file) return true
        storeAndInsertImage(userId, file, view)
        return true
      },
    }),
    EditorView.theme({
      '&': {
        height: '100%',
        fontFamily: EDITOR_FONT,
        fontSize: '16.5px',
        backgroundColor: 'transparent',
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
