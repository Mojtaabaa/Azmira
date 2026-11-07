import { useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { FileText, FolderTree, Network, PanelRight } from 'lucide-react'
import { useAuth } from '../../hooks/useAuth'
import CommandPalette from './components/CommandPalette'
import EditorPane from './components/EditorPane'
import Explorer from './components/Explorer'
import HelpModal from './components/HelpModal'
import NoteTabs from './components/NoteTabs'
import NotesToolbar from './components/NotesToolbar'
import QuickSwitcher from './components/QuickSwitcher'
import RightPanel from './components/RightPanel'
import SearchPanel from './components/SearchPanel'
import StatusBar from './components/StatusBar'
import TrashPanel from './components/TrashPanel'
import GraphControls from './graph/GraphControls'
import GraphView from './graph/GraphView'
import { collectImageFiles } from './lib/insertImage'
import { importMarkdownFiles } from './lib/importVault'
import { useNotesStore } from './store/notesStore'
import { useGraphSettingsStore } from './store/graphSettingsStore'
import { useNotesUiStore } from './store/uiStore'
import './styles/notes.css'

function serializeGraphSettings(state) {
  return {
    search: state.search,
    showTags: state.showTags,
    showOrphans: state.showOrphans,
    showUnresolved: state.showUnresolved,
    existingOnly: state.existingOnly,
    folderFilter: state.folderFilter,
    tagFilter: state.tagFilter,
    groups: state.groups,
    nodeSize: state.nodeSize,
    linkThickness: state.linkThickness,
    textFade: state.textFade,
    arrows: state.arrows,
    centerForce: state.centerForce,
    repelForce: state.repelForce,
    linkForce: state.linkForce,
    linkDistance: state.linkDistance,
    localDepth: state.localDepth,
  }
}

export default function NotesPage() {
  const { user } = useAuth()
  const userId = user?.id || 'anon'
  const hydrate = useNotesStore((state) => state.hydrate)
  const loaded = useNotesStore((state) => state.loaded)
  const getNoteById = useNotesStore((state) => state.getNoteById)
  const createNote = useNotesStore((state) => state.createNote)
  const importVaultData = useNotesStore((state) => state.importVaultData)
  const persistUiSettings = useNotesStore((state) => state.persistUiSettings)
  const hydrateUi = useNotesUiStore((state) => state.hydrateUi)
  const setReady = useNotesUiStore((state) => state.setReady)
  const ready = useNotesUiStore((state) => state.ready)
  const setUserId = useNotesUiStore((state) => state.setUserId)
  const activeNoteId = useNotesUiStore((state) => state.activeNoteId)
  const openTabs = useNotesUiStore((state) => state.openTabs)
  const viewMode = useNotesUiStore((state) => state.viewMode)
  const sortBy = useNotesUiStore((state) => state.sortBy)
  const collapsedFolders = useNotesUiStore((state) => state.collapsedFolders)
  const rightTab = useNotesUiStore((state) => state.rightTab)
  const mobilePane = useNotesUiStore((state) => state.mobilePane)
  const setMobilePane = useNotesUiStore((state) => state.setMobilePane)
  const graphOpen = useNotesUiStore((state) => state.graphOpen)
  const searchOpen = useNotesUiStore((state) => state.searchOpen)
  const commandOpen = useNotesUiStore((state) => state.commandOpen)
  const switcherOpen = useNotesUiStore((state) => state.switcherOpen)
  const helpOpen = useNotesUiStore((state) => state.helpOpen)
  const trashOpen = useNotesUiStore((state) => state.trashOpen)
  const setGraphOpen = useNotesUiStore((state) => state.setGraphOpen)
  const cycleViewMode = useNotesUiStore((state) => state.cycleViewMode)
  const setSearchOpen = useNotesUiStore((state) => state.setSearchOpen)
  const setCommandOpen = useNotesUiStore((state) => state.setCommandOpen)
  const setSwitcherOpen = useNotesUiStore((state) => state.setSwitcherOpen)
  const setHelpOpen = useNotesUiStore((state) => state.setHelpOpen)
  const openNote = useNotesUiStore((state) => state.openNote)
  const hydrateGraph = useGraphSettingsStore((state) => state.hydrate)
  const dropArmed = useRef(false)
  const deepLinkDone = useRef('')
  const [params, setParams] = useSearchParams()
  const searchKey = params.toString()

  const note = useMemo(() => getNoteById(activeNoteId), [getNoteById, activeNoteId])

  useEffect(() => {
    let alive = true
    const ui = useNotesUiStore.getState()
    const keepUi = ui.ready && ui.userId === userId
    if (!keepUi) setReady(false)
    ;(async () => {
      setUserId(userId)
      try {
        const settings = await hydrate(userId)
        if (!alive) return
        if (settings.graph) hydrateGraph(settings.graph)
        if (keepUi) {
          setReady(true)
          return
        }
        const known = new Set(useNotesStore.getState().notes.map((item) => item.id))
        const tabs = (Array.isArray(settings.openTabs) ? settings.openTabs : []).filter((id) => known.has(id))
        const active = known.has(settings.activeNoteId) ? settings.activeNoteId : (tabs[tabs.length - 1] || null)
        hydrateUi({
          openTabs: tabs,
          activeNoteId: active,
          viewMode: settings.viewMode || 'edit',
          sortBy: settings.sortBy || 'modified',
          collapsedFolders: settings.collapsedFolders || {},
          rightTab: settings.rightTab || 'backlinks',
        })
      } catch (error) {
        console.error(error)
        if (alive) setReady(true)
      }
    })()
    return () => {
      alive = false
    }
  }, [userId, hydrate, hydrateUi, setUserId, hydrateGraph, setReady])

  useEffect(() => {
    if (!loaded) return
    const timer = setTimeout(() => {
      persistUiSettings(userId, {
        openTabs,
        activeNoteId,
        viewMode,
        sortBy,
        collapsedFolders,
        rightTab,
        graph: serializeGraphSettings(useGraphSettingsStore.getState()),
      })
    }, 500)
    return () => clearTimeout(timer)
  }, [
    loaded,
    userId,
    openTabs,
    activeNoteId,
    viewMode,
    sortBy,
    collapsedFolders,
    rightTab,
    persistUiSettings,
  ])

  useEffect(() => {
    if (!loaded || !ready) return
    const id = params.get('n')
    const daily = params.get('daily')
    if (!id && !daily) return
    const key = searchKey
    if (deepLinkDone.current === key) return
    deepLinkDone.current = key
    if (id) openNote(id)
    if (daily) {
      const title = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
      }).format(new Date())
      const existing = useNotesStore.getState().getNoteByTitle(title)
      if (existing) openNote(existing.id)
      else {
        useNotesStore.getState().createNote(userId, { title, content: `# ${title}\n\n` }).then((created) => {
          openNote(created.id)
        })
      }
    }
    const next = new URLSearchParams(searchKey)
    next.delete('n')
    next.delete('daily')
    setParams(next, { replace: true })
  }, [loaded, ready, searchKey, params, setParams, openNote, userId])

  async function openDaily() {
    const title = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(new Date())
    const existing = useNotesStore.getState().getNoteByTitle(title)
    if (existing) {
      openNote(existing.id)
      return
    }
    const created = await createNote(userId, { title, content: `# ${title}\n\n` })
    openNote(created.id)
  }

  useEffect(() => {
    function onKey(event) {
      const meta = event.metaKey || event.ctrlKey
      const key = event.key.toLowerCase()
      if (meta && key === 'e') {
        event.preventDefault()
        cycleViewMode()
      }
      if (meta && event.shiftKey && key === 'f') {
        event.preventDefault()
        setSearchOpen(true)
      }
      if (meta && key === 'p') {
        event.preventDefault()
        setCommandOpen(true)
      }
      if (meta && key === 'o') {
        event.preventDefault()
        setSwitcherOpen(true)
      }
      if (meta && key === 'd') {
        event.preventDefault()
        const title = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
          year: 'numeric',
          month: '2-digit',
          day: '2-digit',
        }).format(new Date())
        const existing = useNotesStore.getState().getNoteByTitle(title)
        if (existing) useNotesUiStore.getState().openNote(existing.id)
        else {
          useNotesStore.getState().createNote(userId, { title, content: `# ${title}\n\n` }).then((created) => {
            useNotesUiStore.getState().openNote(created.id)
          })
        }
      }
      if (event.key === '?' && !meta) {
        const tag = event.target?.tagName
        if (tag === 'INPUT' || tag === 'TEXTAREA' || event.target?.closest?.('.cm-editor')) return
        setHelpOpen(true)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [cycleViewMode, setSearchOpen, setCommandOpen, setSwitcherOpen, setHelpOpen, userId])

  if (!loaded || !ready) {
    return (
      <div className="notes-shell notes-loading" role="status">
        در حال بارگذاری یادداشت‌ها…
      </div>
    )
  }

  return (
    <div
      className={`notes-shell is-${mobilePane}`}
      onDragOver={(event) => {
        event.preventDefault()
        dropArmed.current = true
      }}
      onDrop={async (event) => {
        event.preventDefault()
        if (!dropArmed.current) return
        dropArmed.current = false
        if (event.target.closest('.notes-source')) return
        const files = [...(event.dataTransfer?.files || [])]
        if (collectImageFiles(event.dataTransfer).length && !files.some((file) => /\.(md|zip)$/i.test(file.name))) {
          return
        }
        if (!files.length) return
        const payload = await importMarkdownFiles(files)
        await importVaultData(userId, payload)
      }}
    >
      <NotesToolbar onDaily={openDaily} />
      <NoteTabs />
      <div className="notes-panes">
        <div className="notes-pane is-explorer">
          <Explorer userId={userId} />
        </div>
        <div className="notes-pane is-editor">
          <EditorPane userId={userId} note={note} />
        </div>
        <div className="notes-pane is-right">
          <RightPanel note={note} />
          <div className="notes-local-graph">
            <GraphControls local onAnimate={() => useGraphSettingsStore.getState().reheat()} />
            <GraphView local />
          </div>
        </div>
      </div>
      <StatusBar note={note} />
      <nav className="notes-mobile-tabs" aria-label="بخش‌های یادداشت">
        <button type="button" className={mobilePane === 'explorer' ? 'is-active' : ''} onClick={() => setMobilePane('explorer')}>
          <FolderTree size={16} />
          فهرست
        </button>
        <button type="button" className={mobilePane === 'editor' ? 'is-active' : ''} onClick={() => setMobilePane('editor')}>
          <FileText size={16} />
          یادداشت
        </button>
        <button type="button" className={mobilePane === 'right' ? 'is-active' : ''} onClick={() => setMobilePane('right')}>
          <PanelRight size={16} />
          جزئیات
        </button>
        <button type="button" onClick={() => setGraphOpen(true)}>
          <Network size={16} />
          گراف
        </button>
      </nav>
      {searchOpen ? <SearchPanel /> : null}
      {commandOpen ? <CommandPalette /> : null}
      {switcherOpen ? <QuickSwitcher /> : null}
      {helpOpen ? <HelpModal /> : null}
      {trashOpen ? <TrashPanel /> : null}
      {graphOpen ? (
        <GraphOverlay onClose={() => setGraphOpen(false)} />
      ) : null}
    </div>
  )
}

function GraphOverlay({ onClose }) {
  const [filtersOpen, setFiltersOpen] = useState(false)
  return (
    <div className={`notes-graph-overlay${filtersOpen ? ' is-filters' : ''}`}>
      <GraphControls onAnimate={() => useGraphSettingsStore.getState().reheat()} />
      <GraphView
        onClose={onClose}
        onToggleFilters={() => setFiltersOpen((open) => !open)}
        filtersOpen={filtersOpen}
      />
    </div>
  )
}
