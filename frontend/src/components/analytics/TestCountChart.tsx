// @ts-nocheck
import { Bar, CartesianGrid, ComposedChart, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useChartTheme } from '../../hooks/useChartTheme'
import ChartTip from './ChartTip'

export default function TestCountChart({ rows }) {
  const theme = useChartTheme()

  if (rows.length === 0) {
    return <p className="card-copy">تستی ثبت نشده.</p>
  }

  const weekly = rows.some((row) => String(row.axis).includes('تا'))
  const interval = rows.length > 8 ? Math.ceil(rows.length / 6) - 1 : 0

  return (
    <div className="chart-box">
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart data={rows} margin={{ top: 8, right: 0, left: 8, bottom: weekly ? 24 : 0 }}>
          <CartesianGrid stroke={theme.grid} />
          <XAxis
            reversed
            dataKey="axis"
            interval={interval}
            tick={{ fill: theme.text, fontSize: weekly ? 11 : 12, fillOpacity: 0.65 }}
            angle={weekly ? -28 : 0}
            textAnchor={weekly ? 'end' : 'middle'}
            height={weekly ? 74 : 30}
            axisLine={false}
            tickLine={false}
          />
          <YAxis
            orientation="right"
            tick={{ fill: theme.text, fontSize: 12, fillOpacity: 0.65 }}
            axisLine={false}
            tickLine={false}
            width={40}
            allowDecimals={false}
          />
          <Tooltip content={<ChartTip />} />
          <Bar
            dataKey="count"
            name="تست"
            fill={theme.fill}
            fillOpacity={0.28}
            radius={[4, 4, 0, 0]}
            maxBarSize={36}
            isAnimationActive={false}
          />
          <Line
            type="monotone"
            dataKey="count"
            name="تست"
            stroke={theme.fill}
            strokeWidth={2.4}
            dot={{ r: 4, fill: theme.fill, stroke: theme.tipBg, strokeWidth: 2 }}
            activeDot={{ r: 6 }}
            legendType="none"
          />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  )
}
