import { Decoration, EditorView, MatchDecorator, ViewPlugin, WidgetType } from '@codemirror/view'
import { getImage } from './db'
import { peekImage, rememberImage } from './imageCache'
import { parseNotesImageId } from './insertImage'

class ImagePreviewWidget extends WidgetType {
  constructor(src, userId) {
    super()
    this.src = src
    this.userId = userId
  }

  eq(other) {
    return other instanceof ImagePreviewWidget
      && other.src === this.src
      && other.userId === this.userId
  }

  toDOM(view) {
    const wrap = document.createElement('span')
    wrap.className = 'notes-cm-image-wrap is-inline'
    wrap.contentEditable = 'false'

    const img = document.createElement('img')
    img.className = 'notes-cm-image'
    img.alt = 'تصویر یادداشت'
    img.draggable = false
    wrap.appendChild(img)

    const measure = () => {
      try {
        view.requestMeasure()
      } catch {
        /* ignore */
      }
    }
    img.addEventListener('load', measure)
    img.addEventListener('error', measure)

    const storedId = parseNotesImageId(this.src)
    const cached = storedId ? peekImage(storedId) : ''
    if (cached) {
      img.src = cached
      queueMicrotask(measure)
      return wrap
    }
    if (storedId) {
      getImage(this.userId, storedId).then((row) => {
        if (!wrap.isConnected) return
        if (row?.dataUrl) {
          rememberImage(storedId, row.dataUrl)
          img.src = row.dataUrl
          measure()
          return
        }
        wrap.replaceChildren(fallbackNode())
        measure()
      }).catch(() => {
        if (!wrap.isConnected) return
        wrap.replaceChildren(fallbackNode())
        measure()
      })
      return wrap
    }
    if (/^https?:\/\//i.test(this.src)) {
      img.src = this.src
      return wrap
    }
    wrap.replaceChildren(fallbackNode())
    return wrap
  }

  ignoreEvent() {
    return true
  }
}

function fallbackNode() {
  const span = document.createElement('span')
  span.className = 'notes-muted'
  span.textContent = '[تصویر]'
  return span
}

export function imageWidgets(userId) {
  const decorator = new MatchDecorator({
    regexp: /!\[[^\]]*\]\((notes-image\/[A-Za-z0-9_-]+|notes-image:[A-Za-z0-9_-]+|https?:\/\/[^\s)]+)\)/g,
    decoration: (match) => {
      const src = /\(([^)]+)\)/.exec(match[0])?.[1] || ''
      if (!src) return null
      return Decoration.replace({
        widget: new ImagePreviewWidget(src, userId),
      })
    },
  })

  return ViewPlugin.fromClass(
    class {
      constructor(view) {
        this.decorations = decorator.createDeco(view)
      }

      update(update) {
        if (update.docChanged || update.viewportChanged) {
          this.decorations = decorator.updateDeco(update, this.decorations)
        }
      }
    },
    {
      decorations: (plugin) => plugin.decorations,
      provide: (plugin) => EditorView.atomicRanges.of((view) => (
        view.plugin(plugin)?.decorations || Decoration.none
      )),
    },
  )
}
