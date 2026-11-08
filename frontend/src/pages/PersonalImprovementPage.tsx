// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/ui/EmptyState'
import StatusChip from '../components/ui/StatusChip'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './PersonalImprovementPage.css'

export default function PersonalImprovementPage() {
  const { status } = useAuth()
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setError('')
      setData(await request('GET', '/improvement/me'))
    } catch (err) {
      setError(err.message || 'بارگذاری بهبود شخصی ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function rebaseline() {
    if (!window.confirm('معیار پایه با رضایت تو بازنشانی می‌شود. ادامه؟')) return
    setBusy(true)
    try {
      await request('POST', '/improvement/rebaseline', { consent: true })
      await load()
    } catch (err) {
      setError(err.message || 'بازنشانی ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  if (!data && !error) return <p className="page-fallback">در حال بارگذاری بهبود شخصی…</p>

  const composite = data?.composite
  const sparkline = data?.sparkline || []
  const ready = composite?.status === 'Ready'
  const stagnating = ready && Math.abs(Number(composite?.improvementPercent) || 0) < 0.5
  const maxAbs = Math.max(
    0.01,
    ...sparkline.map((p) => Number(p.absoluteScore) || 0),
    Number(composite?.baselineScore) || 0,
    Number(composite?.absoluteScore) || 0,
  )

  return (
    <div className="page improve-page">
      <header className="page-header improve-page__header">
        <p className="improve-page__en">IMPROVE · Personal</p>
        <h1>بهبود شخصی</h1>
        <p>تغییر نسبت به معیار خودت — نه رقابت اجباری. شواهد: تسلط، ارزیابی، بازبینی موفق.</p>
      </header>

      {error ? <p className="improve-page__error">{error}</p> : null}

      {composite?.status === 'Collecting' || composite?.status === 'NoData' ? (
        <EmptyState
          pillar="improve"
          title={composite?.message || 'در حال جمع‌آوری معیار پایه'}
          description={`حداقل ${data?.minEvidenceEvents || 5} شاهد یادگیری در ${data?.coldStartDays || 14} روز اول لازم است. XP و زمان اپ به‌تنهایی شمرده نمی‌شوند.`}
          actionTo="/assessments"
          actionLabel="شروع ارزیابی"
        />
      ) : (
        <section className="improve-page__hero-stats">
          <div>
            <span className="improve-page__label">امتیاز فعلی</span>
            <strong dir="ltr">{(Number(composite?.absoluteScore) * 100).toFixed(0)}%</strong>
          </div>
          <div>
            <span className="improve-page__label">معیار پایه</span>
            <strong dir="ltr">{(Number(composite?.baselineScore) * 100).toFixed(0)}%</strong>
          </div>
          <div>
            <span className="improve-page__label">Δ شخصی</span>
            <strong dir="ltr" className={Number(composite?.improvementPercent) >= 0 ? 'is-up' : 'is-down'}>
              {Number(composite?.improvementPercent) >= 0 ? '+' : ''}
              {Number(composite?.improvementPercent).toFixed(1)}%
            </strong>
          </div>
        </section>
      )}

      {ready ? (
        <section className="improve-page__chart" aria-label="نمودار بهبود شخصی">
          <h2>روند نسبت به معیار پایه</h2>
          {sparkline.length === 0 ? (
            <p className="muted">پس از آماده‌شدن معیار پایه، نقاط هفتگی اینجا می‌آید.</p>
          ) : (
            <>
              <svg className="improve-page__spark" viewBox="0 0 320 140" role="img">
                <title>اسپارک‌لاین بهبود شخصی</title>
                {composite?.baselineScore != null ? (
                  <line
                    className="improve-page__baseline"
                    x1="0"
                    x2="320"
                    y1={100 - (Number(composite.baselineScore) / maxAbs) * 80}
                    y2={100 - (Number(composite.baselineScore) / maxAbs) * 80}
                  />
                ) : null}
                <polyline
                  className="improve-page__line"
                  fill="none"
                  points={sparkline
                    .map((point, index) => {
                      const x = sparkline.length === 1 ? 160 : (index / (sparkline.length - 1)) * 300 + 10
                      const y = 100 - ((Number(point.absoluteScore) || 0) / maxAbs) * 80
                      return `${x},${y}`
                    })
                    .join(' ')}
                />
                {sparkline.map((point, index) => {
                  const x = sparkline.length === 1 ? 160 : (index / (sparkline.length - 1)) * 300 + 10
                  const label = String(point.weekStart || point.date || '').slice(5, 10)
                  return (
                    <text key={`${point.weekStart || index}`} x={x} y={128} textAnchor="middle" className="improve-page__tick">
                      {label || index + 1}
                    </text>
                  )
                })}
              </svg>
              <ul className="improve-page__spark-legend" aria-label="نقاط هفتگی">
                {sparkline.map((point, index) => (
                  <li key={`lg-${point.weekStart || index}`}>
                    <span>{String(point.weekStart || point.date || `W${index + 1}`).slice(0, 10)}</span>
                    <strong dir="ltr">{((Number(point.absoluteScore) || 0) * 100).toFixed(0)}%</strong>
                  </li>
                ))}
              </ul>
            </>
          )}
          <p className="improve-page__policy muted">{data?.sparklineNote || 'نقاط هفتگی ذخیره‌شده؛ تاریخچهٔ کامل تسلط بازسازی نمی‌شود.'}</p>
          <p className="improve-page__policy muted">{data?.scoringPolicy}</p>
        </section>
      ) : null}

      {stagnating ? (
        <p className="improve-page__stagnate">
          Δ تقریباً ثابت است. برای حرکت بعدی:{' '}
          <Link className="text-link" to="/reviews">بازبینی</Link>
          {' · '}
          <Link className="text-link" to="/assessments">ارزیابی</Link>
        </p>
      ) : null}

      {(data?.subjects || []).length > 0 ? (
        <section className="improve-page__subjects">
          <h2>به تفکیک درس</h2>
          <ul>
            {(data.subjects || []).map((row) => (
              <li key={row.subjectKey || 'x'}>
                <strong>{row.subjectKey}</strong>
                <span className="improve-page__subject-meta">
                  <StatusChip status={row.status} />
                  {row.status === 'Ready'
                    ? ` ${Number(row.improvementPercent) >= 0 ? '+' : ''}${Number(row.improvementPercent).toFixed(1)}%`
                    : ` ${row.message || ''}`}
                </span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <div className="improve-page__actions">
        <button type="button" disabled={busy || composite?.status !== 'Ready'} onClick={rebaseline}>
          بازنشانی معیار پایه (با رضایت)
        </button>
        <Link className="text-link" to="/ai-coach">
          گفتگو با مربی
        </Link>
        <Link className="text-link" to="/leaderboards">
          رتبه‌بندی گروهی (اختیاری)
        </Link>
      </div>
    </div>
  )
}
