import { create } from 'zustand'

export const useNotesUiStore = create((set, get) => ({
  ready: false,
  userId: '',
  openTabs: [],
  activeNoteId: null,
  viewMode: 'edit',
  sortBy: 'modified',
  collapsedFolders: {},
  rightTab: 'backlinks',
  mobilePane: 'editor',
  searchOpen: false,
  commandOpen: false,
  switcherOpen: false,
  helpOpen: false,
  graphOpen: false,
  trashOpen: false,
  tagFilter: '',
  selectedFolderId: null,
  saveState: 'saved',
  lastSavedAt: 0,
  setReady: (ready) => set({ ready }),
  hydrateUi: (partial) => set({ ...partial, ready: true }),
  setUserId: (userId) => set({ userId }),
  openNote: (id) => {
    const tabs = get().openTabs.includes(id) ? get().openTabs : [...get().openTabs, id]
    set({ openTabs: tabs, activeNoteId: id, mobilePane: 'editor' })
  },
  closeTab: (id) => {
    const tabs = get().openTabs.filter((tab) => tab !== id)
    const active = get().activeNoteId === id ? tabs[tabs.length - 1] || null : get().activeNoteId
    set({ openTabs: tabs, activeNoteId: active })
  },
  reorderTabs: (tabs) => set({ openTabs: tabs }),
  setActiveNoteId: (id) => set({ activeNoteId: id, mobilePane: 'editor' }),
  setViewMode: (viewMode) => set({ viewMode }),
  cycleViewMode: () => {
    const order = ['edit', 'preview', 'split']
    const next = order[(order.indexOf(get().viewMode) + 1) % order.length]
    set({ viewMode: next })
  },
  setSortBy: (sortBy) => set({ sortBy }),
  toggleFolder: (id) => set({
    collapsedFolders: {
      ...get().collapsedFolders,
      [id]: !get().collapsedFolders[id],
    },
  }),
  setRightTab: (rightTab) => set({ rightTab }),
  setMobilePane: (mobilePane) => set({ mobilePane }),
  setSearchOpen: (searchOpen) => set({ searchOpen }),
  setCommandOpen: (commandOpen) => set({ commandOpen }),
  setSwitcherOpen: (switcherOpen) => set({ switcherOpen }),
  setHelpOpen: (helpOpen) => set({ helpOpen }),
  setGraphOpen: (graphOpen) => set({ graphOpen }),
  setTrashOpen: (trashOpen) => set({ trashOpen }),
  setTagFilter: (tagFilter) => set({ tagFilter }),
  setSelectedFolderId: (selectedFolderId) => set({ selectedFolderId }),
  setSaveState: (saveState, lastSavedAt = Date.now()) => set({ saveState, lastSavedAt }),
}))
