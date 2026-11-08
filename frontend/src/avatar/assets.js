/**
 * Rotbeh avatar asset catalog (SVG/CSS first).
 * Future commissioned packs register here without changing AvatarPortrait.
 */

/** @typedef {'fair'|'light'|'medium'|'tan'|'deep'} SkinTone */
/** @typedef {'short'|'wavy'|'bun'|'shaved'} HairStyle */
/** @typedef {'hoodie'|'tee'|'jacket'} ClothingId */
/** @typedef {'none'|'glasses'|'headphones'|'scarf'} AccessoryId */
/** @typedef {'none'|'owl'|'fox'} CompanionId */
/** @typedef {'plain'|'dawn'|'library'|'trail'} BackgroundId */

export const SKIN_TONES = {
  fair: '#f3d4b8',
  light: '#e2b896',
  medium: '#c6865a',
  tan: '#a0653d',
  deep: '#6b3f2a',
}

export const HAIR_COLORS = {
  ink: '#1f1a17',
  chestnut: '#5c3317',
  copper: '#8a4b28',
  sand: '#c4a574',
  mist: '#6b7280',
}

export const DEFAULT_APPEARANCE = Object.freeze({
  skinTone: 'medium',
  hairStyle: 'wavy',
  hairColor: 'chestnut',
  clothing: 'hoodie',
  accessory: 'none',
  companion: 'none',
  background: 'dawn',
})

/** Map store AssetKey / CssClass fragments → appearance fields. */
export const ASSET_KEY_MAP = {
  'avatar.base.default': { skinTone: 'medium' },
  'avatar.base.fair': { skinTone: 'fair' },
  'avatar.base.deep': { skinTone: 'deep' },
  'avatar.hair.short': { hairStyle: 'short' },
  'avatar.hair.wavy': { hairStyle: 'wavy' },
  'avatar.hair.bun': { hairStyle: 'bun' },
  'avatar.hair.shaved': { hairStyle: 'shaved' },
  'avatar.clothing.hoodie': { clothing: 'hoodie' },
  'avatar.clothing.tee': { clothing: 'tee' },
  'avatar.clothing.jacket': { clothing: 'jacket' },
  'avatar.accessory.glasses': { accessory: 'glasses' },
  'avatar.accessory.headphones': { accessory: 'headphones' },
  'avatar.accessory.scarf': { accessory: 'scarf' },
  'avatar.companion.owl': { companion: 'owl' },
  'avatar.companion.fox': { companion: 'fox' },
  'avatar.bg.plain': { background: 'plain' },
  'avatar.bg.dawn': { background: 'dawn' },
  'avatar.bg.library': { background: 'library' },
  'avatar.bg.trail': { background: 'trail' },
}

export const LOCAL_PROTOTYPE_KEY = 'rotbeh.avatar.prototype'

export function readLocalPrototype() {
  if (typeof localStorage === 'undefined') return null
  try {
    const raw = localStorage.getItem(LOCAL_PROTOTYPE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw)
    return parsed && typeof parsed === 'object' ? parsed : null
  } catch {
    return null
  }
}

export function writeLocalPrototype(partial) {
  if (typeof localStorage === 'undefined') return
  const next = { ...(readLocalPrototype() || {}), ...partial }
  localStorage.setItem(LOCAL_PROTOTYPE_KEY, JSON.stringify(next))
  return next
}
