import { getSettings } from '../utils/storage'

export const SUBJECTS = [
  {
    id: 'math',
    name: 'ریاضی',
    color: '#1a3a6d',
    topics: [],
  },
  {
    id: 'physics',
    name: 'فیزیک',
    color: '#fdb827',
    topics: [],
  },
  {
    id: 'chemistry',
    name: 'شیمی',
    color: '#3d7ea6',
    topics: [],
  },
  {
    id: 'geometry',
    name: 'هندسه',
    color: '#c4782a',
    topics: [],
  },
  {
    id: 'literature',
    name: 'ادبیات',
    color: '#5b6b8c',
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
    color: subject.color || '#1a3a6d',
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
    color: custom.color || '#1a3a6d',
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
