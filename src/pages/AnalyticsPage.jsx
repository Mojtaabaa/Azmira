import { useState } from 'react'
import CauseChart from '../components/analytics/CauseChart'
import GrainToggle from '../components/analytics/GrainToggle'
import InsightCards from '../components/analytics/InsightCards'
import ScoreTrendChart from '../components/analytics/ScoreTrendChart'
import StudyHoursChart from '../components/analytics/StudyHoursChart'
import SubjectShareChart from '../components/analytics/SubjectShareChart'
import TestCountChart from '../components/analytics/TestCountChart'
import TopicTable from '../components/analytics/TopicTable'
import { useCollection } from '../hooks/useCollection'
import { buildAnalytics } from '../utils/analytics'
import { todayInputValue } from '../utils/clock'
import { hasSampleData, removeSampleAnalytics } from '../utils/sampleAnalytics'
import './AnalyticsPage.css'

export default function AnalyticsPage() {
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const [grain, setGrain] = useState('week')
  const [questionGrain, setQuestionGrain] = useState('week')
  const today = todayInputValue()
  const report = buildAnalytics({ sessions, tests, mistakes, grain, questionGrain, today })
  const samples = hasSampleData(sessions, tests, mistakes)

  return (
    <div className="page">
      <header className="page-header">
        <h1>آمار</h1>
        <p>ساعت مطالعه و تعداد تست را جدا با روز، هفته یا ماه ببین. نمای روز هفت روز همین هفته است، از شنبه تا جمعه.</p>
      </header>
      <div className="stack">
        {samples ? (
          <div className="card">
            <p className="card-copy">چند جلسه، آزمون و اشتباه نمونه اضافه شده تا شکل نمودارها دیده شود. داده‌های خودت سر جایش مانده.</p>
            <div className="button-row">
              <button type="button" className="button button-danger" onClick={removeSampleAnalytics}>
                حذف داده‌های نمونه
              </button>
            </div>
          </div>
        ) : null}
        <InsightCards
          streak={report.streak}
          best={report.best}
          totalMinutes={report.totalMinutes}
          testCount={report.testCount}
          questionCount={report.questionCount}
        />
        <section className="card">
          <div className="analytics-head">
            <h2>تعداد تست</h2>
            <GrainToggle grain={questionGrain} onChange={setQuestionGrain} />
          </div>
          <p className="card-copy">جمع سوال‌های هر آزمون. هر آزمون به اندازه تعداد تست‌هایش حساب می‌شود.</p>
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
        <section className="card">
          <div className="analytics-head">
            <h2>ساعت مطالعه</h2>
            <GrainToggle grain={grain} onChange={setGrain} />
          </div>
          <StudyHoursChart rows={report.hours} />
        </section>
        <section className="card">
          <h2>سهم هر درس</h2>
          <SubjectShareChart rows={report.subjects} />
        </section>
        <section className="card">
          <h2>درصد آزمون</h2>
          <p className="card-copy">میانگین درصد کنکور هر درس در روزهایی که آزمون داده‌ای.</p>
          <ScoreTrendChart series={report.scores} />
        </section>
        <section className="card">
          <h2>مبحث‌ها</h2>
          <p className="card-copy">ضعیف‌ترین درصد بالا است و با زمینه سفید مشخص شده.</p>
          <TopicTable rows={report.topics} />
        </section>
        <section className="card">
          <h2>علت اشتباه</h2>
          <CauseChart rows={report.causes} />
        </section>
      </div>
    </div>
  )
}
