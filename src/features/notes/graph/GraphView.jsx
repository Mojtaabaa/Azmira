import { useEffect, useMemo, useRef, useState } from 'react'
import ForceGraph2D from 'react-force-graph-2d'
import { buildGraphData } from './buildGraphData'
import { useGraphSettingsStore } from '../store/graphSettingsStore'
import { useNotesStore } from '../store/notesStore'
import { useNotesUiStore } from '../store/uiStore'

export default function GraphView({ local = false, onClose }) {
  const notes = useNotesStore((state) => state.notes)
  const createNote = useNotesStore((state) => state.createNote)
  const activeNoteId = useNotesUiStore((state) => state.activeNoteId)
  const openNote = useNotesUiStore((state) => state.openNote)
  const userId = useNotesUiStore((state) => state.userId)
  const settings = useGraphSettingsStore()
  const graphRef = useRef()
  const hoverRef = useRef(null)
  const dataRef = useRef({ nodes: [], links: [] })
  const [, bump] = useState(0)

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
  }), [notes, settings, local, activeNoteId])

  useEffect(() => {
    dataRef.current = data
  }, [data])

  useEffect(() => {
    const graph = graphRef.current
    if (!graph) return
    graph.d3Force('charge')?.strength(settings.repelForce)
    graph.d3Force('link')?.distance(settings.linkDistance).strength(settings.linkForce)
    graph.d3Force('center')?.strength(settings.centerForce)
  }, [settings.repelForce, settings.linkDistance, settings.linkForce, settings.centerForce, data])

  useEffect(() => {
    const graph = graphRef.current
    if (!graph) return
    graph.d3ReheatSimulation()
  }, [settings.layoutNonce])

  return (
    <div className={local ? 'notes-graph is-local' : 'notes-graph is-global'}>
      {!local ? (
        <div className="notes-graph-top">
          <strong>گراف یادداشت‌ها</strong>
          <button type="button" onClick={onClose}>بستن</button>
        </div>
      ) : null}
      <ForceGraph2D
        ref={graphRef}
        graphData={data}
        nodeId="id"
        nodeLabel="title"
        nodeRelSize={settings.nodeSize}
        linkWidth={settings.linkThickness}
        linkDirectionalArrowLength={settings.arrows ? 4 : 0}
        cooldownTicks={80}
        onEngineStop={() => graphRef.current?.pauseAnimation()}
        onNodeClick={async (node) => {
          if (!node.unresolved) {
            openNote(node.id)
            return
          }
          const created = await createNote(userId, { title: node.title })
          openNote(created.id)
        }}
        onNodeDragEnd={(node) => {
          node.fx = node.x
          node.fy = node.y
        }}
        onNodeHover={(node) => {
          hoverRef.current = node
          bump((value) => value + 1)
        }}
        onNodeDblClick={(node) => {
          Reflect.deleteProperty(node, 'fx')
          Reflect.deleteProperty(node, 'fy')
        }}
        nodeCanvasObject={(node, ctx, globalScale) => {
          const label = node.title
          const size = (node.val || 1) * settings.nodeSize * 0.35
          const hover = hoverRef.current
          const dim = hover && hover.id !== node.id && !isNeighbor(dataRef.current, hover.id, node.id)
          if (hover?.id === node.id) {
            ctx.shadowColor = 'rgba(107, 163, 214, 0.55)'
            ctx.shadowBlur = 18
          } else {
            ctx.shadowBlur = 0
          }
          ctx.beginPath()
          ctx.arc(node.x, node.y, size, 0, Math.PI * 2)
          ctx.fillStyle = dim ? 'rgba(140,140,140,0.25)' : node.color
          ctx.fill()
          ctx.shadowBlur = 0
          if (node.id === activeNoteId) {
            ctx.strokeStyle = '#ffffff'
            ctx.lineWidth = 2 / globalScale
            ctx.stroke()
          }
          if (globalScale > settings.textFade || hover?.id === node.id) {
            ctx.font = `${12 / globalScale}px Vazirmatn, Tahoma, sans-serif`
            ctx.textAlign = 'center'
            ctx.textBaseline = 'top'
            ctx.fillStyle = dim ? 'rgba(200,200,200,0.35)' : '#f4f6fb'
            ctx.fillText(label, node.x, node.y + size + 2)
          }
        }}
      />
    </div>
  )
}

function isNeighbor(data, fromId, toId) {
  return (data.links || []).some((link) => {
    const s = typeof link.source === 'object' ? link.source.id : link.source
    const t = typeof link.target === 'object' ? link.target.id : link.target
    return (s === fromId && t === toId) || (t === fromId && s === toId)
  })
}
