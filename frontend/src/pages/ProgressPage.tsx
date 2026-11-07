// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './ProgressPage.css'

export default function ProgressPage() {
  const { status } = useAuth()
  const [profile, setProfile] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setError('')
      setProfile(await request('GET', '/gamification/me'))
    } catch (err) {
      setError(err.message || 'بارگذاری پیشرفت ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function claimQuest(id) {
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

  if (!profile && !error) return <p className="page-fallback">در حال بارگذاری پیشرفت…</p>
  if (error && !profile) return <p className="progress-error">{error}</p>

  const ratio = Math.round((profile.levelProgressRatio || 0) * 100)

  return (
    <div className="progress-page">
      <header className="progress-hero">
        <div>
          <p className="progress-kicker">پیشرفت</p>
          <h1>
            سطح {profile.level}
            <span> — {profile.levelTitle}</span>
          </h1>
          <p className="progress-sub">
            {profile.xpTotal} XP · {profile.coins} سکه · استریک {profile.streakCurrent} روز
          </p>
        </div>
        <div className="progress-hero-links">
          <Link className="progress-admin-link" to="/store">
            فروشگاه · {profile.coins} سکه
          </Link>
          <Link className="progress-admin-link" to="/leaderboards">
            رتبه‌بندی
          </Link>
          <Link className="progress-admin-link" to="/admin/gamification">
            تنظیمات بازی‌سازی
          </Link>
        </div>
      </header>

      <section className="progress-level cardish" aria-label="سطح">
        <div className="progress-level-meta">
          <strong>{profile.levelTitle}</strong>
          <span>
            {profile.nextLevelTitle
              ? `${profile.levelProgressXp} / ${profile.levelNeedXp} تا ${profile.nextLevelTitle}`
              : 'بالاترین سطح'}
          </span>
        </div>
        <div className="progress-bar" role="progressbar" aria-valuenow={ratio} aria-valuemin={0} aria-valuemax={100}>
          <span style={{ width: `${ratio}%` }} />
        </div>
      </section>

      <div className="progress-grid">
        <section className="cardish">
          <h2>نشان‌ها</h2>
          <ul className="badge-list">
            {(profile.earnedBadges || []).length === 0 ? <li className="muted">هنوز نشانی نداری.</li> : null}
            {(profile.earnedBadges || []).map((badge) => (
              <li key={badge.id} className={`badge-chip tier-${badge.tier}`}>
                <strong>{badge.title}</strong>
                <span>{badge.description}</span>
              </li>
            ))}
          </ul>
        </section>

        <section className="cardish">
          <h2>ماموریت‌های فعال</h2>
          <ul className="quest-list">
            {(profile.activeQuests || []).length === 0 ? <li className="muted">ماموریت فعالی نیست.</li> : null}
            {(profile.activeQuests || []).map((quest) => (
              <li key={quest.id}>
                <div>
                  <strong>{quest.title}</strong>
                  <p>{quest.description}</p>
                  <span dir="ltr">
                    {quest.progress}/{quest.targetValue} · +{quest.rewardXp} XP
                  </span>
                </div>
              </li>
            ))}
          </ul>
          <h2 className="progress-subhead">آماده دریافت</h2>
          <ul className="quest-list">
            {(profile.completedQuests || [])
              .filter((quest) => quest.status === 'Completed')
              .map((quest) => (
                <li key={quest.id} className="quest-claim">
                  <div>
                    <strong>{quest.title}</strong>
                    <span>
                      +{quest.rewardXp} XP · +{quest.rewardCoins} سکه
                    </span>
                  </div>
                  <button type="button" disabled={busy} onClick={() => claimQuest(quest.id)}>
                    دریافت
                  </button>
                </li>
              ))}
          </ul>
        </section>
      </div>

      <section className="cardish">
        <h2>تقویم استریک</h2>
        <div className="streak-calendar" dir="ltr">
          {(profile.streakCalendar || []).slice(0, 28).map((day) => (
            <span key={day.date} className={`streak-day kind-${day.kind}`} title={`${day.date} · ${day.kind}`}>
              {String(day.date).slice(-2)}
            </span>
          ))}
        </div>
        <p className="muted">
          بهترین رکورد: {profile.streakBest} · فریز باقی‌مانده: {profile.streakFreezes}
        </p>
      </section>

      {error ? <p className="progress-error">{error}</p> : null}
    </div>
  )
}
