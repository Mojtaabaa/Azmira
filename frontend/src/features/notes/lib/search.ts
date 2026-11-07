// @ts-nocheck
import Fuse from 'fuse.js'
import { extractTags } from './parseTags'

export function buildSearchIndex(notes) {
  return new Fuse(notes, {
    keys: [
      { name: 'title', weight: 0.5 },
      { name: 'content', weight: 0.35 },
      { name: 'tags', weight: 0.15 },
    ],
    threshold: 0.38,
    ignoreLocation: true,
    includeMatches: true,
    includeScore: true,
  })
}

export function parseSearchQuery(raw) {
  const text = typeof raw === 'string' ? raw.trim() : ''
  const tags = []
  const paths = []
  let rest = text
  rest = rest.replace(/\btag:#?([\p{L}\p{N}_/-]+)/gu, (_, tag) => {
    tags.push(tag)
    return ' '
  })
  rest = rest.replace(/\bpath:([^\s]+)/gi, (_, path) => {
    paths.push(path)
    return ' '
  })
  return { text: rest.trim(), tags, paths }
}

export function searchNotes(notes, folders, query) {
  const parsed = parseSearchQuery(query)
  let pool = notes.map((note) => ({
    ...note,
    tags: note.tags?.length ? note.tags : extractTags(note.content || ''),
  }))

  if (parsed.tags.length) {
    pool = pool.filter((note) => parsed.tags.every((tag) => note.tags.includes(tag)))
  }

  if (parsed.paths.length) {
    const folderMap = Object.fromEntries(folders.map((folder) => [folder.id, folder]))
    pool = pool.filter((note) => {
      const path = folderPath(note.folderId, folderMap).toLowerCase()
      return parsed.paths.every((part) => path.includes(part.toLowerCase()))
    })
  }

  if (!parsed.text) {
    return pool
      .slice()
      .sort((a, b) => b.updatedAt - a.updatedAt)
      .map((note) => ({ item: note, matches: [] }))
  }

  return buildSearchIndex(pool).search(parsed.text)
}

function folderPath(folderId, map) {
  const parts = []
  let cursor = folderId
  const guard = new Set()
  while (cursor && map[cursor] && !guard.has(cursor)) {
    guard.add(cursor)
    parts.unshift(map[cursor].name)
    cursor = map[cursor].parentId
  }
  return parts.join('/')
}

export function snippetFromMatches(note, matches) {
  return snippetParts(note, matches).map((part) => part.text).join('')
}

export function snippetParts(note, matches) {
  const content = note.content || ''
  const contentHit = matches?.find((match) => match.key === 'content')
  if (!contentHit?.indices?.length) {
    return [{ text: content.slice(0, 120), hit: false }]
  }
  const [start, end] = contentHit.indices[0]
  const from = Math.max(0, start - 40)
  const to = Math.min(content.length, end + 40)
  const parts = []
  if (from > 0) parts.push({ text: '…', hit: false })
  if (from < start) parts.push({ text: content.slice(from, start), hit: false })
  parts.push({ text: content.slice(start, end + 1), hit: true })
  if (end + 1 < to) parts.push({ text: content.slice(end + 1, to), hit: false })
  if (to < content.length) parts.push({ text: '…', hit: false })
  return parts
}
