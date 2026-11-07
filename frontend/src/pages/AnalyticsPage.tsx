// @ts-nocheck
import { useMemo, useState } from 'react'
import CauseChart from '../components/analytics/CauseChart'
import GrainToggle from '../components/analytics/GrainToggle'
import InsightCards from '../components/analytics/InsightCards'
import ScoreTrendChart from '../components/analytics/ScoreTrendChart'
import StudyHoursChart from '../components/analytics/StudyHoursChart'
import SubjectGoalsChart from '../components/analytics/SubjectGoalsChart'
import SubjectShareChart from '../components/analytics/SubjectShareChart'
import TaskCountChart from '../components/analytics/TaskCountChart'
import TestCountChart from '../components/analytics/TestCountChart'
import TopicTable from '../components/analytics/TopicTable'
import { useCollection } from '../hooks/useCollection'
import { useSettings } from '../hooks/useSettings'
import { buildAnalytics } from '../utils/analytics'
import { todayInputValue } from '../utils/clock'
import { hasSampleData, removeSampleAnalytics } from '../utils/sampleAnalytics'
import { buildWeeklyReport, downloadWeeklyReportPng } from '../utils/weeklyReport'
import './AnalyticsPage.css'

export default function AnalyticsPage() {
  const settings = useSettings()
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const tasks = useCollection('tasks')
  const [grain, setGrain] = useState('week')
  const [questionGrain, setQuestionGrain] = useState('week')
  const [shareGrain, setShareGrain] = useState('week')
  const [taskGrain, setTaskGrain] = useState('week')
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState('')
  const today = todayInputValue()
  const report = useMemo(() => buildAnalytics({
    sessions,
    tests,
    mistakes,
    tasks,
    goals: settings.subjectWeeklyGoals,
    grain,
    questionGrain,
    shareGrain,
    taskGrain,
    today,
  }), [sessions, tests, mistakes, tasks, settings.subjectWeeklyGoals, grain, questionGrain, shareGrain, taskGrain, today])
  const weakest = report.topics.find((row) => row.percentage != null) || null
  const samples = hasSampleData(sessions, tests, mistakes)

  async function exportWeek() {
    setExporting(true)
    setExportError('')
    try {
      const data = buildWeeklyReport({ sessions, tests, mistakes, today })
      await downloadWeeklyReportPng(data)
    } catch {
      setExportError('گزارش ساخته نشد. دوباره تلاش کن.')
    } finally {
      setExporting(false)
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>آمار</h1>
        <p>
          ساعت مطالعه، تست، کارها و هدف درس‌ها را جدا با روز، هفته یا ماه ببین. اگر «روز» را بزنی، هفت روز همین هفته را از شنبه تا جمعه می‌بینی.
        </p>
      </header>
      <div className="stack analytics-stack">
        {samples ? (
          <div className="card">
            <p className="card-copy">چند جلسه، آزمون و اشتباه نمونه اضافه شده تا شکل نمودارها دیده شود. داده‌های خودت دست‌نخورده مانده.</p>
            <div className="button-row">
              <button type="button" className="button button-danger" onClick={removeSampleAnalytics}>
                حذف داده‌های نمونه
              </button>
            </div>
          </div>
        ) : null}
        <InsightCards
          streak={report.streak}
          totalMinutes={report.totalMinutes}
          consistency={report.consistency}
          averageScore={report.averageScore}
          questionCount={report.questionCount}
          testCount={report.testCount}
          unreviewed={report.unreviewedMistakes}
          taskRate={report.taskSummary.rate}
          best={report.best}
          weakest={weakest}
        />

        <section className="card">
          <div className="analytics-head">
            <div>
              <h2>گزارش هفته</h2>
              <p className="card-copy analytics-head-copy">
                یک تصویر از ساعت مطالعه، روزهای پیاپی، آزمون‌ها و سهم درس‌های همین هفته بساز و ذخیره یا اشتراک کن.
              </p>
            </div>
            <button type="button" className="button button-primary" disabled={exporting} onClick={exportWeek}>
              {exporting ? 'در حال ساخت…' : 'دانلود تصویر گزارش'}
            </button>
          </div>
          {exportError ? <p className="form-message is-error" role="alert">{exportError}</p> : null}
        </section>

        <section className="card" id="hours">
          <div className="analytics-head">
            <h2>ساعت مطالعه</h2>
            <GrainToggle grain={grain} onChange={setGrain} />
          </div>
          <StudyHoursChart rows={report.hours} />
        </section>

        <section className="card" id="questions">
          <div className="analytics-head">
            <div>
              <h2>تعداد تست</h2>
              <p className="card-copy analytics-head-copy">
                مجموع تست‌های ثبت‌شده در آزمون‌ها.
              </p>
            </div>
            <GrainToggle grain={questionGrain} onChange={setQuestionGrain} />
          </div>
          <TestCountChart rows={report.questions} />
          {report.questionSubjects.length > 0 ? (
            <ul className="count-list">
              {report.questionSubjects.map((row) => (
                <li key={row.id}>
                  <span>{row.label}</span>
                  <strong>{row.count}</strong>
                </li>
              ))}
            </ul>
          ) : null}
        </section>

        <section className="card" id="tasks">
          <div className="analytics-head">
            <div>
              <h2>کارهای انجام‌شده</h2>
              <p className="card-copy analytics-head-copy">
                تعداد کارهایی که در هر بازه تیک زده‌ای.
              </p>
            </div>
            <GrainToggle grain={taskGrain} onChange={setTaskGrain} />
          </div>
          <ul className="task-summary" aria-label="خلاصه کارها">
            <li>
              <span>انجام‌شده</span>
              <strong>{report.taskSummary.done}</strong>
            </li>
            <li>
              <span>مانده</span>
              <strong>{report.taskSummary.open}</strong>
            </li>
            <li>
              <span>عقب‌افتاده</span>
              <strong>{report.taskSummary.overdue}</strong>
            </li>
            <li>
              <span>نرخ انجام</span>
              <strong>{report.taskSummary.rate}٪</strong>
            </li>
          </ul>
          <TaskCountChart rows={report.tasks} />
        </section>

        <section className="card" id="goals">
          <div className="analytics-head">
            <div>
              <h2>هدف هفتگی درس‌ها</h2>
              <p className="card-copy analytics-head-copy">
                پیشرفت هر درس نسبت به هدفی که در تنظیمات گذاشته‌ای.
              </p>
            </div>
          </div>
          <SubjectGoalsChart rows={report.subjectGoals} />
        </section>

        <section className="card" id="share">
          <div className="analytics-head">
            <div>
              <h2>سهم هر درس</h2>
              <p className="card-copy analytics-head-copy">
                سهم ساعت مطالعه بین درس‌ها در بازه انتخابی (امروز، همین هفته، یا همین ماه).
              </p>
            </div>
            <GrainToggle grain={shareGrain} onChange={setShareGrain} />
          </div>
          <SubjectShareChart rows={report.subjects} />
        </section>
        <section className="card" id="scores">
          <h2>درصد آزمون</h2>
          <p className="card-copy">میانگین درصد کنکور هر درس در روزهایی که آزمون داده‌ای.</p>
          <ScoreTrendChart series={report.scores} />
        </section>
        <section className="card" id="topics">
          <h2>مبحث‌ها</h2>
          <p className="card-copy">ضعیف‌ترین‌ها بالاترند و روشن‌تر مشخص شده‌اند.</p>
          <TopicTable rows={report.topics} />
        </section>
        <section className="card" id="causes">
          <h2>علت اشتباهات</h2>
          <CauseChart rows={report.causes} />
        </section>
      </div>
    </div>
  )
}
