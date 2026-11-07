import { ASSET_KEY_MAP, DEFAULT_APPEARANCE, readLocalPrototype } from './assets.js'

function pickMapped(item) {
  if (!item) return null
  const key = item.assetKey || item.AssetKey || ''
  if (ASSET_KEY_MAP[key]) return ASSET_KEY_MAP[key]
  const css = item.cssClass || item.CssClass || ''
  for (const [mapKey, value] of Object.entries(ASSET_KEY_MAP)) {
    const slug = mapKey.split('.').pop()
    if (css.includes(slug) || css.includes(mapKey.replace(/\./g, '-'))) return value
  }
  return null
}

/**
 * Build a deterministic appearance from server loadout + inventory + optional local prototype.
 * Never invent mastery or rewards — visual config only.
 */
export function resolveAppearance({ loadout = null, inventory = [], localOverrides = null } = {}) {
  const byId = Object.fromEntries(
    (inventory || []).map((item) => [String(item.itemId || item.ItemId || item.id), item]),
  )

  const layers = [
    loadout?.backgroundItemId,
    loadout?.baseItemId,
    loadout?.clothingItemId,
    loadout?.accessoryItemId,
    loadout?.frameItemId,
  ]
    .filter(Boolean)
    .map((id) => byId[String(id)])
    .filter(Boolean)

  let appearance = { ...DEFAULT_APPEARANCE }

  for (const layer of layers) {
    const mapped = pickMapped(layer)
    if (mapped) appearance = { ...appearance, ...mapped }
  }

  const local = localOverrides === undefined ? readLocalPrototype() : localOverrides
  if (local && typeof local === 'object') {
    appearance = { ...appearance, ...local }
  }

  return Object.freeze({ ...appearance })
}

export function appearanceLabel(appearance) {
  const a = appearance || DEFAULT_APPEARANCE
  return [
    `پوست ${a.skinTone}`,
    `مو ${a.hairStyle}`,
    `لباس ${a.clothing}`,
    a.accessory !== 'none' ? `اکسسوری ${a.accessory}` : null,
    a.companion !== 'none' ? `همراه ${a.companion}` : null,
  ]
    .filter(Boolean)
    .join('، ')
}
