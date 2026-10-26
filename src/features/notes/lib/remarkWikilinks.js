import { visit } from 'unist-util-visit'

const WIKI_RE = /\[\[([^\]|#]+)(?:\|([^\]]+))?\]\]/g

export function remarkWikilinks() {
  return (tree) => {
    visit(tree, 'text', (node, index, parent) => {
      if (!parent || typeof node.value !== 'string' || !node.value.includes('[[')) return
      const value = node.value
      const children = []
      let last = 0
      WIKI_RE.lastIndex = 0
      let match
      while ((match = WIKI_RE.exec(value))) {
        if (match.index > last) {
          children.push({ type: 'text', value: value.slice(last, match.index) })
        }
        const title = match[1].trim()
        const alias = match[2] ? match[2].trim() : title
        children.push({
          type: 'link',
          url: `wikilink:${encodeURIComponent(title)}`,
          data: { hProperties: { className: ['wiki-link'], 'data-wiki': title } },
          children: [{ type: 'text', value: alias }],
        })
        last = match.index + match[0].length
      }
      if (!children.length) return
      if (last < value.length) children.push({ type: 'text', value: value.slice(last) })
      parent.children.splice(index, 1, ...children)
      return index + children.length
    })
  }
}
