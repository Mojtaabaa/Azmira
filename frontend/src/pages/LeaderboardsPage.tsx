// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SocialGamification.css'

const METRICS = [
  { id: 'WeeklyXp', label: 'XP هفتگی' },
  { id: 'MonthlyXp', label: 'XP ماهانه' },
  { id: 'Consistency', label: 'ثبات' },
  { id: 'GoalCompletion', label: 'اهداف' },
  { id: 'QuestCompletion', label: 'ماموریت‌ها' },
  { id: 'Team', label: 'تیم‌ها' },
]

export default function LeaderboardsPage() {
  const { status } = useAuth()
  const [metric, setMetric] = useState('WeeklyXp')
  const [board, setBoard] = useState(null)
  const [privacy, setPrivacy] = useState({ leaderboardVisible: true, showRealName: true })
  const [page, setPage] = useState(1)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setError('')
      const [boardPayload, privacyPayload] = await Promise.all([
        request('GET', `/leaderboards?metric=${metric}&period=${metric === 'MonthlyXp' ? 'month' : 'week'}&page=${page}&pageSize=20`),
        request('GET', '/gamification/privacy'),
      ])
      setBoard(boardPayload)
      setPrivacy(privacyPayload)
    } catch (err) {
      setError(err.message || 'بارگذاری جدول رتبه‌بندی ممکن نشد.')
    }
  }, [metric, page])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function savePrivacy(next) {
    setBusy(true)
    try {
      const payload = await request('PUT', '/gamification/privacy', next)
      setPrivacy(payload)
      await load()
    } catch (err) {
      setError(err.message || 'ذخیره حریم خصوصی ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  if (!board && !error) return <p className="page-fallback">در حال بارگذاری رتبه‌بندی…</p>

  return (
    <div className="social-page">
      <header className="social-hero">
        <div>
          <p className="social-kicker">رقابت دوستانه</p>
          <h1>جدول رتبه‌بندی</h1>
          <p className="social-sub">فقط نام نمایشی یا برچسب ناشناس — بدون نام کاربری و ایمیل.</p>
        </div>
      </header>

      {error ? <p className="social-error">{error}</p> : null}

      <section className="social-panel">
        <h2>حریم خصوصی</h2>
        <label className="social-check">
          <input
            type="checkbox"
            checked={!!privacy.leaderboardVisible}
            disabled={busy}
            onChange={(event) => savePrivacy({ ...privacy, leaderboardVisible: event.target.checked })}
          />
          نمایش من در جدول‌ها
        </label>
        <label className="social-check">
          <input
            type="checkbox"
            checked={!!privacy.showRealName}
            disabled={busy}
            onChange={(event) => savePrivacy({ ...privacy, showRealName: event.target.checked })}
          />
          نمایش نام واقعی
        </label>
      </section>

      <div className="social-tabs" role="tablist">
        {METRICS.map((item) => (
          <button
            key={item.id}
            type="button"
            className={metric === item.id ? 'is-active' : ''}
            onClick={() => {
              setPage(1)
              setMetric(item.id)
            }}
          >
            {item.label}
          </button>
        ))}
      </div>

      <section className="social-panel">
        <ol className="rank-list">
          {(board?.entries || []).length === 0 ? <li className="muted">هنوز رتبه‌ای ثبت نشده.</li> : null}
          {(board?.entries || []).map((row) => (
            <li key={`${row.rank}-${row.userId || row.teamId}`}>
              <span className="rank-num">#{row.rank}</span>
              <span className="rank-name">{row.displayName}</span>
              <strong className="rank-score">{row.score}</strong>
            </li>
          ))}
        </ol>
        <div className="social-pager">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
            قبلی
          </button>
          <span>
            صفحه {page} از {Math.max(1, Math.ceil((board?.total || 0) / (board?.pageSize || 20)))}
          </span>
          <button
            type="button"
            disabled={(board?.entries || []).length < (board?.pageSize || 20)}
            onClick={() => setPage((p) => p + 1)}
          >
            بعدی
          </button>
        </div>
      </section>
    </div>
  )
}
