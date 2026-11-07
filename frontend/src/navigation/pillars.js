/**
 * Four-pillar navigation config for Rotbeh Phase 9.
 * Pillars are product navigation; account items stay capability-gated.
 */

export const PILLARS = {
  plan: {
    id: 'plan',
    label: 'برنامه',
    labelEn: 'PLAN',
    short: 'برنامه',
    icon: 'pillar-plan',
    hub: '/',
  },
  learn: {
    id: 'learn',
    label: 'یادگیری',
    labelEn: 'LEARN',
    short: 'یادگیری',
    icon: 'pillar-learn',
    hub: '/learn',
  },
  improve: {
    id: 'improve',
    label: 'بهبود',
    labelEn: 'IMPROVE',
    short: 'بهبود',
    icon: 'pillar-improve',
    hub: '/improve',
  },
  achieve: {
    id: 'achieve',
    label: 'دستاورد',
    labelEn: 'ACHIEVE',
    short: 'دستاورد',
    icon: 'pillar-achieve',
    hub: '/achieve',
  },
}

/** @type {Array<{ pillar?: string, label: string, items: Array<Record<string, unknown>> }>} */
export const NAV_GROUPS = [
  {
    pillar: 'plan',
    label: 'برنامه',
    labelEn: 'PLAN',
    items: [
      { to: '/', label: 'خانه', short: 'خانه', icon: 'home', end: true },
      { to: '/planner', label: 'برنامه‌ریز', short: 'هفته', icon: 'calendar' },
      { to: '/tasks', label: 'کارها', short: 'کار', icon: 'tasks' },
      { to: '/timer', label: 'تمرکز', short: 'تمرکز', icon: 'timer' },
    ],
  },
  {
    pillar: 'learn',
    label: 'یادگیری',
    labelEn: 'LEARN',
    items: [
      { to: '/learn', label: 'مرکز یادگیری', short: 'یادگیری', icon: 'pillar-learn', end: true },
      { to: '/topics', label: 'مباحث', short: 'مبحث', icon: 'topics' },
      { to: '/assessments', label: 'ارزیابی', short: 'ارزیابی', icon: 'tests' },
      { to: '/tests', label: 'آزمون‌ها', short: 'آزمون', icon: 'tests' },
      { to: '/reviews', label: 'بازبینی', short: 'مرور', icon: 'review' },
      { to: '/mastery', label: 'تسلط', short: 'تسلط', icon: 'mastery' },
      { to: '/notes', label: 'یادداشت‌ها', short: 'یادداشت', icon: 'notes' },
    ],
  },
  {
    pillar: 'improve',
    label: 'بهبود',
    labelEn: 'IMPROVE',
    items: [
      { to: '/improve', label: 'مرکز بهبود', short: 'بهبود', icon: 'pillar-improve', end: true },
      { to: '/analytics', label: 'آمار یادگیری', short: 'آمار', icon: 'chart' },
      { to: '/mistakes', label: 'اشتباهات', short: 'اشتباه', icon: 'mistakes' },
      { to: '/ai-coach', label: 'مربی یادگیری', short: 'مربی', icon: 'coach' },
    ],
  },
  {
    pillar: 'achieve',
    label: 'دستاورد',
    labelEn: 'ACHIEVE',
    items: [
      { to: '/achieve', label: 'مرکز دستاورد', short: 'دستاورد', icon: 'pillar-achieve', end: true },
      { to: '/missions', label: 'مأموریت‌های روزانه', short: 'مأموریت', icon: 'trophy' },
      { to: '/progress', label: 'پیشرفت و پاداش', short: 'پیشرفت', icon: 'chart' },
      { to: '/store', label: 'آواتار و فروشگاه', short: 'آواتار', icon: 'avatar' },
      { to: '/leaderboards', label: 'رتبه‌بندی', short: 'رتبه', icon: 'chart' },
      { to: '/teams', label: 'تیم‌ها', short: 'تیم', icon: 'tasks' },
      { to: '/competitions', label: 'مسابقات', short: 'مسابقه', icon: 'tests' },
    ],
  },
  {
    label: 'حساب و فضا',
    items: [
      { to: '/platform', label: 'ادمین سامانه', short: 'ادمین', icon: 'settings', requires: 'platformAdmin' },
      { to: '/school', label: 'مدرسه', short: 'مدرسه', icon: 'data', requires: 'schoolLink' },
      { to: '/teacher', label: 'معلم', short: 'معلم', icon: 'tasks', requires: 'teacher' },
      { to: '/parent', label: 'والدین', short: 'والد', icon: 'home', requires: 'parent' },
      { to: '/school/analytics', label: 'تحلیل مدرسه', short: 'تحلیل', icon: 'chart', requires: 'schoolAnalytics' },
      { to: '/data', label: 'انتقال داده', short: 'داده', icon: 'data' },
      { to: '/settings', label: 'تنظیمات', short: 'تنظیم', icon: 'settings' },
    ],
  },
]

/** Mobile bottom tabs: one entry per pillar hub. */
export const MOBILE_PILLAR_PATHS = ['/', '/learn', '/improve', '/achieve']

export function listPillarIds() {
  return Object.keys(PILLARS)
}

export function findActivePillar(pathname) {
  if (pathname === '/' || pathname === '') return 'plan'
  for (const group of NAV_GROUPS) {
    if (!group.pillar) continue
    for (const item of group.items) {
      if (item.end) {
        if (pathname === item.to) return group.pillar
      } else if (pathname === item.to || pathname.startsWith(`${item.to}/`)) {
        return group.pillar
      }
    }
  }
  return null
}
