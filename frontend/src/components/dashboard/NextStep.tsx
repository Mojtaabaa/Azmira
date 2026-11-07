// @ts-nocheck
import { Link } from 'react-router-dom'
import { formatMinutes } from '../../utils/clock'

export default function NextStep({ overdue, openMistakes, studied, goal, openTasks }) {
  let title = 'برنامه هفته را مرتب کن'
  let copy = 'کار عقب‌افتاده و اشتباه باز نداری. اگر هدف امروز پر شده، برنامه روزهای بعد را ببین.'
  let to = '/planner'
  let action = 'رفتن به برنامه'

  if (overdue > 0) {
    title = `${overdue} کار از موعد گذشته`
    copy = 'اول همان‌ها را ببند تا فهرست امروز شلوغ نماند.'
    to = '/tasks?filter=overdue'
    action = 'دیدن کارها'
  } else if (openMistakes > 0) {
    title = `${openMistakes} اشتباه هنوز مرور نشده`
    copy = 'مرور اشتباه، بیشتر از یک آزمون تازه به درصد کمک می‌کند.'
    to = '/mistakes'
    action = 'مرور اشتباهات'
  } else if (goal > 0 && studied < goal) {
    title = `${formatMinutes(goal - studied)} تا هدف امروز مانده`
    copy = 'یک نوبت کوتاه هم کافی است. درس و مبحث را در زمان‌سنج انتخاب کن.'
    to = '/timer'
    action = 'شروع زمان‌سنج'
  } else if (openTasks > 0) {
    title = `${openTasks} کار برای امروز`
    copy = 'هدف زمان پر شده. اگر انرژی ماند، کار همین امروز را انجام بده.'
    to = '/tasks'
    action = 'فهرست کارها'
  }

  return (
    <section className="card next-step">
      <p className="eyebrow">گام بعدی</p>
      <h2>{title}</h2>
      <p className="card-copy">{copy}</p>
      <Link className="button button-primary" to={to}>{action}</Link>
    </section>
  )
}
