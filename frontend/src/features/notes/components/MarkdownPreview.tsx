// @ts-nocheck
import { useMemo, useState } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import remarkMath from 'remark-math'
import rehypeKatex from 'rehype-katex'
import { remarkWikilinks } from '../lib/remarkWikilinks'
import LinkPreviewCard from './LinkPreviewCard'
import NotesImage from './NotesImage'
import 'katex/dist/katex.min.css'

export default function MarkdownPreview({ content, notes, onOpenWiki, onToggleTask }) {
  const [hover, setHover] = useState(null)
  const byTitle = useMemo(() => {
    const map = new Map()
    for (const note of notes) map.set(note.title.trim().toLowerCase(), note)
    return map
  }, [notes])
  const titleSet = useMemo(() => new Set(byTitle.keys()), [byTitle])
  const body = useMemo(() => stripFrontmatter(content || ''), [content])

  return (
    <div className="notes-preview" dir="auto">
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkMath, remarkWikilinks]}
        rehypePlugins={[rehypeKatex]}
        urlTransform={(url) => url || ''}
        components={{
          a({ href, children, ...props }) {
            if (href?.startsWith('wikilink:')) {
              const title = decodeURIComponent(href.slice('wikilink:'.length))
              const exists = titleSet.has(title.trim().toLowerCase())
              const target = byTitle.get(title.trim().toLowerCase())
              return (
                <button
                  type="button"
                  className={exists ? 'wiki-link' : 'wiki-link is-unresolved'}
                  onClick={() => onOpenWiki?.(title)}
                  onMouseEnter={(event) => {
                    if (!target) {
                      setHover({
                        note: { title, content: 'یادداشت هنوز ساخته نشده.' },
                        x: event.clientX,
                        y: event.clientY,
                      })
                      return
                    }
                    setHover({ note: target, x: event.clientX, y: event.clientY })
                  }}
                  onMouseLeave={() => setHover(null)}
                >
                  {children}
                </button>
              )
            }
            return <a href={href} target="_blank" rel="noreferrer" {...props}>{children}</a>
          },
          input({ checked, ...props }) {
            if (props.type !== 'checkbox') return <input {...props} />
            return (
              <input
                {...props}
                type="checkbox"
                checked={Boolean(checked)}
                onChange={(event) => onToggleTask?.(event.target.checked, event)}
              />
            )
          },
          img({ src, alt }) {
            return <NotesImage src={src} alt={alt} />
          },
          blockquote({ children, ...props }) {
            const text = extractText(children)
            const callout = text.match(/^\[!(note|warning|tip)\]\s*([\s\S]*)$/i)
            if (!callout) return <blockquote {...props}>{children}</blockquote>
            const kind = callout[1].toLowerCase()
            const rest = callout[2].trim()
            return (
              <aside className={`notes-callout is-${kind}`}>
                {rest ? <p dir="auto">{rest}</p> : null}
              </aside>
            )
          },
          code({ className, children, ...props }) {
            const inline = !className
            if (inline) return <code className={className} {...props}>{children}</code>
            return (
              <pre className={className}>
                <code className={className} {...props}>{children}</code>
              </pre>
            )
          },
        }}
      >
        {body}
      </ReactMarkdown>
      {hover ? <LinkPreviewCard note={hover.note} x={hover.x} y={hover.y} /> : null}
    </div>
  )
}

function stripFrontmatter(raw) {
  if (!raw.startsWith('---')) return raw
  const match = /^---\r?\n[\s\S]*?\r?\n---\r?\n?/.exec(raw)
  return match ? raw.slice(match[0].length) : raw
}

function extractText(node) {
  if (typeof node === 'string') return node
  if (Array.isArray(node)) return node.map(extractText).join('')
  if (node?.props?.children) return extractText(node.props.children)
  return ''
}
