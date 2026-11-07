// @ts-nocheck
import { Link } from 'react-router-dom'
import { formatMinutes } from '../../utils/clock'

const JUMPS = [
  ['#hours', 'ساعت'],
  ['#questions', 'تست'],
  ['#tasks', 'کار'],
  ['#goals', 'هدف درس'],
  ['#share', 'سهم درس'],
  ['#scores', 'درصد'],
  ['#topics', 'مبحث'],
  ['#causes', 'علت'],
]

export default function InsightCards({
  streak,
  totalMinutes,
  consistency,
  averageScore,
  questionCount,
  testCount,
  unreviewed,
  taskRate,
  best,
  weakest,
}) {
  const items = [
    { label: 'مطالعه', value: formatMinutes(totalMinutes), hint: 'تا امروز', href: '#hours' },
    { label: 'تست ثبت‌شده', value: questionCount, hint: `${testCount} آزمون`, href: '#questions' },
    { label: 'میانگین درصد', value: averageScore == null ? '—' : `${averageScore}٪`, hint: 'همه آزمون‌ها', href: '#scores' },
    { label: 'روز پیاپی', value: streak, hint: 'روز مطالعه', href: '#hours' },
    {
      label: 'ثبات هفته',
      value: consistency ? `${consistency.rate}٪` : '—',
      hint: consistency ? `${consistency.hit} از ${consistency.span} روز` : 'این هفته',
      href: '#hours',
    },
    { label: 'انجام کارها', value: `${taskRate}٪`, hint: 'از همه کارها', href: '#tasks' },
    { label: 'اشتباه باز', value: unreviewed, hint: 'هنوز مرور نشده', to: '/mistakes' },
    {
      label: 'بهترین بازه',
      value: best?.label || '—',
      hint: best?.detail || 'بعد از چند جلسه مشخص می‌شود',
      href: '#hours',
    },
  ]

  return (
    <section className="kpi-board" aria-label="شاخص‌ها">
      <nav className="analytics-jump" aria-label="پرش به نمودار">
        {JUMPS.map(([href, label]) => (
          <a key={href} href={href}>{label}</a>
        ))}
      </nav>
      <ul className="stat-grid kpi-grid">
        {items.map((item) => (
          <li key={item.label}>
            {item.to ? (
              <Link to={item.to}>
                <Kpi item={item} />
              </Link>
            ) : (
              <a href={item.href}>
                <Kpi item={item} />
              </a>
            )}
          </li>
        ))}
      </ul>
      {weakest ? (
        <p className="kpi-note">
          ضعیف‌ترین مبحث با آزمون: {weakest.subjectName}، {weakest.topic}
          {weakest.percentage == null ? '' : `، ${weakest.percentage}٪`}.
          {' '}
          <a href="#topics">جدول مبحث‌ها</a>
        </p>
      ) : null}
    </section>
  )
}

function Kpi({ item }) {
  return (
    <>
      <span>{item.label}</span>
      <strong>{item.value}</strong>
      <p>{item.hint}</p>
    </>
  )
}
