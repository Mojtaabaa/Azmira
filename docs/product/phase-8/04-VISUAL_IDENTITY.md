# 4. Visual identity & design system proposal

## Direction
**Adventurous · optimistic · modern · educational · credible.**  
Appeal to school students without childish tropes; remain suitable for older learners and institutions.

## Color (evolve current tokens — do not purple-default)

Keep terracotta accent as brand signal; refine roles:

| Token | Role | Light proposal | Dark |
|-------|------|----------------|------|
| `--accent` | Brand CTA / energy | `#c45c26` (keep) | `#e8924a` |
| `--success` | Mastery / unlock | deepen slightly for WCAG | keep |
| `--info` | LEARN / cool focus | `#3b6ea5` | `#6ea0d4` |
| `--warning` | Due / attention | keep | keep |
| `--pillar-plan` | Nav accent | muted teal `#2f6f6a` | softer |
| `--pillar-learn` | Nav accent | info | |
| `--pillar-improve` | Nav accent | indigo-slate `#4a5d8a` | |
| `--pillar-achieve` | Nav accent | accent / gold-adjacent | |

Backgrounds: keep atmospheric soft orbs; avoid cream+serif+terracotta AI cliché by keeping Estedad geometric + cool-gray bases already in use.

**School themes:** map school brand hex → CSS variables via controlled overlay; do not invent parallel card systems.

## Typography
- Primary: **Estedad** (already loaded) for UI + display
- Fallback: Vazirmatn
- Weights: display 700 for brand/home title only; body 400/500; avoid Inter/Roboto
- Numeric tabular for XP / scores

## Iconography
- Single stroke set (current Layout SVG style)
- Unique icons per route (replace repeated `chart`)
- Pillar icons: calendar/compass (PLAN), book/path (LEARN), rising chart (IMPROVE), torch/badge (ACHIEVE) — geometric, not cartoon

## Illustrations
- Journey regions: flat vector landscapes, limited palette, no mascot overload
- Empty states: one calm illustration family (line + soft fill)
- Avatar: layered SVG/CSS first (see Avatar doc)

## Cards & progress
- Prefer **open sections** on Home; cards only for interactive units (missions, store items)
- Unify meters: one `.meter` API (track/fill/label); deprecate duplicate bars gradually
- Progress rings for level; linear for mastery evidence

## Motion
Ship 2–3 intentional motions:
1. Home section enter (stagger fade/slide, `prefers-reduced-motion` safe)
2. Mission complete check + soft confetti (optional, disable in reduced-motion)
3. Journey stage unlock path draw

No glow spam; use `--ease-out` / `--ease-spring` already defined.

## States
| State | Pattern |
|-------|---------|
| Empty | Illustration + one sentence + one CTA |
| Loading | Skeleton matching section shape (not spinner walls) |
| Success | Toast (existing) + inline check |
| Error | Inline + retry; danger tokens |

## Component targets (Phase 9)
`PillarNav`, `LearnerHome`, `EmptyState`, `Meter`, `AvatarPortrait`, `MissionCard`, `JourneyNode` — all token-driven, RTL-aware.