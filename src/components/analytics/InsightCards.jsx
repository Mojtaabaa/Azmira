import { formatMinutes } from '../../utils/clock'

export default function InsightCards({ streak, best, totalMinutes, testCount, questionCount }) {
  return (
    <ul className="stat-grid">
      <li>
        <span>زنجیره</span>
        <strong>{streak} روز</strong>
      </li>
      <li>
        <span>بهترین زمان</span>
        <strong>{best ? best.label : 'نامشخص'}</strong>
        <p>{best ? best.detail : 'با ثبت جلسه، ساعت بهتر معلوم می‌شود.'}</p>
      </li>
      <li>
        <span>مطالعه</span>
        <strong>{formatMinutes(totalMinutes)}</strong>
      </li>
      <li>
        <span>آزمون</span>
        <strong>{testCount}</strong>
      </li>
      <li>
        <span>تست</span>
        <strong>{questionCount}</strong>
        <p>جمع سوال‌های ثبت‌شده.</p>
      </li>
    </ul>
  )
}
