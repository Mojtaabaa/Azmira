import { formatMinutes } from '../../utils/clock'

export default function TopicTable({ rows }) {
  if (rows.length === 0) {
    return <p className="card-copy">مبحثی ثبت نشده.</p>
  }

  const weakKey = rows.find((row) => row.percentage != null)?.key

  return (
    <div className="topic-table-wrap">
      <table className="topic-table">
        <thead>
          <tr>
            <th>مبحث</th>
            <th>درس</th>
            <th>مطالعه</th>
            <th>درصد</th>
            <th>آزمون</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key} className={row.key === weakKey ? 'is-weak' : undefined}>
              <td>{row.topic}</td>
              <td>{row.subjectName}</td>
              <td className="num">{formatMinutes(row.minutes)}</td>
              <td className="num">{row.percentage == null ? 'بدون آزمون' : `${row.percentage.toFixed(1)}%`}</td>
              <td className="num">{row.tests}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
