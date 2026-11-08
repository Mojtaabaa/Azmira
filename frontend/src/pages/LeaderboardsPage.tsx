// @ts-nocheck
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/ui/EmptyState'
import { useAuth } from '../hooks/useAuth'
import { useSettings } from '../hooks/useSettings'
import { request } from '../utils/api'
import './SocialGamification.css'

const METRICS = [
  { id: 'PersonalImprovement', label: 'بهبود شخصی (پیش‌فرض)', self: true },
  { id: 'LearningImprovement', label: 'بهبود هم‌کلاسی (اختیاری)', peer: true },
  { id: 'WeeklyXp', label: 'XP هفتگی' },
  { id: 'MonthlyXp', label: 'XP ماهانه' },
  { id: 'Consistency', label: 'ثبات' },
  { id: 'GoalCompletion', label: 'اهداف' },
  { id: 'QuestCompletion', label: 'ماموریت‌ها' },
  { id: 'Team', label: 'تیم‌ها' },
]

export default function LeaderboardsPage() {
  const { status, user } = useAuth()
  const settings = useSettings()
  const subjectOptions = useMemo(() => {
    const custom = (settings?.customSubjects || []).map((s) => s.name || s.id).filter(Boolean)
    const defaults = ['ریاضی', 'فیزیک', 'شیمی', 'زیست', 'ادبیات', 'عربی', 'دینی', 'زبان']
    return [...new Set([...custom, ...defaults])]
  }, [settings])

  const [metric, setMetric] = useState('PersonalImprovement')
  const [board, setBoard] = useState(null)
  const [selfView, setSelfView] = useState(null)
  const [privacy, setPrivacy] = useState({ leaderboardVisible: false, showRealName: true })
  const [page, setPage] = useState(1)
  const [subject, setSubject] = useState(subjectOptions[0] || 'ریاضی')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!subjectOptions.includes(subject) && subjectOptions[0]) setSubject(subjectOptions[0])
  }, [subjectOptions, subject])

  const load = useCallback(async () => {
    try {
      setError('')
      const privacyPayload = await request('GET', '/gamification/privacy')
      setPrivacy(privacyPayload)

      if (metric === 'PersonalImprovement') {
        setSelfView(await request('GET', '/improvement/me'))
        setBoard(null)
        return
      }

      const qs = new URLSearchParams({
        metric,
        period: metric === 'MonthlyXp' ? 'month' : 'week',
        page: String(page),
        pageSize: '20',
      })
      if (metric === 'LearningImprovement') qs.set('subject', subject || subjectOptions[0] || 'ریاضی')
      setBoard(await request('GET', `/leaderboards?${qs.toString()}`))
      setSelfView(null)
    } catch (err) {
      setError(err.message || 'بارگذاری جدول رتبه‌بندی ممکن نشد.')
    }
  }, [metric, page, subject, subjectOptions])

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

  if (!board && !selfView && !error) return <p className="page-fallback">در حال بارگذاری رتبه‌بندی…</p>

  const composite = selfView?.composite
  const myUserId = user?.id
  const peerEmpty = board && (board.entries || []).length === 0

  return (
    <div className="social-page">
      <header className="social-hero">
        <div>
          <p className="social-kicker">رقابت دوستانه</p>
          <h1>جدول رتبه‌بندی</h1>
          <p className="social-sub">
            پیش‌فرض: بهبود شخصی نسبت به معیار خودت. نمایش گروهی خاموش است مگر خودت فعال کنی.
          </p>
        </div>
      </header>

      {error ? <p className="social-error">{error}</p> : null}

      <section className="social-panel">
        <h2>حریم خصوصی</h2>
        <p className="muted">{privacy.note || 'نمایش در جدول‌های گروهی به‌صورت پیش‌فرض خاموش است.'}</p>
        <label className="social-check">
          <input
            type="checkbox"
            checked={!!privacy.leaderboardVisible}
            disabled={busy}
            onChange={(event) => savePrivacy({ ...privacy, leaderboardVisible: event.target.checked })}
          />
          نمایش من در جدول‌های گروهی
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

      {metric === 'LearningImprovement' ? (
        <div className="social-panel">
          <label>
            درس برای مقایسه هم‌کلاسی{' '}
            <select value={subject} onChange={(e) => setSubject(e.target.value)}>
              {subjectOptions.map((name) => (
                <option key={name} value={name}>
                  {name}
                </option>
              ))}
            </select>
          </label>
          <p className="muted">{board?.note}</p>
        </div>
      ) : null}

      {selfView ? (
        <section className="social-panel">
          <h2>نمای بهبود شخصی (نه رتبه گروهی)</h2>
          <p className="muted">{selfView.scoringPolicy}</p>
          {composite?.status === 'Ready' ? (
            <ol className="rank-list">
              <li>
                <span className="rank-num">Δ</span>
                <span className="rank-name">نسبت به معیار پایه خودت</span>
                <strong className="rank-score" dir="ltr">
                  {Number(composite.improvementPercent) >= 0 ? '+' : ''}
                  {Number(composite.improvementPercent).toFixed(1)}%
                </strong>
              </li>
            </ol>
          ) : (
            <EmptyState
              pillar="improve"
              title={composite?.message || 'در حال جمع‌آوری معیار پایه'}
              description="پس از Ready شدن، Δ شخصی اینجا دیده می‌شود."
              actionTo="/improvement"
              actionLabel="بهبود شخصی"
            />
          )}
          <Link className="text-link" to="/improvement">
            جزئیات و نمودار
          </Link>
        </section>
      ) : peerEmpty ? (
        <section className="social-panel">
          <EmptyState
            pillar="achieve"
            title={!privacy.leaderboardVisible ? 'جدول گروهی خالی یا خصوصی است' : 'هنوز رتبه‌ای ثبت نشده'}
            description={
              !privacy.leaderboardVisible
                ? 'اگر می‌خواهی دیده شوی، نمایش در جدول‌های گروهی را در حریم خصوصی روشن کن.'
                : 'با کسب XP یا بهبود شخصی، ردیف‌ها ظاهر می‌شوند.'
            }
            actionTo="/improvement"
            actionLabel="بهبود شخصی"
          />
        </section>
      ) : (
        <section className="social-panel">
          {board?.labelFa ? (
            <p className="muted">
              {board.labelFa}
              {board.peerComparison ? ' · مقایسه گروهی' : ''}
              {board.note ? ` — ${board.note}` : ''}
            </p>
          ) : null}
          <ol className="rank-list">
            {(board?.entries || []).map((row) => {
              const isSelf = myUserId != null && Number(row.userId) === Number(myUserId)
              return (
                <li key={`${row.rank}-${row.userId || row.teamId}`} className={isSelf ? 'is-self' : undefined}>
                  <span className="rank-num">#{row.rank}</span>
                  <span className="rank-name">
                    {row.displayName}
                    {isSelf ? ' · تو' : ''}
                  </span>
                  <strong className="rank-score">{row.score}</strong>
                </li>
              )
            })}
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
      )}
    </div>
  )
}
