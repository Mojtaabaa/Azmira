import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { causeColorVar } from '../../data/causes'
import { useChartTheme } from '../../hooks/useChartTheme'
import ChartTip from './ChartTip'

export default function CauseChart({ rows }) {
  const theme = useChartTheme()

  if (!rows.some((row) => row.count > 0)) {
    return <p className="card-copy">اشتباهی ثبت نشده.</p>
  }

  return (
    <div className="chart-box" style={{ height: Math.max(200, rows.length * 52 + 24) }}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={rows} layout="vertical" margin={{ top: 8, right: 12, left: 8, bottom: 0 }}>
          <CartesianGrid horizontal={false} stroke={theme.grid} />
          <XAxis type="number" allowDecimals={false} tick={{ fill: theme.text, fontSize: 12, fillOpacity: 0.9 }} axisLine={false} tickLine={false} />
          <YAxis type="category" dataKey="label" width={88} tick={{ fill: theme.text, fontSize: 13 }} axisLine={false} tickLine={false} />
          <Tooltip content={<ChartTip />} cursor={{ fill: 'var(--accent-soft)' }} />
          <Bar dataKey="count" name="مورد" radius={4} maxBarSize={22}>
            {rows.map((row) => (
              <Cell key={row.id || row.label} fill={`var(${causeColorVar(row.id)})`} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
