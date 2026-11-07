// @ts-nocheck
export function wrapSelection(view, before, after = before) {
  if (!view) return ''
  const { state } = view
  let { from, to } = state.selection.main
  if (from === to) {
    const expanded = expandToWord(state, from)
    from = expanded.from
    to = expanded.to
  }
  const selected = state.sliceDoc(from, to)
  const openStart = from - before.length
  const closeEnd = to + after.length
  if (
    openStart >= 0
    && closeEnd <= state.doc.length
    && state.sliceDoc(openStart, from) === before
    && state.sliceDoc(to, closeEnd) === after
  ) {
    view.dispatch({
      changes: [
        { from: openStart, to: from, insert: '' },
        { from: to, to: closeEnd, insert: '' },
      ],
      selection: { anchor: openStart, head: openStart + selected.length },
    })
  } else {
    const insert = `${before}${selected}${after}`
    view.dispatch({
      changes: { from, to, insert },
      selection: selected
        ? { anchor: from + before.length, head: from + before.length + selected.length }
        : { anchor: from + before.length },
    })
  }
  view.focus()
  return view.state.doc.toString()
}

export function wrapRangeInDoc(doc, from, to, before, after = before) {
  let start = from
  let end = to
  if (start === end) {
    const expanded = expandToWordInString(doc, start)
    start = expanded.from
    end = expanded.to
  }
  const selected = doc.slice(start, end)
  const openStart = start - before.length
  const closeEnd = end + after.length
  if (
    openStart >= 0
    && closeEnd <= doc.length
    && doc.slice(openStart, start) === before
    && doc.slice(end, closeEnd) === after
  ) {
    const next = `${doc.slice(0, openStart)}${selected}${doc.slice(closeEnd)}`
    return {
      next,
      selection: { anchor: openStart, head: openStart + selected.length },
    }
  }
  const insert = `${before}${selected}${after}`
  const next = `${doc.slice(0, start)}${insert}${doc.slice(end)}`
  return {
    next,
    selection: selected
      ? { anchor: start + before.length, head: start + before.length + selected.length }
      : { anchor: start + before.length },
  }
}

export function toggleLinePrefix(view, prefix) {
  if (!view) return ''
  const { state } = view
  const line = state.doc.lineAt(state.selection.main.from)
  const text = line.text
  const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const re = new RegExp(`^${escaped}`)
  const next = re.test(text)
    ? text.replace(re, '')
    : `${prefix}${text.replace(/^#{1,6}\s+/, '').replace(/^[-*]\s+(\[[ xX]\]\s+)?/, '').replace(/^\d+\.\s+/, '')}`
  view.dispatch({
    changes: { from: line.from, to: line.to, insert: next },
    selection: { anchor: line.from + next.length },
  })
  view.focus()
  return view.state.doc.toString()
}

export function toggleLinePrefixInDoc(doc, cursor, prefix) {
  const lineStart = doc.lastIndexOf('\n', Math.max(0, cursor - 1)) + 1
  const lineEndIdx = doc.indexOf('\n', cursor)
  const lineEnd = lineEndIdx === -1 ? doc.length : lineEndIdx
  const text = doc.slice(lineStart, lineEnd)
  const escaped = prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const re = new RegExp(`^${escaped}`)
  const nextLine = re.test(text)
    ? text.replace(re, '')
    : `${prefix}${text.replace(/^#{1,6}\s+/, '').replace(/^[-*]\s+(\[[ xX]\]\s+)?/, '').replace(/^\d+\.\s+/, '')}`
  const next = `${doc.slice(0, lineStart)}${nextLine}${doc.slice(lineEnd)}`
  return {
    next,
    selection: { anchor: lineStart + nextLine.length },
  }
}

export function insertSnippet(view, snippet, cursorOffset) {
  if (!view) return ''
  const { from, to } = view.state.selection.main
  view.dispatch({
    changes: { from, to, insert: snippet },
    selection: { anchor: from + (cursorOffset ?? snippet.length) },
  })
  view.focus()
  return view.state.doc.toString()
}

export function insertSnippetInDoc(doc, from, to, snippet, cursorOffset) {
  const next = `${doc.slice(0, from)}${snippet}${doc.slice(to)}`
  return {
    next,
    selection: { anchor: from + (cursorOffset ?? snippet.length) },
  }
}

export function insertAtCursor(view, text) {
  if (!view) return ''
  view.dispatch(view.state.replaceSelection(text))
  view.focus()
  return view.state.doc.toString()
}

export function insertAtCursorInDoc(doc, from, to, text) {
  const next = `${doc.slice(0, from)}${text}${doc.slice(to)}`
  return {
    next,
    selection: { anchor: from + text.length },
  }
}

function expandToWord(state, pos) {
  const line = state.doc.lineAt(pos)
  const text = line.text
  let local = pos - line.from
  if (!text || local < 0) return { from: pos, to: pos }
  while (local > 0 && isWordChar(text[local - 1])) local -= 1
  let end = pos - line.from
  while (end < text.length && isWordChar(text[end])) end += 1
  return { from: line.from + local, to: line.from + end }
}

function expandToWordInString(doc, pos) {
  let start = pos
  let end = pos
  while (start > 0 && isWordChar(doc[start - 1])) start -= 1
  while (end < doc.length && isWordChar(doc[end])) end += 1
  return { from: start, to: end }
}

function isWordChar(ch) {
  return /[\p{L}\p{N}_-]/u.test(ch)
}
