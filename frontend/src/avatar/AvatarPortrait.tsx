// @ts-nocheck
import { useMemo } from 'react'
import { HAIR_COLORS, SKIN_TONES } from './assets'
import { appearanceLabel, resolveAppearance } from './resolveAppearance'
import './AvatarPortrait.css'

const VIEWBOX = '0 0 120 140'

function BackgroundLayer({ id }) {
  if (id === 'plain') {
    return <rect width="120" height="140" fill="var(--surface-solid, #f7f7f9)" />
  }
  if (id === 'library') {
    return (
      <g>
        <rect width="120" height="140" fill="#d9e2ec" />
        <rect x="8" y="18" width="14" height="104" rx="2" fill="#8aa0b5" opacity="0.55" />
        <rect x="28" y="28" width="14" height="94" rx="2" fill="#6f879c" opacity="0.5" />
        <rect x="98" y="22" width="14" height="100" rx="2" fill="#7f95aa" opacity="0.45" />
      </g>
    )
  }
  if (id === 'trail') {
    return (
      <g>
        <rect width="120" height="140" fill="#d7eadc" />
        <path d="M0 96 C40 78 80 112 120 88 L120 140 L0 140 Z" fill="#9bc4a5" opacity="0.7" />
        <circle cx="92" cy="34" r="14" fill="#f0c36a" opacity="0.55" />
      </g>
    )
  }
  // dawn (default)
  return (
    <g>
      <rect width="120" height="140" fill="#f0e4d4" />
      <circle cx="28" cy="28" r="18" fill="#f2b56b" opacity="0.55" />
      <path d="M0 108 C36 92 72 118 120 100 L120 140 L0 140 Z" fill="#c9b49a" opacity="0.55" />
    </g>
  )
}

function ClothingLayer({ id, accent }) {
  if (id === 'tee') {
    return (
      <path
        d="M38 86 L28 96 L36 118 L84 118 L92 96 L82 86 Z"
        fill={accent}
      />
    )
  }
  if (id === 'jacket') {
    return (
      <g>
        <path d="M36 84 L24 98 L34 122 L86 122 L96 98 L84 84 Z" fill="#2f3b4c" />
        <path d="M50 86 L60 118 L70 86" fill="none" stroke="#e8edf4" strokeWidth="2" />
      </g>
    )
  }
  return (
    <g>
      <path d="M36 84 L26 100 L36 122 L84 122 L94 100 L84 84 Z" fill={accent} />
      <path d="M44 92 H76" stroke="#fff" strokeOpacity="0.35" strokeWidth="3" strokeLinecap="round" />
    </g>
  )
}

function HairLayer({ style, color }) {
  if (style === 'shaved') {
    return <path d="M42 48 C48 36 72 34 78 48" fill="none" stroke={color} strokeWidth="3" strokeLinecap="round" />
  }
  if (style === 'short') {
    return <path d="M38 58 C40 34 80 34 82 58 C74 46 46 46 38 58 Z" fill={color} />
  }
  if (style === 'bun') {
    return (
      <g>
        <path d="M38 60 C40 34 80 34 82 60 C70 48 50 48 38 60 Z" fill={color} />
        <circle cx="60" cy="30" r="10" fill={color} />
      </g>
    )
  }
  return <path d="M36 62 C34 30 86 30 84 62 C78 44 42 44 36 62 Z" fill={color} />
}

function AccessoryLayer({ id }) {
  if (id === 'glasses') {
    return (
      <g fill="none" stroke="#1f2937" strokeWidth="2.2">
        <circle cx="50" cy="62" r="7" />
        <circle cx="70" cy="62" r="7" />
        <path d="M57 62 H63" />
      </g>
    )
  }
  if (id === 'headphones') {
    return (
      <g fill="#374151">
        <path d="M40 54 C40 38 80 38 80 54" fill="none" stroke="#374151" strokeWidth="4" />
        <rect x="34" y="54" width="10" height="16" rx="3" />
        <rect x="76" y="54" width="10" height="16" rx="3" />
      </g>
    )
  }
  if (id === 'scarf') {
    return <path d="M44 86 C52 96 68 96 76 86 L72 104 L48 104 Z" fill="#c45c26" opacity="0.9" />
  }
  return null
}

function CompanionLayer({ id }) {
  if (id === 'owl') {
    return (
      <g transform="translate(88 96)">
        <ellipse cx="12" cy="14" rx="11" ry="12" fill="#8b6914" />
        <circle cx="8" cy="12" r="3" fill="#f8f1df" />
        <circle cx="16" cy="12" r="3" fill="#f8f1df" />
        <circle cx="8" cy="12" r="1.2" fill="#1f1a17" />
        <circle cx="16" cy="12" r="1.2" fill="#1f1a17" />
      </g>
    )
  }
  if (id === 'fox') {
    return (
      <g transform="translate(86 100)">
        <path d="M4 20 L12 4 L20 20 Z" fill="#d97706" />
        <circle cx="12" cy="16" r="2" fill="#1f1a17" />
      </g>
    )
  }
  return null
}

/**
 * Layered SVG avatar. Asset definitions live in ./assets; rendering stays here.
 */
export default function AvatarPortrait({
  appearance: appearanceProp,
  loadout = null,
  inventory = [],
  localOverrides,
  size = 'md',
  label,
  fallbackInitial,
  className = '',
}) {
  const appearance = useMemo(
    () =>
      appearanceProp ||
      resolveAppearance({ loadout, inventory, localOverrides }),
    [appearanceProp, loadout, inventory, localOverrides],
  )

  const skin = SKIN_TONES[appearance.skinTone] || SKIN_TONES.medium
  const hair = HAIR_COLORS[appearance.hairColor] || HAIR_COLORS.chestnut
  const clothingAccent =
    appearance.clothing === 'tee' ? '#3b6ea5' : appearance.clothing === 'jacket' ? '#2f3b4c' : '#2f6f6a'
  const aria = label || appearanceLabel(appearance)

  if (!appearance) {
    return (
      <div
        className={`avatar-portrait is-fallback is-${size} ${className}`.trim()}
        role="img"
        aria-label={fallbackInitial ? `آواتار ${fallbackInitial}` : 'آواتار پیش‌فرض'}
      >
        {(fallbackInitial || 'ر').slice(0, 1)}
      </div>
    )
  }

  return (
    <div
      className={`avatar-portrait is-${size} ${className}`.trim()}
      role="img"
      aria-label={aria}
    >
      <svg className="avatar-portrait__svg" viewBox={VIEWBOX} xmlns="http://www.w3.org/2000/svg">
        <BackgroundLayer id={appearance.background} />
        <ClothingLayer id={appearance.clothing} accent={clothingAccent} />
        <ellipse cx="60" cy="118" rx="28" ry="8" fill="#000" opacity="0.08" />
        <circle cx="60" cy="58" r="24" fill={skin} />
        <ellipse cx="52" cy="62" rx="2.2" ry="2.6" fill="#1f1a17" />
        <ellipse cx="68" cy="62" rx="2.2" ry="2.6" fill="#1f1a17" />
        <path d="M54 72 C58 76 62 76 66 72" fill="none" stroke="#8a5a44" strokeWidth="2" strokeLinecap="round" />
        <HairLayer style={appearance.hairStyle} color={hair} />
        <AccessoryLayer id={appearance.accessory} />
        <CompanionLayer id={appearance.companion} />
      </svg>
    </div>
  )
}
