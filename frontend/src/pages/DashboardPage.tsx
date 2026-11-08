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
import Meter from '../components/ui/Meter'
import PillarSection from '../components/ui/PillarSection'
import { useAuth } from '../hooks/useAuth'
import { useCollection } from '../hooks/useCollection'
import { useSettings } from '../hooks/useSettings'
import { studyStreak } from '../utils/analytics'
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

const WEAK_LEVELS = new Set(['NotStarted', 'Learning', 'NeedsReview'])

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
  const [masteryItems, setMasteryItems] = useState(null)
  const [remoteError, setRemoteError] = useState('')
  const [dailyMissions, setDailyMissions] = useState(null)

  useEffect(() => {
    if (status !== 'authed') return undefined
    let alive = true
    setProfileState('loading')
    setRemoteError('')

    Promise.all([
      request('GET', '/gamification/me', undefined, { silent: true }).catch(() => null),
      request('GET', '/store/inventory', undefined, { silent: true }).catch(() => null),
      request('GET', '/reviews/due', undefined, { silent: true }).catch(() => null),
      request('GET', '/mastery', undefined, { silent: true }).catch(() => null),
      request('GET', '/missions/daily', undefined, { silent: true }).catch(() => null),
    ]).then(([me, inv, due, mastery, missions]) => {
      if (!alive) return
      if (me) {
        setProfile(me)
        setProfileState('ready')
      } else {
        setProfile(null)
        setProfileState('empty')
      }
      setLoadout(inv?.loadout || null)
      setInventory(inv?.items || [])
      const dueCount = Array.isArray(due?.items) ? due.items.length : Array.isArray(due) ? due.length : null
      setReviewsDue(dueCount)
      setMasteryItems(Array.isArray(mastery?.items) ? mastery.items : null)
      setDailyMissions(missions)
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
      streakLocal: studyStreak(sessions, today),
      openTasks: todayTasks(tasks, today),
      overdue: overdueCount(tasks, today),
      recent: recentTests(tests),
      todayEvents: eventsOnDay(events, today),
      overdueTasks: tasksForFilter(tasks, 'overdue', today),
    }
  }, [sessions, tests, mistakes, tasks, events, topics, settings])

  const weakMasteryCount = useMemo(() => {
    if (!Array.isArray(masteryItems)) return null
    return masteryItems.filter((item) => WEAK_LEVELS.has(item.level)).length
  }, [masteryItems])

  const masteryEvidenceCount = useMemo(() => {
    if (!Array.isArray(masteryItems)) return null
    return masteryItems.reduce((sum, item) => sum + (Number(item.evidenceCount) || 0), 0)
  }, [masteryItems])

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
    profile && typeof profile.streakCurrent === 'number' ? profile.streakCurrent : view.streakLocal

  const levelProgress = Math.round((profile?.levelProgressRatio || 0) * 100)

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
          eyebrow="PLAN · گام بعدی"
          title={next.title}
          action={
            <Link className="button button-primary" to={next.to}>
              {next.action}
            </Link>
          }
        >
          <p className="card-copy">{next.copy}</p>
        </PillarSection>

        <PillarSection
          pillar="plan"
          eyebrow="PLAN · برنامه امروز"
          title="چه کارهایی برای امروز داری؟"
          action={<Link className="text-link" to="/planner">برنامه‌ریز</Link>}
        >
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
          <div className="learner-home__row">
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
        </PillarSection>

        <PillarSection
          pillar="learn"
          eyebrow="LEARN · یادگیری"
          title="الان روی چه چیزی کار می‌کنی؟"
          action={<Link className="text-link" to="/learn">مرکز یادگیری</Link>}
        >
          <div className="learner-home__stats-line">
            <span>
              مباحث ثبت‌شده: <strong dir="ltr">{topics.length}</strong>
            </span>
            <span>
              پوشش فهرست: <strong dir="ltr">{view.coverage}%</strong>
            </span>
            <span>
              بازبینی سررسید: <strong dir="ltr">{reviewsDue == null ? 'نامشخص' : reviewsDue}</strong>
            </span>
          </div>
          <Meter value={view.coverage} max={100} label="پوشش مباحث" tone="learn" />
          <p className="learner-home__note">
            پوشش مباحث نشان‌دهنده فهرست‌نویسی است، نه لزوماً تسلط. تسلط فقط با شواهد آزمون و بازبینی معنا دارد.
          </p>
          <div className="dash-links">
            <Link className="text-link" to="/topics">مباحث</Link>
            <Link className="text-link" to="/reviews">بازبینی</Link>
            <Link className="text-link" to="/mastery">نقشه تسلط</Link>
            <Link className="text-link" to="/assessments">ارزیابی</Link>
          </div>
          {view.recent?.length ? <RecentTests tests={view.recent} /> : null}
        </PillarSection>

        <PillarSection
          pillar="improve"
          eyebrow="IMPROVE · بهبود شخصی"
          title="نسبت به معیار خودت چطور پیش می‌روی؟"
          action={<Link className="text-link" to="/improve">مرکز بهبود</Link>}
        >
          {masteryEvidenceCount == null ? (
            <EmptyState
              pillar="improve"
              title="در حال دریافت شواهد تسلط"
              description="اگر سرویس در دسترس باشد، خلاصه تسلط اینجا می‌آید. تا آن زمان از آمار محلی هفته استفاده کن."
              actionTo="/analytics"
              actionLabel="آمار یادگیری"
            />
          ) : masteryEvidenceCount < 3 ? (
            <EmptyState
              pillar="improve"
              title="معیار پایه هنوز آماده نیست"
              description="برای سنجش بهبود شخصی، به شواهد یادگیری بیشتری نیاز است (آزمون، ارزیابی، یا بازبینی). رتبه ساختگی نمایش داده نمی‌شود."
              actionTo="/assessments"
              actionLabel="شروع ارزیابی"
            />
          ) : (
            <div className="learner-home__improve-ready">
              <p>
                <strong dir="ltr">{weakMasteryCount}</strong> مبحث در وضعیت نیازمند تقویت ·{' '}
                <strong dir="ltr">{masteryEvidenceCount}</strong> شاهد ثبت‌شده
              </p>
              <p className="learner-home__note">
                نمودار بهبود شخصی نسبت به معیار پایه در مرکز بهبود آماده است. مقایسه گروهی اختیاری و پیش‌فرض خصوصی است.
              </p>
              <Link className="button button-secondary" to="/improvement">
                دیدن بهبود شخصی
              </Link>
            </div>
          )}
          <div className="learner-home__row">
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
          action={<Link className="text-link" to="/achieve">مرکز دستاورد</Link>}
        >
          {profileState === 'ready' && profile ? (
            <div className="learner-home__achieve">
              <div>
                <p className="learner-home__muted">سطح و XP</p>
                <p className="learner-home__xp" dir="ltr">
                  Lv {profile.level} · {profile.xpTotal} XP · {profile.coins} سکه
                </p>
                <Meter value={levelProgress} max={100} label="پیشرفت سطح" tone="achieve" />
              </div>
              {dailyMissions?.items?.length ? (
                <div className="learner-home__missions-strip">
                  <p className="learner-home__muted">مأموریت‌های امروز</p>
                  <ul>
                    {dailyMissions.items.slice(0, 3).map((m) => (
                      <li key={m.id}>
                        <span>{m.title}</span>
                        <span dir="ltr">{m.status}</span>
                      </li>
                    ))}
                  </ul>
                  <Link className="text-link" to="/missions">همه مأموریت‌ها</Link>
                </div>
              ) : (
                <p className="learner-home__note">
                  مأموریت روزانه هنوز بارگذاری نشده یا خالی است.{' '}
                  <Link className="text-link" to="/missions">مشاهده مأموریت‌ها</Link>
                </p>
              )}
              <div className="dash-links">
                <Link className="text-link" to="/missions">مأموریت‌های روزانه</Link>
                <Link className="text-link" to="/progress">پیشرفت و پاداش</Link>
                <Link className="text-link" to="/store">آواتار و فروشگاه</Link>
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
