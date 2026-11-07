import { formatMinutes } from '../../utils/clock'

export default function InsightCards({
  streak,
  totalMinutes,
  consistency,
  averageScore,
}) {
  return (
    <ul className="stat-grid">
      <li>
        <span>مطالعه</span>
        <strong>{formatMinutes(totalMinutes)}</strong>
      </li>
      <li>
        <span>روز پیاپی</span>
        <strong>{streak} روز</strong>
      </li>
      <li>
        <span>این هفته</span>
        <strong>
          {consistency ? `${consistency.hit} از ${consistency.span}` : '—'}
        </strong>
        <p>روزهایی که مطالعه کردی.</p>
      </li>
      <li>
        <span>میانگین درصد</span>
        <strong>{averageScore == null ? '—' : `${averageScore}%`}</strong>
      </li>
    </ul>
  )
}
