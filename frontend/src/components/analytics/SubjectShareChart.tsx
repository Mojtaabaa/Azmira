// @ts-nocheck
import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { SUBJECT_COLORS, getSubjectById } from '../../data/subjects'
import { useChartTheme } from '../../hooks/useChartTheme'
import ChartTip from './ChartTip'

export default function SubjectShareChart({ rows }) {
  const theme = useChartTheme()

  if (rows.length === 0) {
    return <p className="card-copy">مطالعه‌ای ثبت نشده.</p>
  }

  return (
    <div className="chart-box" style={{ height: Math.max(200, rows.length * 52 + 24) }}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={rows} layout="vertical" margin={{ top: 8, right: 12, left: 8, bottom: 0 }}>
          <CartesianGrid horizontal={false} stroke={theme.grid} />
          <XAxis type="number" tick={{ fill: theme.text, fontSize: 12, fillOpacity: 0.65 }} axisLine={false} tickLine={false} />
          <YAxis type="category" dataKey="label" width={72} tick={{ fill: theme.text, fontSize: 13 }} axisLine={false} tickLine={false} />
          <Tooltip content={<ChartTip />} cursor={{ fill: 'var(--accent-soft)' }} />
          <Bar dataKey="hours" name="ساعت" radius={4} maxBarSize={22}>
            {rows.map((row) => (
              <Cell
                key={row.id || row.label}
                fill={getSubjectById(row.id)?.color || SUBJECT_COLORS.default}
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
