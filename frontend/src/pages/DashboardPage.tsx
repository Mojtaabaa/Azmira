// @ts-nocheck
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AvatarPortrait from '../avatar/AvatarPortrait'
import CountdownCard from '../components/dashboard/CountdownCard'
import GoalMeter from '../components/dashboard/GoalMeter'
import RecentTests from '../components/dashboard/RecentTests'
import SubjectBalance from '../components/dashboard/SubjectBalance'
import TodayLists from '../components/dashboard/TodayLists'
import EmptyState from '../components/ui/EmptyState'
import KpiStat from '../components/ui/KpiStat'
import Meter from '../components/ui/Meter'
import PillarSection from '../components/ui/PillarSection'
import StatusChip from '../components/ui/StatusChip'
import { useAuth } from '../hooks/useAuth'
import { useCollection } from '../hooks/useCollection'
import { useSettings } from '../hooks/useSettings'
import { formatHourCount, formatMinutes, todayInputValue } from '../utils/clock'
import {
  overdueCount,
  recentTests,
  todayStudyMinutes,
  todayTasks,
  unreviewedMistakes,
  weekReview,
} from '../utils/dashboard'
import { eventsOnDay } from '../utils/events'
import { countdownToKonkoor } from '../utils/konkoor'
import { recommendNext } from '../utils/recommendNext'
import { request } from '../utils/api'
import { tasksForFilter } from '../utils/tasks'
import { coverageOf } from '../utils/topics'
import './DashboardPage.css'

function greetingForHour(date = new Date()) {
  const hour = date.getHours()
  if (hour < 12) return 'صبح بخیر'
  if (hour < 18) return 'روز بخیر'
  return 'عصر بخیر'
}

function missionProgressRatio(mission) {
  const target = Number(mission?.targetValue) || 0
  if (target <= 0) return mission?.status === 'Claimed' || mission?.status === 'Completed' ? 100 : 0
  return Math.round((Math.min(Number(mission.progress) || 0, target) / target) * 100)
}

export default function DashboardPage() {
  const { user, status } = useAuth()
  const settings = useSettings()
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const tasks = useCollection('tasks')
  const events = useCollection('events')
  const topics = useCollection('topics')

  const [profile, setProfile] = useState(null)
  const [profileState, setProfileState] = useState('loading')
  const [loadout, setLoadout] = useState(null)
  const [inventory, setInventory] = useState([])
  const [reviewsDue, setReviewsDue] = useState(null)
  const [weakMasteryCount, setWeakMasteryCount] = useState(null)
  const [remoteError, setRemoteError] = useState('')
  const [dailyMissions, setDailyMissions] = useState(null)
  const [journeySummary, setJourneySummary] = useState(null)
  const [improvementPulse, setImprovementPulse] = useState(null)

  useEffect(() => {
    if (status !== 'authed') return undefined
    let alive = true
    setProfileState('loading')
    setRemoteError('')

    // Phase 16: compose-first; inventory only for avatar (not covered by home).
    Promise.all([
      request('GET', '/learner/home', undefined, { silent: true }).catch(() => null),
      request('GET', '/store/inventory', undefined, { silent: true }).catch(() => null),
    ]).then(([home, inv]) => {
      if (!alive) return
      const profilePayload = home?.profile || null
      if (profilePayload) {
        setProfile(profilePayload)
        setProfileState('ready')
      } else {
        setProfile(null)
        setProfileState('empty')
      }
      setLoadout(inv?.loadout || null)
      setInventory(inv?.items || [])
      setReviewsDue(home?.reviewsDue != null ? home.reviewsDue : null)
      setWeakMasteryCount(home?.weakMasteryCount != null ? home.weakMasteryCount : null)
      setDailyMissions(home?.dailyMissions || null)
      setJourneySummary(home?.journeySummary || null)
      setImprovementPulse(home?.improvementPulse || null)
    }).catch((err) => {
      if (!alive) return
      setProfileState('error')
      setRemoteError(err?.message || 'بارگذاری خلاصه ممکن نشد.')
    })

    return () => {
      alive = false
    }
  }, [status])

  const view = useMemo(() => {
    const today = todayInputValue()
    const goal = settings.dailyGoalMinutes
    const weekGoal = settings.weeklyGoalMinutes
    const studied = todayStudyMinutes(sessions)
    const week = weekReview({
      sessions,
      tests,
      events,
      dailyGoal: goal,
      weeklyGoal: weekGoal,
      today,
    })
    return {
      today,
      goal,
      weekGoal,
      studied,
      week,
      openMistakes: unreviewedMistakes(mistakes),
      countdown: countdownToKonkoor(settings),
      coverage: coverageOf(topics),
      openTasks: todayTasks(tasks, today),
      overdue: overdueCount(tasks, today),
      recent: recentTests(tests),
      todayEvents: eventsOnDay(events, today),
      overdueTasks: tasksForFilter(tasks, 'overdue', today),
    }
  }, [sessions, tests, mistakes, tasks, events, topics, settings])

  const next = useMemo(
    () =>
      recommendNext({
        overdue: view.overdue,
        openMistakes: view.openMistakes.length,
        studied: view.studied,
        goal: view.goal,
        openTasks: view.openTasks.length,
        reviewsDue,
        weakMasteryCount,
      }),
    [view, reviewsDue, weakMasteryCount],
  )

  const streakDisplay =
    profile && typeof profile.streakCurrent === 'number' ? profile.streakCurrent : '—'

  const levelProgress = Math.round((profile?.levelProgressRatio || 0) * 100)
  const composite = improvementPulse?.composite || null
  const improveStatus = composite?.status || null
  const learnPrimaryTo = reviewsDue > 0 ? '/reviews' : '/journey'
  const learnPrimaryLabel = reviewsDue > 0 ? 'بازبینی سررسید' : 'نقشه سفر'

  return (
    <div className="page is-wide learner-home">
      <header className="page-header learner-home__welcome">
        <AvatarPortrait
          size="lg"
          loadout={loadout}
          inventory={inventory}
          fallbackInitial={user?.username}
        />
        <div className="learner-home__welcome-copy">
          <p className="dash-hello">
            {greetingForHour()}، {user?.username}
          </p>
          <h1>خانه یادگیری</h1>
          <p>
            {view.countdown.started
              ? 'روز کنکور رسیده است.'
              : `${view.countdown.days} روز تا کنکور.`}
            {' '}
            چهار ستون رتبه: برنامه، یادگیری، بهبود، دستاورد.
          </p>
          {profileState === 'ready' && profile ? (
            <p className="learner-home__level" dir="ltr">
              سطح {profile.level} · {profile.xpTotal} XP
            </p>
          ) : profileState === 'loading' ? (
            <p className="learner-home__muted">در حال بارگذاری پیشرفت…</p>
          ) : profileState === 'error' ? (
            <p className="learner-home__error">{remoteError}</p>
          ) : (
            <p className="learner-home__muted">هنوز پروفایل پیشرفت سروری در دسترس نیست.</p>
          )}
        </div>
        <ul className="dash-quick-stats" aria-label="خلاصه سریع">
          <li>
            <span>امروز</span>
            <strong>{formatMinutes(view.studied)}</strong>
          </li>
          <li>
            <span>روز پیاپی</span>
            <strong dir="ltr">{streakDisplay}</strong>
          </li>
          <li>
            <span>کار مانده</span>
            <strong dir="ltr">{view.openTasks.length + view.overdue}</strong>
          </li>
          <li>
            <span>بازبینی</span>
            <strong dir="ltr">{reviewsDue == null ? '—' : reviewsDue}</strong>
          </li>
        </ul>
      </header>

      <div className="stack learner-home__stack">
        <PillarSection
          pillar="plan"
          eyebrow="PLAN · امروز"
          title="برنامه و گام بعدی"
          action={
            <Link className="button button-primary" to={next.to}>
              {next.action}
            </Link>
          }
        >
          <p className="card-copy learner-home__next-copy">
            <strong>{next.title}</strong> — {next.copy}
          </p>
          {view.openTasks.length === 0 && view.todayEvents.length === 0 && view.overdue === 0 ? (
            <EmptyState
              pillar="plan"
              title="برنامه امروز خالی است"
              description="یک کار یا رویداد برای امروز اضافه کن تا مسیر روشن شود."
              actionTo="/tasks"
              actionLabel="افزودن کار"
            />
          ) : (
            <TodayLists
              tasks={view.openTasks}
              events={view.todayEvents}
              overdue={view.overdue}
            />
          )}
          <div className="learner-home__row learner-home__row--flat">
            <GoalMeter
              title="تمرکز امروز"
              doneMinutes={view.studied}
              planMinutes={view.goal}
              planLabel={`از هدف ${formatHourCount(view.goal)} ساعته`}
              note={view.studied > view.goal ? 'از هدف امروز رد شدی.' : ''}
              to="/timer"
              action="شروع تمرکز"
            />
            <CountdownCard countdown={view.countdown} />
          </div>
          <Link className="text-link" to="/planner">باز کردن برنامه‌ریز</Link>
        </PillarSection>

        <PillarSection
          pillar="learn"
          eyebrow="LEARN · یادگیری"
          title="الان روی چه چیزی کار می‌کنی؟"
          action={<Link className="button button-secondary" to={learnPrimaryTo}>{learnPrimaryLabel}</Link>}
        >
          <div className="kpi-stat-row">
            <KpiStat label="مباحث" value={topics.length} />
            <KpiStat label="پوشش فهرست" value={`${view.coverage}%`} />
            <KpiStat label="نقاط ضعیف" value={weakMasteryCount == null ? '—' : weakMasteryCount} />
          </div>
          <Meter value={view.coverage} max={100} label="پوشش مباحث" tone="learn" />
          {journeySummary ? (
            <div className="learner-home__pulse">
              <p>
                سفر: <strong>{journeySummary.title || 'نقشه یادگیری'}</strong>
                {journeySummary.isBootstrap ? ' · راه‌اندازی اولیه' : ''}
              </p>
              {journeySummary.summary?.completed != null ? (
                <p className="learner-home__note" dir="ltr">
                  {journeySummary.summary.completed}/{journeySummary.summary.total || '—'} مرحله
                </p>
              ) : null}
            </div>
          ) : null}
          <p className="learner-home__note">
            پوشش مباحث فهرست‌نویسی است؛ تسلط فقط با شواهد آزمون و بازبینی معنا دارد.
          </p>
          <div className="learner-home__actions">
            <Link className="text-link" to="/mastery">نقشه تسلط</Link>
          </div>
          {view.recent?.length ? <RecentTests tests={view.recent} /> : null}
        </PillarSection>

        <PillarSection
          pillar="improve"
          eyebrow="IMPROVE · بهبود شخصی"
          title="نسبت به معیار خودت چطور پیش می‌روی؟"
          action={<Link className="button button-secondary" to="/improvement">بهبود شخصی</Link>}
        >
          {!improvementPulse ? (
            <EmptyState
              pillar="improve"
              title="نبض بهبود هنوز آماده نیست"
              description="با ارزیابی یا بازبینی، معیار پایه ساخته می‌شود. رتبه ساختگی نشان داده نمی‌شود."
              actionTo="/assessments"
              actionLabel="شروع ارزیابی"
            />
          ) : improveStatus === 'Collecting' ? (
            <EmptyState
              pillar="improve"
              title="در حال جمع‌آوری شواهد"
              description="برای Ready شدن به شواهد بیشتری نیاز است."
              actionTo="/assessments"
              actionLabel="ادامه ارزیابی"
            />
          ) : (
            <div className="learner-home__improve-ready">
              <div className="kpi-stat-row">
                <div className="kpi-stat">
                  <span className="kpi-stat__label">وضعیت</span>
                  <StatusChip status={improveStatus || 'Ready'} />
                </div>
                <KpiStat
                  label="Δ بهبود"
                  value={
                    composite?.improvementPercent != null
                      ? `${Number(composite.improvementPercent).toFixed(1)}%`
                      : '—'
                  }
                />
                <KpiStat
                  label="نقاط ضعیف"
                  value={weakMasteryCount == null ? '—' : weakMasteryCount}
                />
              </div>
              <p className="learner-home__note">
                مقایسه گروهی اختیاری و پیش‌فرض خصوصی است. نمودار کامل در مرکز بهبود.
              </p>
            </div>
          )}
          <div className="learner-home__row learner-home__row--flat">
            <GoalMeter
              title="فعالیت مطالعه این هفته"
              doneMinutes={view.week.studied}
              planMinutes={view.weekGoal}
              planLabel={`از هدف ${formatHourCount(view.weekGoal)} ساعته`}
              note="این متر فعالیت برنامه‌ریزی‌شده است، نه امتیاز تسلط."
              to="/settings#weekly-goal"
              action="هدف هفتگی"
            />
            <SubjectBalance sessions={sessions} goals={settings.subjectWeeklyGoals} />
          </div>
          {view.openMistakes.length > 0 ? (
            <p>
              {view.openMistakes.length} اشتباه مرورنشده ·{' '}
              <Link className="text-link" to="/mistakes">مرور</Link>
            </p>
          ) : null}
        </PillarSection>

        <PillarSection
          pillar="achieve"
          eyebrow="ACHIEVE · دستاورد"
          title="چه چیزی به دست آورده‌ای؟"
          action={<Link className="button button-secondary" to="/missions">مأموریت‌ها</Link>}
        >
          {profileState === 'ready' && profile ? (
            <div className="learner-home__achieve">
              <Meter value={levelProgress} max={100} label="پیشرفت سطح" tone="achieve" />
              <p className="learner-home__xp" dir="ltr">
                Lv {profile.level} · {profile.xpTotal} XP · {profile.coins} سکه
              </p>
              {dailyMissions?.items?.length ? (
                <div className="learner-home__missions-strip">
                  <p className="learner-home__muted">مأموریت‌های امروز</p>
                  <ul>
                    {dailyMissions.items.slice(0, 3).map((m) => (
                      <li key={m.id}>
                        <div className="learner-home__mission-row">
                          <span>{m.title}</span>
                          <StatusChip status={m.status} />
                        </div>
                        <Meter value={missionProgressRatio(m)} max={100} label={m.title} tone="achieve" />
                      </li>
                    ))}
                  </ul>
                </div>
              ) : (
                <EmptyState
                  pillar="achieve"
                  title="مأموریت آماده‌ای نیست"
                  description="با مطالعه یا بازبینی، مأموریت‌های امروز پیشنهاد می‌شوند."
                  actionTo="/missions"
                  actionLabel="صفحه مأموریت‌ها"
                />
              )}
              <div className="learner-home__actions">
                <Link className="text-link" to="/progress">پیشرفت و پاداش</Link>
                <Link className="text-link" to="/store">فروشگاه آواتار</Link>
              </div>
            </div>
          ) : (
            <EmptyState
              pillar="achieve"
              title="دستاورد سروری در دسترس نیست"
              description="وقتی پروفایل پیشرفت بارگذاری شود، سطح و XP اینجا می‌آید."
              actionTo="/progress"
              actionLabel="صفحه پیشرفت"
            />
          )}
        </PillarSection>
      </div>
    </div>
  )
}
