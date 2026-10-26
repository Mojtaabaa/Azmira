const TAG_RE = /(^|[\s([{])#([\p{L}\p{N}_/-]+)/gu
const FRONTMATTER_RE = /^---\r?\n([\s\S]*?)\r?\n---/

export function extractFrontmatterTags(content) {
  if (typeof content !== 'string') return []
  const match = content.match(FRONTMATTER_RE)
  if (!match) return []
  const block = match[1]
  const tagsLine = block.split(/\r?\n/).find((line) => /^\s*tags\s*:/i.test(line))
  if (!tagsLine) return []
  const value = tagsLine.replace(/^\s*tags\s*:/i, '').trim()
  if (!value) return []
  if (value.startsWith('[')) {
    return value
      .slice(1, -1)
      .split(',')
      .map((part) => part.trim().replace(/^['"]|['"]$/g, ''))
      .filter(Boolean)
  }
  return value
    .split(/[,\s]+/)
    .map((part) => part.replace(/^#/, '').trim())
    .filter(Boolean)
}

export function extractInlineTags(content) {
  if (typeof content !== 'string' || !content) return []
  const withoutFm = content.replace(FRONTMATTER_RE, '')
  const tags = []
  const seen = new Set()
  TAG_RE.lastIndex = 0
  let match
  while ((match = TAG_RE.exec(withoutFm))) {
    const tag = match[2]
    if (!tag || seen.has(tag)) continue
    seen.add(tag)
    tags.push(tag)
  }
  return tags
}

export function extractTags(content) {
  const all = [...extractFrontmatterTags(content), ...extractInlineTags(content)]
  return [...new Set(all)]
}
