import { autocompletion } from '@codemirror/autocomplete'
import Fuse from 'fuse.js'

function optionsFromNotes(notes, query) {
  const list = notes || []
  if (!query) {
    return list.slice(0, 20).map((note) => ({
      label: note.title,
      type: 'text',
      apply: `${note.title}]]`,
    }))
  }
  const fuse = new Fuse(list, { keys: ['title'], threshold: 0.4, ignoreLocation: true })
  return fuse.search(query).slice(0, 20).map((row) => ({
    label: row.item.title,
    type: 'text',
    apply: `${row.item.title}]]`,
  }))
}

export function wikilinkCompletion(getNotes) {
  return autocompletion({
    override: [
      (context) => {
        const before = context.matchBefore(/\[\[([^\]]*)$/)
        if (!before) return null
        if (before.text === '[[' && !context.explicit) {
          return {
            from: before.to,
            options: optionsFromNotes(getNotes(), ''),
          }
        }
        const query = before.text.slice(2)
        return {
          from: before.from + 2,
          options: optionsFromNotes(getNotes(), query),
        }
      },
    ],
  })
}
