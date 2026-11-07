import { getSettings } from '../utils/storage'

export const SUBJECT_COLORS = {
  math: '#4f6fb8',
  physics: '#2f8f9a',
  chemistry: '#3d8f66',
  geometry: '#c45c26',
  literature: '#b05a7a',
  default: '#c45c26',
}

export const SUBJECTS = [
  {
    id: 'math',
    name: 'ریاضی',
    color: SUBJECT_COLORS.math,
    topics: [],
  },
  {
    id: 'physics',
    name: 'فیزیک',
    color: SUBJECT_COLORS.physics,
    topics: [],
  },
  {
    id: 'chemistry',
    name: 'شیمی',
    color: SUBJECT_COLORS.chemistry,
    topics: [],
  },
  {
    id: 'geometry',
    name: 'هندسه',
    color: SUBJECT_COLORS.geometry,
    topics: [],
  },
  {
    id: 'literature',
    name: 'ادبیات',
    color: SUBJECT_COLORS.literature,
    topics: [],
  },
]

function hiddenSet() {
  const saved = getSettings().hiddenSubjects
  return new Set(Array.isArray(saved) ? saved : [])
}

function customList() {
  const saved = getSettings().customSubjects
  return Array.isArray(saved) ? saved : []
}

export function listSubjects({ includeHidden = false } = {}) {
  const hidden = hiddenSet()
  const builtIn = SUBJECTS
    .filter((subject) => includeHidden || !hidden.has(subject.id))
    .map((subject) => ({
      ...subject,
      custom: false,
      hidden: hidden.has(subject.id),
    }))
  const custom = customList().map((subject) => ({
    id: subject.id,
    name: subject.name,
    color: subject.color || SUBJECT_COLORS.default,
    topics: [],
    custom: true,
    hidden: false,
  }))
  return [...builtIn, ...custom]
}

export function getSubjectById(subjectId) {
  if (typeof subjectId !== 'string' || !subjectId) return null
  const builtIn = SUBJECTS.find((subject) => subject.id === subjectId)
  if (builtIn) {
    return {
      ...builtIn,
      custom: false,
      hidden: hiddenSet().has(builtIn.id),
    }
  }
  const custom = customList().find((subject) => subject.id === subjectId)
  if (!custom) return null
  return {
    id: custom.id,
    name: custom.name,
    color: custom.color || SUBJECT_COLORS.default,
    topics: [],
    custom: true,
    hidden: false,
  }
}

export function listHiddenBuiltins() {
  const hidden = hiddenSet()
  return SUBJECTS
    .filter((subject) => hidden.has(subject.id))
    .map((subject) => ({ ...subject, custom: false, hidden: true }))
}
