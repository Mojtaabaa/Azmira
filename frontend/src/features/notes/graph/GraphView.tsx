// @ts-nocheck
import { useEffect, useMemo, useRef, useState } from 'react'
import ForceGraph2D from 'react-force-graph-2d'
import { SUBJECT_COLORS } from '../../../data/subjects'
import { useTheme } from '../../../hooks/useTheme'
import GraphInspector from './GraphInspector'
import { buildGraphData } from './buildGraphData'
import { useGraphSettingsStore } from '../store/graphSettingsStore'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

const CLICK_PX = 6

export default function GraphView({ local = false, onClose, onToggleFilters, filtersOpen = false }) {
  const notes = useNotesStore((state) => state.notes)
  const createNote = useNotesStore((state) => state.createNote)
  const updateNote = useNotesStore((state) => state.updateNote)
  const activeNoteId = useNotesUiStore((state) => state.activeNoteId)
  const openNote = useNotesUiStore((state) => state.openNote)
  const userId = useNotesUiStore((state) => state.userId)
  const setGraphOpen = useNotesUiStore((state) => state.setGraphOpen)
  const settings = useGraphSettingsStore()
  const { theme } = useTheme()
  const wrapRef = useRef(null)
  const graphRef = useRef()
  const hoverRef = useRef(null)
  const selectedRef = useRef(null)
  const dataRef = useRef({ nodes: [], links: [] })
  const shiftRef = useRef(false)
  const openLockRef = useRef(false)
  const pointerRef = useRef(null)
  const activateRef = useRef(null)
  const [selected, setSelected] = useState(null)
  const [size, setSize] = useState({ width: 0, height: 0 })

  const data = useMemo(() => buildGraphData(notes, {
    search: settings.search,
    showOrphans: settings.showOrphans,
    showUnresolved: settings.showUnresolved,
    existingOnly: settings.existingOnly,
    folderFilter: settings.folderFilter,
    tagFilter: settings.tagFilter,
    groups: settings.groups,
    focusId: local ? activeNoteId : null,
    depth: local ? settings.localDepth : null,
  }), [notes, settings, local, activeNoteId, theme])

  useEffect(() => {
    dataRef.current = data
  }, [data])

  useEffect(() => {
    selectedRef.current = selected
    graphRef.current?.refresh?.()
  }, [selected, activeNoteId])

  useEffect(() => {
    const el = wrapRef.current
    if (!el || typeof ResizeObserver === 'undefined') return undefined
    const sync = () => {
      const next = { width: Math.floor(el.clientWidth), height: Math.floor(el.clientHeight) }
      setSize((prev) => (prev.width === next.width && prev.height === next.height ? prev : next))
    }
    sync()
    const observer = new ResizeObserver(sync)
    observer.observe(el)
    return () => observer.disconnect()
  }, [local])

  useEffect(() => {
    const graph = graphRef.current
    if (!graph || size.width < 1) return
    graph.d3Force('charge')?.strength(settings.repelForce)
    graph.d3Force('link')?.distance(settings.linkDistance).strength(settings.linkForce)
    graph.d3Force('center')?.strength(settings.centerForce)
  }, [settings.repelForce, settings.linkDistance, settings.linkForce, settings.centerForce, size.width, size.height])

  useEffect(() => {
    if (size.width < 1) return
    graphRef.current?.d3ReheatSimulation?.()
  }, [settings.layoutNonce, data, size.width, size.height])

  useEffect(() => {
    function onKey(event) {
      shiftRef.current = event.shiftKey
    }
    window.addEventListener('keydown', onKey)
    window.addEventListener('keyup', onKey)
    return () => {
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('keyup', onKey)
    }
  }, [])

  useEffect(() => {
    const el = wrapRef.current
    if (!el) return undefined

    function canvasEl() {
      return el.querySelector('canvas')
    }

    function localPoint(event) {
      const canvas = canvasEl() || el
      const rect = canvas.getBoundingClientRect()
      return { x: event.clientX - rect.left, y: event.clientY - rect.top }
    }

    function nearestNode(screenX, screenY) {
      const graph = graphRef.current
      if (!graph || typeof graph.screen2GraphCoords !== 'function') return null
      const point = graph.screen2GraphCoords(screenX, screenY)
      let zoom = 1
      try {
        zoom = typeof graph.zoom === 'function' ? graph.zoom() : 1
      } catch {
        zoom = 1
      }
      let best = null
      let bestDist = Infinity
      for (const node of dataRef.current.nodes || []) {
        if (!Number.isFinite(node.x) || !Number.isFinite(node.y)) continue
        const radius = (Math.max(12, (node.val || 1) * settings.nodeSize * 0.5) + 14) / Math.max(zoom, 0.2)
        const dist = Math.hypot(node.x - point.x, node.y - point.y)
        if (dist <= radius && dist < bestDist) {
          best = node
          bestDist = dist
        }
      }
      return best
    }

    function onPointerDown(event) {
      if (event.button !== 0) return
      const point = localPoint(event)
      pointerRef.current = {
        x: point.x,
        y: point.y,
        node: nearestNode(point.x, point.y),
        moved: false,
      }
    }

    function onPointerMove(event) {
      const start = pointerRef.current
      const point = localPoint(event)
      if (start && !start.moved) {
        if (Math.hypot(point.x - start.x, point.y - start.y) > CLICK_PX) start.moved = true
      }
      const hovered = nearestNode(point.x, point.y)
      const prev = hoverRef.current?.id || null
      const next = hovered?.id || null
      hoverRef.current = hovered
      if (prev !== next) graphRef.current?.refresh?.()
      el.style.cursor = hovered ? 'pointer' : ''
    }

    function onPointerUp(event) {
      const start = pointerRef.current
      pointerRef.current = null
      if (!start || start.moved || event.button !== 0) return
      const point = localPoint(event)
      if (Math.hypot(point.x - start.x, point.y - start.y) > CLICK_PX) return
      const node = nearestNode(point.x, point.y) || start.node
      if (!node) return
      activateRef.current?.(node, event)
    }

    function onPointerLeave() {
      pointerRef.current = null
      if (hoverRef.current) {
        hoverRef.current = null
        graphRef.current?.refresh?.()
      }
      el.style.cursor = ''
    }

    el.addEventListener('pointerdown', onPointerDown)
    el.addEventListener('pointermove', onPointerMove)
    el.addEventListener('pointerup', onPointerUp)
    el.addEventListener('pointerleave', onPointerLeave)
    el.addEventListener('pointercancel', onPointerLeave)
    return () => {
      el.removeEventListener('pointerdown', onPointerDown)
      el.removeEventListener('pointermove', onPointerMove)
      el.removeEventListener('pointerup', onPointerUp)
      el.removeEventListener('pointerleave', onPointerLeave)
      el.removeEventListener('pointercancel', onPointerLeave)
    }
  }, [settings.nodeSize])

  async function openNode(node) {
    if (!node || openLockRef.current) return
    openLockRef.current = true
    try {
      if (node.unresolved) {
        const created = await createNote(userId, { title: node.title })
        openNote(created.id)
      } else {
        openNote(node.id)
      }
      if (!local) {
        setGraphOpen(false)
        onClose?.()
      }
    } finally {
      window.setTimeout(() => {
        openLockRef.current = false
      }, 250)
    }
  }

  async function linkFromActive(node) {
    const active = notes.find((item) => item.id === activeNoteId)
    if (!active || !node?.title || active.id === node.id) return
    const already = (active.outgoingLinks || []).some((title) => title.trim().toLowerCase() === node.title.trim().toLowerCase())
    if (already) return
    const next = `${active.content || ''}${active.content?.endsWith('\n') ? '' : '\n'}[[${node.title}]]\n`
    await updateNote(userId, active.id, { content: next })
  }

  function handleNodeActivate(node, event) {
    if (!node) return
    setSelected(node)
    if (event?.shiftKey || shiftRef.current) {
      void linkFromActive(node)
      return
    }
    void openNode(node)
  }

  activateRef.current = handleNodeActivate

  function readColors() {
    const styles = getComputedStyle(document.documentElement)
    return {
      accent: styles.getPropertyValue('--accent').trim() || SUBJECT_COLORS.default,
      text: styles.getPropertyValue('--text').trim() || '#f0efec',
      muted: styles.getPropertyValue('--text-muted').trim() || '#9a9890',
      border: styles.getPropertyValue('--border').trim() || '#2c2c29',
    }
  }

  const ready = size.width > 0 && size.height > 0
  const canvas = ready ? (
    <ForceGraph2D
      ref={graphRef}
      width={size.width}
      height={size.height}
      graphData={data}
      nodeId="id"
      nodeRelSize={settings.nodeSize}
      linkWidth={(link) => {
        const hover = hoverRef.current
        if (!hover) return settings.linkThickness
        return linkTouches(link, hover.id) ? settings.linkThickness * 1.7 : Math.max(0.4, settings.linkThickness * 0.5)
      }}
      linkDirectionalArrowLength={settings.arrows ? 4 : 0}
      backgroundColor="transparent"
      linkColor={(link) => {
        const colors = readColors()
        const hover = hoverRef.current
        if (!hover) return colors.border
        return linkTouches(link, hover.id) ? colors.accent : colors.border
      }}
      cooldownTicks={80}
      warmupTicks={12}
      autoPauseRedraw={false}
      enableNodeDrag
      enableZoomInteraction
      enablePanInteraction
      enablePointerInteraction
      onNodeDrag={(node) => {
        node.fx = node.x
        node.fy = node.y
        if (pointerRef.current) pointerRef.current.moved = true
      }}
      onNodeDragEnd={(node) => {
        node.fx = node.x
        node.fy = node.y
      }}
      onNodeDblClick={(node) => {
        Reflect.deleteProperty(node, 'fx')
        Reflect.deleteProperty(node, 'fy')
        graphRef.current?.d3ReheatSimulation?.()
      }}
      nodePointerAreaPaint={(node, color, ctx) => {
        const sizePx = Math.max(10, (node.val || 1) * settings.nodeSize * 0.45) + 10
        ctx.beginPath()
        ctx.arc(node.x, node.y, sizePx, 0, Math.PI * 2)
        ctx.fillStyle = color
        ctx.fill()
      }}
      nodeCanvasObject={(node, ctx, globalScale) => {
        const colors = readColors()
        const hover = hoverRef.current
        const picked = selectedRef.current
        const dim = hover && hover.id !== node.id && !isNeighbor(dataRef.current, hover.id, node.id)
        const isPicked = picked?.id === node.id
        const active = node.id === activeNoteId
        const hovered = hover?.id === node.id
        const scale = hovered ? 1.35 : isPicked || active ? 1.12 : 1
        const radius = Math.max(3.5, (node.val || 1) * settings.nodeSize * 0.35) * scale

        if (hovered || isPicked || active) {
          ctx.beginPath()
          ctx.arc(node.x, node.y, radius + 4 / globalScale, 0, Math.PI * 2)
          ctx.strokeStyle = colors.accent
          ctx.globalAlpha = hovered ? 0.5 : 0.3
          ctx.lineWidth = (hovered ? 2.2 : 1.5) / globalScale
          ctx.stroke()
          ctx.globalAlpha = 1
          ctx.shadowColor = colors.accent
          ctx.shadowBlur = hovered ? 18 : 10
        } else {
          ctx.shadowBlur = 0
        }

        ctx.beginPath()
        ctx.arc(node.x, node.y, radius, 0, Math.PI * 2)
        ctx.globalAlpha = dim ? 0.2 : 1
        ctx.fillStyle = node.color || colors.accent
        ctx.fill()
        ctx.shadowBlur = 0

        if (hovered || isPicked || active) {
          ctx.strokeStyle = colors.text
          ctx.globalAlpha = 0.9
          ctx.lineWidth = (hovered ? 2 : 1.5) / globalScale
          ctx.stroke()
        }
        ctx.globalAlpha = 1

        const showLabel = hovered
          || isPicked
          || active
          || globalScale > settings.textFade
          || (hover && isNeighbor(dataRef.current, hover.id, node.id))
        if (!showLabel) return

        ctx.font = `${hovered || isPicked ? 600 : 500} ${Math.max(11, 12 / Math.max(globalScale * 0.9, 0.7))}px Estedad, Vazirmatn, Tahoma, sans-serif`
        ctx.textAlign = 'center'
        ctx.textBaseline = 'top'
        ctx.globalAlpha = dim ? 0.3 : 1
        ctx.fillStyle = hovered || isPicked || active ? colors.text : colors.muted
        ctx.fillText(node.title, node.x, node.y + radius + 4)
        ctx.globalAlpha = 1
      }}
    />
  ) : null

  if (local) {
    return (
      <div className="notes-graph is-local" ref={wrapRef}>
        {data.nodes.length === 0 ? (
          <p className="notes-muted notes-graph-empty">پیوندی در این عمق نیست. با [[عنوان]] وصل کن.</p>
        ) : canvas}
      </div>
    )
  }

  return (
    <div className="notes-graph is-global">
      <div className="notes-graph-top">
        <strong>گراف یادداشت‌ها</strong>
        <span className="notes-graph-stats">{data.nodes.length} گره · {data.links.length} پیوند</span>
        {onToggleFilters ? (
          <button type="button" className="button notes-graph-filters-btn" aria-pressed={filtersOpen} onClick={onToggleFilters}>
            {filtersOpen ? 'بستن فیلتر' : 'فیلتر'}
          </button>
        ) : null}
        <button type="button" className="button" onClick={() => graphRef.current?.zoomToFit(400, 48)}>جا شدن</button>
        <button type="button" className="button" onClick={onClose}>بستن</button>
      </div>
      <div className="notes-graph-body">
        <div className="notes-graph-canvas" ref={wrapRef}>
          {data.nodes.length === 0 ? (
            <div className="notes-empty">
              <p>با دکمه ⟦⟧ یا نوشتن [[عنوان]] یادداشت‌ها را به هم وصل کن تا اینجا یال ساخته شود.</p>
            </div>
          ) : canvas}
        </div>
        <GraphInspector node={selected} onCloseGraph={onClose} />
      </div>
    </div>
  )
}

function linkTouches(link, nodeId) {
  const s = typeof link.source === 'object' ? link.source.id : link.source
  const t = typeof link.target === 'object' ? link.target.id : link.target
  return s === nodeId || t === nodeId
}

function isNeighbor(data, fromId, toId) {
  return (data.links || []).some((link) => linkTouches(link, fromId) && linkTouches(link, toId) && fromId !== toId)
}
