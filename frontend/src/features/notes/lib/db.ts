// @ts-nocheck
import { ApiError, request } from '../../../utils/api'

export async function loadVault(_userId?: string) {
  return request('GET', '/notes/vault')
}

export async function putNote(_userId: string, note: unknown) {
  await request('PUT', '/notes/notes', note)
}

export async function putFolder(_userId: string, folder: unknown) {
  await request('PUT', '/notes/folders', folder)
}

export async function deleteNoteRow(_userId: string, id: string) {
  await request('DELETE', `/notes/notes/${encodeURIComponent(id)}`)
}

export async function deleteFolderRow(_userId: string, id: string) {
  await request('DELETE', `/notes/folders/${encodeURIComponent(id)}`)
}

export async function putSetting(_userId: string, key: string, value: unknown) {
  await request('PUT', '/notes/settings', { key, value })
}

export async function putTrash(_userId: string, item: unknown) {
  await request('PUT', '/notes/trash', item)
}

export async function restoreTrash(_userId: string, id: string) {
  try {
    return await request('POST', `/notes/trash/${encodeURIComponent(id)}/restore`)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}

export async function putImage(_userId: string, image: unknown) {
  await request('PUT', '/notes/images', image)
}

export async function getImage(_userId: string, id: string) {
  try {
    return await request('GET', `/notes/images/${encodeURIComponent(id)}`)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return undefined
    throw error
  }
}

export async function purgeOldTrash(_userId: string, days = 30) {
  await request('DELETE', `/notes/trash?days=${days}`)
}
