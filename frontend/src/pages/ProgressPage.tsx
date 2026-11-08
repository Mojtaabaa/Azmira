// @ts-nocheck
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/ui/EmptyState'
import Meter from '../components/ui/Meter'
import StatusChip from '../components/ui/StatusChip'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import { resolveCapabilities } from '../utils/capabilities'
import './ProgressPage.css'

export default function ProgressPage() {
  const { status, user, workspaces, activeWorkspaceId, capabilities } = useAuth()
  const caps = resolveCapabilities(capabilities, workspaces, activeWorkspaceId, user)
  const canManageGamification = caps.platformAdmin || caps.active?.canManageSchool || caps.canManageSchool
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

  const claimable = useMemo(
    () => (profile?.completedQuests || []).filter((quest) => quest.status === 'Completed'),
    [profile],
  )

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
          <Link className="progress-admin-link" to="/missions">
            مأموریت‌های روزانه
          </Link>
          <Link className="progress-admin-link" to="/store">
            فروشگاه · {profile.coins} سکه
          </Link>
          <Link className="progress-admin-link" to="/leaderboards">
            رتبه‌بندی
          </Link>
          {canManageGamification ? (
            <Link className="progress-admin-link" to="/admin/gamification">
              تنظیمات بازی‌سازی
            </Link>
          ) : null}
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
        <Meter value={ratio} max={100} label="پیشرفت سطح" tone="achieve" />
      </section>

      <div className="progress-grid">
        <section className="cardish">
          <h2>نشان‌ها</h2>
          {(profile.earnedBadges || []).length === 0 ? (
            <EmptyState
              pillar="achieve"
              title="هنوز نشانی نداری"
              description="با تکمیل مأموریت‌ها و مسیر یادگیری، نشان‌ها ظاهر می‌شوند."
              actionTo="/missions"
              actionLabel="مأموریت‌ها"
            />
          ) : (
            <ul className="badge-list">
              {(profile.earnedBadges || []).map((badge) => (
                <li key={badge.id} className={`badge-chip tier-${badge.tier}`}>
                  <strong>{badge.title}</strong>
                  <span>{badge.description}</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="cardish">
          <h2>ماموریت‌های فعال</h2>
          {(profile.activeQuests || []).length === 0 ? (
            <EmptyState
              pillar="achieve"
              title="ماموریت فعالی نیست"
              description="مأموریت‌های روزانه را ببین یا مطالعه را شروع کن."
              actionTo="/missions"
              actionLabel="مأموریت‌های امروز"
            />
          ) : (
            <ul className="quest-list">
              {(profile.activeQuests || []).map((quest) => {
                const qRatio = quest.targetValue
                  ? Math.round((Math.min(quest.progress, quest.targetValue) / quest.targetValue) * 100)
                  : 0
                return (
                  <li key={quest.id}>
                    <div>
                      <div className="progress-quest-head">
                        <strong>{quest.title}</strong>
                        <StatusChip status={quest.status || 'Active'} />
                      </div>
                      <p>{quest.description}</p>
                      <span dir="ltr">
                        {quest.progress}/{quest.targetValue} · +{quest.rewardXp} XP
                      </span>
                      <Meter value={qRatio} max={100} label={quest.title} tone="achieve" />
                    </div>
                  </li>
                )
              })}
            </ul>
          )}
          <h2 className="progress-subhead">آماده دریافت</h2>
          {claimable.length === 0 ? (
            <p className="muted">هنوز پاداش آماده‌ای برای دریافت نیست.</p>
          ) : (
            <ul className="quest-list">
              {claimable.map((quest) => (
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
          )}
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
