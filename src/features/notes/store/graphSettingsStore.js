import { create } from 'zustand'

const DEFAULT_GROUPS = [
  { id: 'math', query: 'tag:#ریاضی', color: '#1a3a6d', label: 'ریاضی' },
  { id: 'physics', query: 'tag:#فیزیک', color: '#fdb827', label: 'فیزیک' },
  { id: 'chemistry', query: 'tag:#شیمی', color: '#3d7ea6', label: 'شیمی' },
  { id: 'biology', query: 'tag:#زیست', color: '#2f9e44', label: 'زیست' },
  { id: 'lit', query: 'tag:#ادبیات', color: '#5b6b8c', label: 'ادبیات' },
  { id: 'arabic', query: 'tag:#عربی', color: '#ae3ec9', label: 'عربی' },
  { id: 'religion', query: 'tag:#دینی', color: '#e8590c', label: 'دینی' },
  { id: 'english', query: 'tag:#زبان', color: '#1098ad', label: 'زبان' },
]

export const useGraphSettingsStore = create((set, get) => ({
  search: '',
  showTags: true,
  showOrphans: true,
  showUnresolved: false,
  existingOnly: true,
  folderFilter: '',
  tagFilter: '',
  groups: DEFAULT_GROUPS,
  nodeSize: 4,
  linkThickness: 1.2,
  textFade: 0.45,
  arrows: false,
  centerForce: 0.05,
  repelForce: -120,
  linkForce: 0.4,
  linkDistance: 50,
  localDepth: 2,
  layoutNonce: 0,
  hydrate: (partial) => set({ ...get(), ...partial }),
  patch: (partial) => set(partial),
  reheat: () => set({ layoutNonce: get().layoutNonce + 1 }),
  addGroup: (group) => set({ groups: [...get().groups, group] }),
  removeGroup: (id) => set({ groups: get().groups.filter((group) => group.id !== id) }),
  updateGroup: (id, patch) => set({
    groups: get().groups.map((group) => (group.id === id ? { ...group, ...patch } : group)),
  }),
}))
