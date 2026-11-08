import assert from 'node:assert/strict'
import test from 'node:test'
import { DEFAULT_APPEARANCE } from './assets.js'
import { appearanceLabel, resolveAppearance } from './resolveAppearance.js'

test('falls back to safe defaults', () => {
  const a = resolveAppearance({ loadout: null, inventory: [], localOverrides: null })
  assert.deepEqual(a, DEFAULT_APPEARANCE)
})

test('maps store asset keys deterministically', () => {
  const baseId = '11111111-1111-1111-1111-111111111111'
  const hairish = '22222222-2222-2222-2222-222222222222'
  const a = resolveAppearance({
    loadout: { baseItemId: baseId, clothingItemId: hairish },
    inventory: [
      { itemId: baseId, assetKey: 'avatar.base.fair' },
      { itemId: hairish, assetKey: 'avatar.clothing.jacket' },
    ],
    localOverrides: null,
  })
  assert.equal(a.skinTone, 'fair')
  assert.equal(a.clothing, 'jacket')
  assert.equal(a.hairStyle, DEFAULT_APPEARANCE.hairStyle)
})

test('local prototype overrides without mutating defaults object', () => {
  const a = resolveAppearance({
    localOverrides: { accessory: 'glasses', companion: 'owl' },
  })
  assert.equal(a.accessory, 'glasses')
  assert.equal(a.companion, 'owl')
  assert.equal(DEFAULT_APPEARANCE.accessory, 'none')
})

test('appearanceLabel is stable Persian summary', () => {
  const label = appearanceLabel(DEFAULT_APPEARANCE)
  assert.match(label, /پوست/)
  assert.match(label, /مو/)
})
