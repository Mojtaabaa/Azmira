import { COLLECTION_KEYS } from '../data/defaults'
import {
  assertCollection,
  createEmptyData,
  isPlainObject,
  normalizeCollection,
  normalizeData,
  normalizeSettings,
} from '../shared/normalize'
import { ApiError, request } from './api'
import { createId } from './id'

const SYNC_MESSAGE = 'ذخیره روی سرور انجام نشد. داده‌ها دوباره از سرور خوانده شد.'

let memory = createEmptyData()
let queue = Promise.resolve()
let pending = 0
let failed = false
let syncError = ''
const listeners = new Set()
const syncListeners = new Set()

function emit() {
  for (const listener of listeners) listener()
}

function emitSync() {
  for (const listener of syncListeners) listener()
}

function setSyncError(message) {
  syncError = message
  emitSync()
}

async function pull() {
  const payload = await request('GET', '/data')
  memory = normalizeData(payload, false)
  emit()
}

function enqueue(task) {
  pending += 1
  queue = queue
    .then(task)
    .catch((error) => {
      if (!(error instanceof ApiError && error.status === 401)) failed = true
    })
    .then(async () => {
      pending -= 1
      if (pending > 0 || !failed) return
      failed = false
      setSyncError(SYNC_MESSAGE)
      try {
        await pull()
      } catch {
        return
      }
    })
}

function writeData(data) {
  memory = data
  emit()
}

function recordPath(collection, id) {
  return `/data/${collection}/${encodeURIComponent(id)}`
}

export async function loadData() {
  queue = Promise.resolve()
  pending = 0
  failed = false
  setSyncError('')
  await pull()
}

export function resetData() {
  queue = Promise.resolve()
  pending = 0
  failed = false
  syncError = ''
  memory = createEmptyData()
  emitSync()
  emit()
}

export function refreshData() {
  if (pending > 0) return
  pull().catch(() => undefined)
}

export function subscribe(listener) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function subscribeSync(listener) {
  syncListeners.add(listener)
  return () => syncListeners.delete(listener)
}

export function getSyncError() {
  return syncError
}

export function clearSyncError() {
  setSyncError('')
}

export function hasRecords(data) {
  return COLLECTION_KEYS.some((key) => Array.isArray(data?.[key]) && data[key].length > 0)
}

export function getAll(collection) {
  assertCollection(collection)
  return memory[collection]
}

export function getOne(collection, id) {
  return getAll(collection).find((item) => item.id === id) ?? null
}

export function setAll(collection, items) {
  assertCollection(collection)
  const next = normalizeCollection(collection, items, true)
  writeData({ ...memory, [collection]: next })
  enqueue(() => request('PUT', `/data/${collection}`, { items: next }))
  return next
}

export function addOne(collection, item) {
  assertCollection(collection)
  if (!isPlainObject(item)) throw new Error('مورد باید یک شیء باشد.')

  const id = typeof item.id === 'string' && item.id.trim() ? item.id.trim() : createId()
  if (memory[collection].some((entry) => entry.id === id)) {
    throw new Error('شناسه تکراری است.')
  }

  const record = { ...item, id }
  writeData({ ...memory, [collection]: [...memory[collection], record] })
  enqueue(() => request('POST', `/data/${collection}`, record))
  return record
}

export function updateOne(collection, id, changes) {
  assertCollection(collection)
  if (!isPlainObject(changes)) throw new Error('تغییرها باید یک شیء باشند.')

  const current = memory[collection].find((entry) => entry.id === id)
  if (!current) return null

  const updated = { ...current, ...changes, id }
  writeData({
    ...memory,
    [collection]: memory[collection].map((entry) => (entry.id === id ? updated : entry)),
  })
  enqueue(() => request('PATCH', recordPath(collection, id), updated))
  return updated
}

export function deleteOne(collection, id) {
  assertCollection(collection)
  const next = memory[collection].filter((entry) => entry.id !== id)
  if (next.length === memory[collection].length) return false

  writeData({ ...memory, [collection]: next })
  enqueue(() => request('DELETE', recordPath(collection, id)))
  return true
}

export function getSettings() {
  return memory.settings
}

export function setSettings(settings) {
  const next = normalizeSettings(settings, true)
  writeData({ ...memory, settings: next })
  enqueue(() => request('PUT', '/settings', { settings: next }))
  return next
}

export function updateSettings(changes) {
  if (!isPlainObject(changes)) throw new Error('تنظیمات باید یک شیء باشد.')
  return setSettings({ ...memory.settings, ...changes })
}

export function exportAllData() {
  return JSON.parse(JSON.stringify(memory))
}

export function downloadBackup() {
  const data = exportAllData()
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  const day = new Date()
  const stamp = [
    day.getFullYear(),
    String(day.getMonth() + 1).padStart(2, '0'),
    String(day.getDate()).padStart(2, '0'),
  ].join('-')

  link.href = url
  link.download = `konkoor-help-${stamp}.json`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export function importAllData(incoming) {
  const data = normalizeData(incoming, true)
  writeData(data)
  enqueue(() => request('PUT', '/data', { data }))
  return exportAllData()
}

export async function replaceAllData(incoming, strict = false) {
  const data = normalizeData(incoming, strict)
  await queue
  await request('PUT', '/data', { data })
  writeData(data)
}

export function clearAllData() {
  const data = createEmptyData()
  writeData(data)
  enqueue(() => request('PUT', '/data', { data }))
}
