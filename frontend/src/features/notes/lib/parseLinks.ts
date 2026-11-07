// @ts-nocheck
const LINK_RE = /\[\[([^\]|#]+)(?:\|([^\]]+))?\]\]/g

export function extractWikiLinks(content) {
  if (typeof content !== 'string' || !content) return []
  const titles = []
  const seen = new Set()
  LINK_RE.lastIndex = 0
  let match
  while ((match = LINK_RE.exec(content))) {
    const title = match[1].trim()
    if (!title || seen.has(title)) continue
    seen.add(title)
    titles.push(title)
  }
  return titles
}

export function extractWikiLinkHits(content) {
  if (typeof content !== 'string' || !content) return []
  const hits = []
  LINK_RE.lastIndex = 0
  let match
  while ((match = LINK_RE.exec(content))) {
    hits.push({
      title: match[1].trim(),
      alias: match[2] ? match[2].trim() : '',
      start: match.index,
      end: match.index + match[0].length,
      raw: match[0],
    })
  }
  return hits
}

export function renameLinksInContent(content, oldTitle, newTitle) {
  if (!content || oldTitle === newTitle) return content
  const escaped = oldTitle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const re = new RegExp(`\\[\\[${escaped}(\\|[^\\]]*)?\\]\\]`, 'gi')
  return content.replace(re, (_, alias) => `[[${newTitle}${alias || ''}]]`)
}

export function lineSnippet(content, index, radius = 60) {
  if (!content) return ''
  const start = content.lastIndexOf('\n', index)
  const end = content.indexOf('\n', index)
  const lineStart = start === -1 ? 0 : start + 1
  const lineEnd = end === -1 ? content.length : end
  const line = content.slice(lineStart, lineEnd).trim()
  if (line.length <= radius * 2) return line
  const local = index - lineStart
  const from = Math.max(0, local - radius)
  const to = Math.min(line.length, local + radius)
  return `${from > 0 ? '…' : ''}${line.slice(from, to)}${to < line.length ? '…' : ''}`
}
