function formatMinutesFa(totalMinutes) {
  const minutes = Math.max(0, Math.round(Number(totalMinutes) || 0))
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  if (hours === 0) return `${rest} دقیقه`
  if (rest === 0) return `${hours} ساعت`
  return `${hours} ساعت و ${rest} دقیقه`
}

/**
 * Evidence-based next action. Never invents metrics.
 * @returns {{ title: string, copy: string, to: string, action: string, reason: string }}
 */
export function recommendNext({
  overdue = 0,
  openMistakes = 0,
  studied = 0,
  goal = 0,
  openTasks = 0,
  reviewsDue = null,
  weakMasteryCount = null,
} = {}) {
  if (overdue > 0) {
    return {
      title: `${overdue} کار از موعد گذشته`,
      copy: 'اول همان‌ها را ببند تا فهرست امروز شلوغ نماند.',
      to: '/tasks?filter=overdue',
      action: 'دیدن کارها',
      reason: 'overdue_tasks',
    }
  }
  if (typeof reviewsDue === 'number' && reviewsDue > 0) {
    return {
      title: `${reviewsDue} بازبینی سررسید شده`,
      copy: 'مرور فاصله‌دار از شواهد یادگیری است — نه صرفاً زمان مطالعه.',
      to: '/reviews',
      action: 'شروع بازبینی',
      reason: 'reviews_due',
    }
  }
  if (openMistakes > 0) {
    return {
      title: `${openMistakes} اشتباه هنوز مرور نشده`,
      copy: 'مرور اشتباه، بیشتر از یک آزمون تازه به درصد کمک می‌کند.',
      to: '/mistakes',
      action: 'مرور اشتباهات',
      reason: 'open_mistakes',
    }
  }
  if (typeof weakMasteryCount === 'number' && weakMasteryCount > 0) {
    return {
      title: `${weakMasteryCount} مبحث نیاز به تقویت دارد`,
      copy: 'بر اساس شواهد تسلط، روی نقاط ضعف تمرکز کن.',
      to: '/mastery',
      action: 'نقشه تسلط',
      reason: 'weak_mastery',
    }
  }
  if (goal > 0 && studied < goal) {
    return {
      title: `${formatMinutesFa(goal - studied)} تا هدف امروز مانده`,
      copy: 'یک نوبت کوتاه هم کافی است. درس و مبحث را در زمان‌سنج انتخاب کن.',
      to: '/timer',
      action: 'شروع تمرکز',
      reason: 'daily_goal',
    }
  }
  if (openTasks > 0) {
    return {
      title: `${openTasks} کار برای امروز`,
      copy: 'اگر انرژی ماند، کار همین امروز را انجام بده.',
      to: '/tasks',
      action: 'فهرست کارها',
      reason: 'today_tasks',
    }
  }
  return {
    title: 'برنامه هفته را مرتب کن',
    copy: 'کار عقب‌افتاده و اشتباه باز نداری. برنامه روزهای بعد را ببین.',
    to: '/planner',
    action: 'رفتن به برنامه',
    reason: 'planner',
  }
}
