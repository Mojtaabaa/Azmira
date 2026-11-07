# 2. Product information architecture

## North star

One coherent **learner home** that surfaces all four pillars, with navigation that names them. School/teacher/parent remain capability-gated side experiences, not pillars.

## Proposed main navigation (desktop)

| Pillar | Primary destinations | Secondary |
|--------|----------------------|-----------|
| **PLAN** | Home (today), Planner, Tasks | Notes, Settings goals |
| **LEARN** | Timer / Focus, Topics, Assessments | Tests (legacy), Notes |
| **IMPROVE** | Reviews, Mastery/Journey, Analytics | Mistakes, AI Coach |
| **ACHIEVE** | Progress & Missions, Achievements | Store/Avatar, Leaderboards (personal improvement first), Teams, Competitions |

**Account / ops** (footer of nav): Settings, Data, School*, Teacher*, Parent*, Platform*  
\* capability-gated as today.

## Mobile
- Bottom tabs (4): **امروز** (Home), **یادگیری** (LEARN hub), **بهبود** (IMPROVE hub), **دستاورد** (ACHIEVE hub)
- Planner/Timer accessible from Home quick actions and LEARN hub
- “More” sheet for Settings, School, social extras

## Learner home dashboard (single composition)

Not four dashboards. One screen with ordered sections:

1. **Today strip** — date, streak (server), next review due count, active daily missions (max 3)
2. **PLAN snapshot** — today’s tasks/events count + CTA “باز کردن برنامه”
3. **LEARN focus** — continue topic / start timer / due assessment
4. **IMPROVE pulse** — mastery delta 7d, weak subjects, review CTA
5. **ACHIEVE** — level progress, claimable quests, avatar thumbnail
6. Optional school banner if active workspace is school

## Daily overview
- Embedded in Home Today strip + expandable “روز من”
- Inputs: planner events, timer sessions today, reviews due, mission states

## Learning progress overview
- IMPROVE hub: Journey map entry + mastery summary + analytics deep-link
- ACHIEVE hub: badges/missions inventory (not raw XP dump as hero)

## Responsive rules
- Desktop: sidebar pillar groups + home grid 12-col
- Tablet: collapsed labels, same order
- Mobile: 4 bottom tabs; home stacks sections; no horizontal scroll traps

## Explicit non-goals for IA
- Separate apps per pillar
- Hiding planner/timer behind paywalls
- Replacing school portals with learner pillars