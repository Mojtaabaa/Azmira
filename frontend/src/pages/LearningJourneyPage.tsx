// @ts-nocheck
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/ui/EmptyState'
import KpiStat from '../components/ui/KpiStat'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './LearningJourneyPage.css'

export default function LearningJourneyPage() {
  const { status } = useAuth()
  const [data, setData] = useState(null)
  const [selected, setSelected] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const evaluatedOnce = useRef(false)

  const load = useCallback(async () => {
    setError('')
    try {
      setData(await request('GET', '/journey/me'))
    } catch (err) {
      setError(err?.message || 'خطا')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function evaluate() {
    setBusy(true)
    setError('')
    try {
      const body = await request('POST', '/journey/evaluate-unlock', {})
      setData(body.map)
    } catch (err) {
      setError(err?.message || 'خطا')
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    if (!data?.regions?.length || evaluatedOnce.current || status !== 'authed') return undefined
    evaluatedOnce.current = true
    void evaluate()
    return undefined
    // one-shot after first successful map load
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data?.regions?.length, status])

  const stats = useMemo(() => {
    const stages = (data?.regions || []).flatMap((r) => r.stages || [])
    const total = stages.length
    const complete = stages.filter((s) => s.state === 'complete').length
    const available = stages.filter((s) => s.state === 'available').length
    const next = stages.find((s) => s.state === 'available') || null
    const pct = total ? Math.round((complete / total) * 100) : 0
    return { total, complete, available, next, pct }
  }, [data])

  if (!data && !error) {
    return <p className="page-fallback">در حال بارگذاری نقشه سفر…</p>
  }

  return (
    <div className="page journey-page">
      <header className="page-header journey-page__header">
        <p className="journey-page__en">LEARN · Journey</p>
        <h1>{data?.title || 'سفر یادگیری'}</h1>
        <p>
          مسیر منطقه‌ای با ایستگاه‌هایی که فقط با شواهد یادگیری باز می‌شوند — نه با XP یا دقیقه مطالعهٔ خام.
        </p>
        {data?.regions?.length ? (
          <div className="kpi-stat-row">
            <KpiStat label="پیشرفت نقشه" value={`${stats.pct}%`} />
            <KpiStat label="کامل" value={`${stats.complete}/${stats.total}`} />
            <KpiStat label="بازِ بعدی" value={stats.next?.title || '—'} dir="rtl" />
          </div>
        ) : null}
        <div className="journey-page__actions">
          <button type="button" className="btn primary" disabled={busy} onClick={evaluate}>
            {busy ? 'در حال ارزیابی…' : 'ارزیابی شواهد و آنلاک'}
          </button>
          {stats.next ? (
            <button type="button" className="btn ghost" onClick={() => setSelected(stats.next)}>
              ایستگاه بعدی
            </button>
          ) : null}
        </div>
        {data?.unlockPolicy ? <p className="journey-page__policy">{data.unlockPolicy}</p> : null}
      </header>

      {error ? <p className="form-error">{error}</p> : null}

      {!data?.regions?.length ? (
        <EmptyState
          pillar="learn"
          title="هنوز نقشه‌ای ساخته نشده"
          description="با ثبت تسلط یا کارت مرور، سفر از موضوعات واقعی‌ات bootstrap می‌شود."
          actionTo="/assessments"
          actionLabel="شروع ارزیابی"
        />
      ) : (
        <div className="journey-map" role="list">
          {data.regions.map((region) => (
            <section key={region.id} className={`journey-region journey-region--${region.visualThemeKey}`} role="listitem">
              <header className="journey-region__head">
                <h2>{region.title}</h2>
                <span>{region.subjectKey}</span>
              </header>
              <ol className="journey-path">
                {region.stages.map((stage, index) => (
                  <li
                    key={stage.id}
                    className={`journey-node journey-node--${stage.state}${stats.next?.id === stage.id ? ' is-next' : ''}`}
                  >
                    {index > 0 ? <span className="journey-node__link" aria-hidden="true" /> : null}
                    <button
                      type="button"
                      className="journey-node__btn"
                      onClick={() => setSelected(stage)}
                      aria-pressed={selected?.id === stage.id}
                    >
                      <span className="journey-node__dot" />
                      <strong>{stage.title}</strong>
                      <em>{labelState(stage.state)}</em>
                    </button>
                  </li>
                ))}
              </ol>
            </section>
          ))}
        </div>
      )}

      {selected ? (
        <aside className="journey-drawer" aria-label="کشوی شواهد">
          <header>
            <h3>{selected.title}</h3>
            <button type="button" className="btn ghost" onClick={() => setSelected(null)}>
              بستن
            </button>
          </header>
          <p>{selected.narrativeBlurb}</p>
          <h4>چک‌لیست آنلاک</h4>
          {selected.state === 'locked' ? (
            <ul className="journey-checklist">
              <li>شواهد تسلط یا ارزیابی مرتبط ثبت کن</li>
              <li>بازبینی موفق برای کارت‌های مرتبط انجام بده</li>
              <li>
                سپس <button type="button" className="text-link-btn" disabled={busy} onClick={evaluate}>ارزیابی شواهد</button> را بزن
              </li>
              <li>
                یا مستقیم برو به <Link to="/reviews">بازبینی</Link> / <Link to="/assessments">ارزیابی</Link>
              </li>
            </ul>
          ) : (
            <p className="muted">این ایستگاه باز یا کامل است.</p>
          )}
          <h4>چرا باز شد؟</h4>
          {selected.evidence?.length ? (
            <ul className="journey-evidence">
              {selected.evidence.map((ev) => (
                <li key={`${ev.type}-${ev.refId}`}>
                  <strong>{ev.type}</strong>
                  <span>{ev.detail}</span>
                  <code>{ev.refId.slice(0, 8)}</code>
                </li>
              ))}
            </ul>
          ) : (
            <p className="muted">
              {selected.state === 'locked'
                ? 'هنوز شواهد کافی ثبت نشده. تسلط، آزمون یا مرور موفق لازم است.'
                : 'شواهدی برای این ایستگاه ذخیره نشده.'}
            </p>
          )}
          {selected.rewards?.cosmeticKey ? (
            <p className="journey-cosmetic">
              هوک ظاهری دستاورد: <code>{selected.rewards.cosmeticKey}</code>
            </p>
          ) : null}
        </aside>
      ) : null}
    </div>
  )
}

function labelState(state) {
  if (state === 'complete') return 'تمام'
  if (state === 'available') return 'باز'
  return 'قفل'
}
