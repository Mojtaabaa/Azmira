# 5. Avatar domain model & component design

## Principles
- Cosmetics celebrate **meaningful learning achievements**, not paid mastery.
- Academic mastery is **not purchasable**.
- Extensible slots; Phase 8 defines model + architecture only (small catalog later).

## Domain model (logical)

```
AvatarIdentity
  workspaceId, userId
  displayName?, pronouns?
  baseCharacterId   // StoreItemCategory.AvatarBase or free default
  evolutionTier     // 0..N from achievement milestones (visual only)

AvatarLoadout (extends today's UserAvatarLoadout)
  baseCharacterId
  hairId?
  clothingId?
  accessoryIds[]    // capped
  equipmentId?      // e.g. backpack / tool — cosmetic
  companionId?
  backgroundId?
  frameId?
  equippedAt

AvatarInventoryItem
  itemId, acquiredAt, source: Purchase | Achievement | Journey | Gift
  lockedReason?     // if preview-only

AvatarItemDefinition
  id, category, assetKey, cssClass, rarity
  unlockRule: { type: Coins | Badge | JourneyStage | MissionStreak | MasteryEvidence, params }
  accessibilityLabel
  defaultSafe: bool
```

## Mapping to existing schema
- Reuse `StoreItem`, `UserInventoryItem`, `UserAvatarLoadout`
- Phase 9+: add columns or JSON `Slots` for hair/companion; prefer JSON `EquippedSlots` on loadout to avoid wide migrations until catalog solidifies
- New unlock service: evaluate `unlockRule` against badges/journey/mastery — **not** against XP alone for mastery cosmetics

## Component architecture (frontend)

```
<AvatarPortrait size="sm|md|lg" loadout={...} />
  └─ layers: Background → Base → Clothing → Hair → Accessories → Companion → Frame
<AvatarEditor />  // inventory grid + equip (store page refactor)
```

Initial visual prototype (optional Phase 9): CSS layers + placeholder SVG shapes using existing `CssClass` / `AssetKey`. No large asset pack in Phase 8.

## Safe defaults & a11y
- Default base character gender-neutral silhouette
- Every item has `accessibilityLabel`; portrait `role="img"` + alt from equipped labels
- Respect reduced motion; no flashing evolution FX
- School policy flag: disable store purchases while allowing achievement cosmetics

## Anti-patterns
- Selling higher mastery indicators
- Pay-to-win companions that imply rank
- Random loot boxes for minors without parental controls (future policy)