// @ts-nocheck
import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import CountdownCard from '../components/dashboard/CountdownCard'
import DailyBrief from '../components/dashboard/DailyBrief'
import GoalMeter from '../components/dashboard/GoalMeter'
import NextStep from '../components/dashboard/NextStep'
import RankCard from '../components/dashboard/RankCard'
import RecentTests from '../components/dashboard/RecentTests'
import ProgressWidget from '../components/dashboard/ProgressWidget'
import StreakCard from '../components/dashboard/StreakCard'
import SubjectBalance from '../components/dashboard/SubjectBalance'
import TodayLists from '../components/dashboard/TodayLists'
import { useAuth } from '../hooks/useAuth'
import { useCollection } from '../hooks/useCollection'
import { useSettings } from '../hooks/useSettings'
import { studyStreak } from '../utils/analytics'
import { formatHourCount, formatMinutes, todayInputValue } from '../utils/clock'
import {
  overdueCount,
  rankProjection,
  recentTests,
  todayStudyMinutes,
  todayTasks,
  unreviewedMistakes,
  weekReview,
} from '../utils/dashboard'
import { tasksForFilter } from '../utils/tasks'
import { eventsOnDay } from '../utils/events'
import { countdownToKonkoor } from '../utils/konkoor'
import { coverageOf } from '../utils/topics'
import './DashboardPage.css'

function greetingForHour(date = new Date()) {
  const hour = date.getHours()
  if (hour < 12) return 'صبح بخیر'
  if (hour < 18) return 'روز بخیر'
  return 'عصر بخیر'
}

export default function DashboardPage() {
  const { user } = useAuth()
  const settings = useSettings()
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const tasks = useCollection('tasks')
  const events = useCollection('events')
  const topics = useCollection('topics')
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
      streak: studyStreak(sessions, today),
      openTasks: todayTasks(tasks, today),
      overdue: overdueCount(tasks, today),
      projection: rankProjection(tests, settings.subjectWeights),
      recent: recentTests(tests),
      todayEvents: eventsOnDay(events, today),
      overdueTasks: tasksForFilter(tasks, 'overdue', today),
    }
  }, [sessions, tests, mistakes, tasks, events, topics, settings])
  const {
    today,
    goal,
    weekGoal,
    studied,
    week,
    openMistakes,
    countdown,
    coverage,
    streak,
    openTasks,
    overdue,
  } = view
  const habits = [
    { ok: goal > 0 && studied >= goal, label: 'هدف امروز' },
    { ok: overdue === 0, label: 'کارها سر وقت' },
    { ok: openMistakes.length === 0, label: 'مرور تمام' },
  ]
  const habitDone = habits.filter((habit) => habit.ok).length
  const dailyPace = Math.max(15, Math.round(weekGoal / 7 / 15) * 15)

  return (
    <div className="page is-wide">
      <header className="page-header dash-welcome">
        <div>
          <p className="dash-hello">{greetingForHour()}، {user.username}</p>
          <h1>خانه</h1>
          <p>
            {countdown.started
              ? 'روز کنکور رسیده است.'
              : `${countdown.days} روز تا کنکور. آهنگ پیشنهادی امروز ${formatMinutes(dailyPace)} است.`}
          </p>
        </div>
        <ul className="dash-quick-stats" aria-label="خلاصه سریع">
          <li>
            <span>امروز</span>
            <strong>{formatMinutes(studied)}</strong>
          </li>
          <li>
            <span>روز پیاپی</span>
            <strong dir="ltr">{streak}</strong>
          </li>
          <li>
            <span>کار مانده</span>
            <strong dir="ltr">{openTasks.length + overdue}</strong>
          </li>
        </ul>
        <ol className="habit-row" aria-label="سه عادت امروز">
          {habits.map((habit) => (
            <li key={habit.label} className={habit.ok ? 'is-done' : ''}>
              <span aria-hidden="true">{habit.ok ? '✓' : '○'}</span>
              {habit.label}
            </li>
          ))}
          <li className="habit-score" dir="ltr">{habitDone}/3</li>
        </ol>
      </header>
      <div className="stack">
        <ProgressWidget />
        <div className="dash-grid dash-grid-2">
          <NextStep
            overdue={overdue}
            openMistakes={openMistakes.length}
            studied={studied}
            goal={goal}
            openTasks={openTasks.length}
          />
          <SubjectBalance sessions={sessions} goals={settings.subjectWeeklyGoals} />
        </div>
        <DailyBrief
          overdueTasks={view.overdueTasks}
          studied={studied}
          goal={goal}
        />
        <div className="dash-grid">
          <GoalMeter
            title="مطالعه امروز"
            doneMinutes={studied}
            planMinutes={goal}
            planLabel={`از هدف ${formatHourCount(goal)} ساعته`}
            note={studied > goal ? 'از هدف امروز رد شدی.' : ''}
            to="/timer"
            action="زمان‌سنج"
          />
          <CountdownCard countdown={countdown} />
          <StreakCard streak={streak} weekDaysHit={week.daysHit} weekDays={7} />
        </div>
        <div className="dash-grid dash-grid-2">
          <GoalMeter
            title="مطالعه این هفته"
            doneMinutes={week.studied}
            planMinutes={weekGoal}
            planLabel={`از هدف ${formatHourCount(weekGoal)} ساعته`}
            note={week.studied > weekGoal ? 'از هدف این هفته رد شدی.' : ''}
            to="/settings#weekly-goal"
            action="هدف هفتگی"
          />
          <section className="card mistake-count">
            <h2>اشتباهات مرورنشده</h2>
            <p className="countdown-figure" dir="ltr">{openMistakes.length}</p>
            <p className="card-copy">هنوز مرور نشده‌اند.</p>
            <Link className="text-link" to="/mistakes">مرور اشتباهات</Link>
          </section>
        </div>
        <RankCard projection={view.projection} />
        <TodayLists
          tasks={openTasks}
          events={view.todayEvents}
          overdue={overdue}
        />
        <RecentTests tests={view.recent} />
        <section className="card">
          <h2>پیشرفت مباحث</h2>
          <p className="goal-figures">
            <strong dir="ltr">{coverage}%</strong>
            <span>{topics.length} مبحث ثبت‌شده</span>
          </p>
          <div className="meter" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={coverage} aria-label="پیشرفت مباحث">
            <span style={{ width: `${coverage}%` }} />
          </div>
          <div className="dash-links">
            <Link className="text-link" to="/topics">فهرست مباحث</Link>
            <Link className="text-link" to="/settings#daily-goal">هدف روزانه</Link>
          </div>
        </section>
      </div>
    </div>
  )
}
