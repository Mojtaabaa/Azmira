import { create } from 'zustand'
import { SUBJECT_COLORS } from '../../../data/subjects'

const DEFAULT_GROUPS = [
  { id: 'math', query: 'tag:#ریاضی', color: SUBJECT_COLORS.math, label: 'ریاضی' },
  { id: 'physics', query: 'tag:#فیزیک', color: SUBJECT_COLORS.physics, label: 'فیزیک' },
  { id: 'chemistry', query: 'tag:#شیمی', color: SUBJECT_COLORS.chemistry, label: 'شیمی' },
  { id: 'biology', query: 'tag:#زیست', color: SUBJECT_COLORS.chemistry, label: 'زیست' },
  { id: 'lit', query: 'tag:#ادبیات', color: SUBJECT_COLORS.literature, label: 'ادبیات' },
  { id: 'arabic', query: 'tag:#عربی', color: SUBJECT_COLORS.literature, label: 'عربی' },
  { id: 'religion', query: 'tag:#دینی', color: SUBJECT_COLORS.geometry, label: 'دینی' },
  { id: 'english', query: 'tag:#زبان', color: SUBJECT_COLORS.physics, label: 'زبان' },
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
