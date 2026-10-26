import { listSubjects } from '../data/subjects'
import { createId } from './id'
import { getSettings, updateSettings } from './storage'

export const MAX_SUBJECT_NAME = 40

export function cleanSubjectName(value) {
  if (typeof value !== 'string') return ''
  return value.trim().replace(/\s+/g, ' ')
}

export function resolveSubjectName(raw, { optional = false } = {}) {
  const name = cleanSubjectName(raw)
  if (!name) {
    if (optional) return { id: '', name: '' }
    return { error: 'درس را بنویس.' }
  }
  if (name.length > MAX_SUBJECT_NAME) {
    return { error: `نام درس نمی‌تواند بیشتر از ${MAX_SUBJECT_NAME} نویسه باشد.` }
  }

  const existing = listSubjects({ includeHidden: true }).find((subject) => subject.name === name)
  if (existing) {
    if (existing.hidden) restoreBuiltinSubject(existing.id)
    return { id: existing.id, name: existing.name }
  }

  const subject = { id: `custom-${createId()}`, name }
  try {
    updateSettings({
      customSubjects: [...(getSettings().customSubjects || []), subject],
    })
  } catch (saveError) {
    return { error: saveError.message || 'این درس ذخیره نشد.' }
  }
  return { id: subject.id, name }
}

export function subjectFromForm(form, options) {
  const field = form?.elements?.namedItem('subject-name')
  const raw = field && 'value' in field ? field.value : ''
  return resolveSubjectName(raw, options)
}

export function renameCustomSubject(id, raw) {
  const name = cleanSubjectName(raw)
  if (!name) return { error: 'نام درس را بنویس.' }
  if (name.length > MAX_SUBJECT_NAME) {
    return { error: `نام درس نمی‌تواند بیشتر از ${MAX_SUBJECT_NAME} نویسه باشد.` }
  }

  const current = listSubjects().find((subject) => subject.id === id && subject.custom)
  if (!current) return { error: 'این درس را نمی‌شود عوض کرد.' }
  if (current.name === name) return { subject: current }
  if (listSubjects({ includeHidden: true }).some((subject) => subject.name === name && subject.id !== id)) {
    return { error: 'این نام قبلاً استفاده شده.' }
  }

  const next = (getSettings().customSubjects || []).map((subject) => (
    subject.id === id ? { ...subject, name } : subject
  ))
  try {
    updateSettings({ customSubjects: next })
  } catch (saveError) {
    return { error: saveError.message || 'این درس ذخیره نشد.' }
  }
  return { subject: { ...current, name } }
}

export function removeCustomSubject(id) {
  const next = (getSettings().customSubjects || []).filter((subject) => subject.id !== id)
  const weights = { ...(getSettings().subjectWeights || {}) }
  delete weights[id]
  updateSettings({ customSubjects: next, subjectWeights: weights })
}

export function hideBuiltinSubject(id) {
  const builtin = listSubjects({ includeHidden: true }).find((subject) => subject.id === id && !subject.custom)
  if (!builtin) return { error: 'این درس پیش‌فرض نیست.' }
  const hidden = new Set(getSettings().hiddenSubjects || [])
  hidden.add(id)
  const weights = { ...(getSettings().subjectWeights || {}) }
  delete weights[id]
  updateSettings({ hiddenSubjects: [...hidden], subjectWeights: weights })
  return { ok: true }
}

export function restoreBuiltinSubject(id) {
  const next = (getSettings().hiddenSubjects || []).filter((subjectId) => subjectId !== id)
  updateSettings({ hiddenSubjects: next })
  return { ok: true }
}

export function restoreAllBuiltinSubjects() {
  updateSettings({ hiddenSubjects: [] })
  return { ok: true }
}

export function removeSubject(id) {
  const subject = listSubjects({ includeHidden: true }).find((item) => item.id === id)
  if (!subject) return { error: 'این درس پیدا نشد.' }
  if (subject.custom) {
    removeCustomSubject(id)
    return { ok: true }
  }
  return hideBuiltinSubject(id)
}
