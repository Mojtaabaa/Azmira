// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/ui/EmptyState'
import Meter from '../components/ui/Meter'
import PillarSection from '../components/ui/PillarSection'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './DailyMissionsPage.css'

export default function DailyMissionsPage() {
  const { status } = useAuth()
  const [payload, setPayload] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setError('')
      setPayload(await request('GET', '/missions/daily'))
    } catch (err) {
      setError(err.message || 'بارگذاری مأموریت‌های روزانه ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function sync() {
    setBusy(true)
    try {
      setPayload(await request('POST', '/missions/daily/sync', {}))
    } catch (err) {
      setError(err.message || 'همگام‌سازی ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function claim(id) {
    setBusy(true)
    try {
      await request('POST', `/quests/${id}/claim`, {})
      await load()
    } catch (err) {
      setError(err.message || 'دریافت پاداش ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  if (!payload && !error) return <p className="page-fallback">در حال بارگذاری مأموریت‌ها…</p>

  const items = payload?.items || []

  return (
    <div className="page daily-missions">
      <header className="page-header">
        <p className="daily-missions__en">ACHIEVE · Daily Missions</p>
        <h1>مأموریت‌های امروز</h1>
        <p>
          تاریخ محلی {payload?.localDate || '—'} · منطقه زمانی {payload?.timeZone || '—'}
        </p>
        <div className="daily-missions__actions">
          <button type="button" className="button button-secondary" disabled={busy} onClick={() => void sync()}>
            همگام‌سازی شواهد
          </button>
          <Link className="text-link" to="/progress">پیشرفت و پاداش</Link>
        </div>
      </header>

      {error ? <p className="daily-missions__error">{error}</p> : null}

      <PillarSection pillar="achieve" eyebrow="امروز" title="چه کاری معنادار انجام دهی؟">
        {items.length === 0 ? (
          <EmptyState
            pillar="achieve"
            title="مأموریت آماده‌ای نیست"
            description="با ثبت مطالعه، بازبینی یا ارزیابی، مأموریت‌های شواهدمحور پیشنهاد می‌شوند."
            actionTo="/reviews"
            actionLabel="بازبینی"
          />
        ) : (
          <ul className="daily-missions__list">
            {items.map((mission) => {
              const ratio = mission.targetValue
                ? Math.round((Math.min(mission.progress, mission.targetValue) / mission.targetValue) * 100)
                : 0
              return (
                <li key={mission.id} className={`daily-missions__card is-${(mission.status || '').toLowerCase()}`}>
                  <div className="daily-missions__card-head">
                    <strong>{mission.title}</strong>
                    <span className="daily-missions__status">{mission.status}</span>
                  </div>
                  <p>{mission.description}</p>
                  <p className="daily-missions__why">{mission.why}</p>
                  <p className="daily-missions__meta" dir="ltr">
                    {mission.progress}/{mission.targetValue}
                    {mission.estimatedMinutes ? ` · ~${mission.estimatedMinutes}m` : ''}
                    {` · +${mission.rewardXp} XP`}
                    {mission.rewardCoins ? ` · +${mission.rewardCoins} سکه` : ''}
                    {mission.pillar ? ` · ${mission.pillar}` : ''}
                  </p>
                  <Meter value={ratio} max={100} label={mission.title} tone="achieve" />
                  {mission.status === 'Completed' ? (
                    <button type="button" className="button button-primary" disabled={busy} onClick={() => void claim(mission.id)}>
                      دریافت پاداش
                    </button>
                  ) : null}
                  {mission.status === 'Claimed' ? <p className="daily-missions__done">پاداش دریافت شد.</p> : null}
                  {mission.status === 'Expired' ? <p className="daily-missions__done">منقضی — بدون جریمه.</p> : null}
                </li>
              )
            })}
          </ul>
        )}
      </PillarSection>

      <p className="daily-missions__policy">
        {payload?.timezonePolicy ||
          'تغییر منطقه زمانی مأموریت‌های صادرشده همان روز را بازنویسی نمی‌کند.'}
      </p>
    </div>
  )
}
