import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useChartTheme } from '../../hooks/useChartTheme'
import { SUBJECTS } from '../../data/subjects'
import ChartTip from './ChartTip'

const DASH = {
  math: undefined,
  physics: '7 4',
  chemistry: '2 3',
  geometry: '5 3',
  literature: '12 4 2 4',
}

const FALLBACK = ['#1a3a6d', '#fdb827', '#3d7ea6', '#c4782a', '#5b6b8c']

export default function ScoreTrendChart({ series }) {
  const theme = useChartTheme()
  const colors = Object.fromEntries(SUBJECTS.map((subject) => [subject.id, subject.color]))

  if (!series.rows.length) {
    return <p className="card-copy">آزمونی ثبت نشده.</p>
  }

  const interval = series.rows.length > 8 ? Math.ceil(series.rows.length / 6) - 1 : 0

  return (
    <div className="chart-box chart-box-tall">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={series.rows} margin={{ top: 8, right: 0, left: 8, bottom: 0 }}>
          <CartesianGrid stroke={theme.grid} />
          <XAxis reversed dataKey="label" interval={interval} tick={{ fill: theme.text, fontSize: 12, fillOpacity: 0.65 }} axisLine={false} tickLine={false} />
          <YAxis orientation="right" tick={{ fill: theme.text, fontSize: 12, fillOpacity: 0.65 }} axisLine={false} tickLine={false} width={36} />
          <Tooltip content={<ChartTip />} />
          <Legend wrapperStyle={{ color: theme.text }} />
          {series.subjects.map((subject, index) => {
            const stroke = colors[subject.id] || FALLBACK[index % FALLBACK.length]
            return (
              <Line
                key={subject.id}
                type="monotone"
                dataKey={subject.id}
                name={subject.name}
                stroke={stroke}
                strokeDasharray={DASH[subject.id] || ['4 3', '1 4', '9 3 2 3', '6 2'][index % 4]}
                strokeWidth={2}
                dot={{ r: 3, fill: stroke, stroke: theme.tipBg }}
                connectNulls={false}
              />
            )
          })}
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
