const cache = new Map()

export function rememberImage(id, dataUrl) {
  if (id && dataUrl) cache.set(id, dataUrl)
}

export function peekImage(id) {
  return cache.get(id) || ''
}
