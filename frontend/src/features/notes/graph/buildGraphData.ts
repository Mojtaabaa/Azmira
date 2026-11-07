// @ts-nocheck
import { SUBJECT_COLORS } from '../../../data/subjects'
import { extractTags } from '../lib/parseTags'

function readCssColor(name, fallback) {
  if (typeof document === 'undefined') return fallback
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  return value || fallback
}

export function buildGraphData(notes, {
  search = '',
  showOrphans = true,
  showUnresolved = false,
  existingOnly = true,
  folderFilter = '',
  tagFilter = '',
  groups = [],
  focusId = null,
  depth = null,
} = {}) {
  const byTitle = new Map(notes.map((note) => [note.title.trim().toLowerCase(), note]))
  const allowed = new Set()

  if (depth != null) {
    if (!focusId) return { nodes: [], links: [] }
    const start = notes.find((note) => note.id === focusId)
    if (!start) return { nodes: [], links: [] }
    const queue = [{ id: start.id, d: 0 }]
    const seen = new Set([start.id])
    while (queue.length) {
      const current = queue.shift()
      allowed.add(current.id)
      if (current.d >= depth) continue
      const note = notes.find((item) => item.id === current.id)
      if (!note) continue
      for (const title of note.outgoingLinks || []) {
        const target = byTitle.get(title.trim().toLowerCase())
        if (target && !seen.has(target.id)) {
          seen.add(target.id)
          queue.push({ id: target.id, d: current.d + 1 })
        }
      }
      const selfTitle = note.title.trim().toLowerCase()
      for (const other of notes) {
        const hits = (other.outgoingLinks || []).some((link) => link.trim().toLowerCase() === selfTitle)
        if (hits && !seen.has(other.id)) {
          seen.add(other.id)
          queue.push({ id: other.id, d: current.d + 1 })
        }
      }
    }
  }

  let nodes = notes.map((note) => {
    const tags = note.tags?.length ? note.tags : extractTags(note.content || '')
    return {
      id: note.id,
      title: note.title,
      folderId: note.folderId,
      tags,
      links: note.outgoingLinks || [],
      unresolved: false,
    }
  })

  if (allowed.size) nodes = nodes.filter((node) => allowed.has(node.id))
  if (folderFilter) nodes = nodes.filter((node) => node.folderId === folderFilter)
  if (tagFilter) nodes = nodes.filter((node) => node.tags.includes(tagFilter))
  if (search.trim()) {
    const q = search.trim().toLowerCase()
    nodes = nodes.filter((node) => node.title.toLowerCase().includes(q))
  }

  const nodeIds = new Set(nodes.map((node) => node.id))
  const links = []
  const unresolvedNodes = new Map()

  for (const note of notes) {
    if (!nodeIds.has(note.id) && !(allowed.size && allowed.has(note.id))) continue
    if (!nodeIds.has(note.id)) continue
    for (const title of note.outgoingLinks || []) {
      const target = byTitle.get(title.trim().toLowerCase())
      if (target && nodeIds.has(target.id)) {
        links.push({ source: note.id, target: target.id })
      } else if (showUnresolved) {
        const key = title.trim().toLowerCase()
        if (!unresolvedNodes.has(key)) {
          unresolvedNodes.set(key, {
            id: `missing:${key}`,
            title,
            unresolved: true,
            tags: [],
            links: [],
          })
        }
        links.push({ source: note.id, target: `missing:${key}` })
      }
    }
  }

  let finalNodes = [...nodes, ...unresolvedNodes.values()]
  if (existingOnly) {
    finalNodes = finalNodes.filter((node) => !node.unresolved)
    const keep = new Set(finalNodes.map((node) => node.id))
    for (let i = links.length - 1; i >= 0; i -= 1) {
      const s = links[i].source
      const t = links[i].target
      if (!keep.has(s) || !keep.has(t)) links.splice(i, 1)
    }
  }
  if (!showOrphans) {
    const connected = new Set()
    for (const link of links) {
      connected.add(typeof link.source === 'string' ? link.source : link.source.id)
      connected.add(typeof link.target === 'string' ? link.target : link.target.id)
    }
    finalNodes = finalNodes.filter((node) => connected.has(node.id) || node.id === focusId)
  }

  const degree = new Map()
  for (const link of links) {
    const s = typeof link.source === 'string' ? link.source : link.source.id
    const t = typeof link.target === 'string' ? link.target : link.target.id
    degree.set(s, (degree.get(s) || 0) + 1)
    degree.set(t, (degree.get(t) || 0) + 1)
  }

  finalNodes = finalNodes.map((node) => ({
    ...node,
    val: 1 + (degree.get(node.id) || 0),
    color: colorFor(node, groups),
  }))

  return { nodes: finalNodes, links }
}

function colorFor(node, groups) {
  if (node.unresolved) return readCssColor('--text-muted', SUBJECT_COLORS.default)
  for (const group of groups) {
    const match = /tag:#?(.+)/i.exec(group.query || '')
    if (match && node.tags.includes(match[1])) return group.color
  }
  return SUBJECT_COLORS.default
}
