// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SocialGamification.css'

export default function CompetitionsPage() {
  const { status, user, workspaces, activeWorkspaceId } = useAuth()
  const [items, setItems] = useState([])
  const [name, setName] = useState('')
  const [results, setResults] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const canCreate =
    user?.platformRole === 100 ||
    active?.role === 'schoolOwner' ||
    active?.role === 'schoolAdministrator' ||
    active?.role === 'teacher' ||
    active?.type === 'personal'

  const load = useCallback(async () => {
    try {
      setError('')
      const payload = await request('GET', '/competitions')
      setItems(payload.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری مسابقات ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function createCompetition() {
    if (!name.trim()) return
    setBusy(true)
    try {
      const startsAt = new Date().toISOString()
      const endsAt = new Date(Date.now() + 7 * 86400000).toISOString()
      await request('POST', '/competitions', {
        name: name.trim(),
        type: 'Individual',
        startsAt,
        endsAt,
        scoringRules: { metric: 'weeklyXp' },
        rewards: { coins: 20, xp: 50 },
      })
      setName('')
      await load()
    } catch (err) {
      setError(err.message || 'ساخت مسابقه ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function setStatus(id, next) {
    setBusy(true)
    try {
      await request('POST', `/competitions/${id}/status`, { status: next })
      await load()
    } catch (err) {
      setError(err.message || 'تغییر وضعیت ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function join(id) {
    setBusy(true)
    try {
      await request('POST', `/competitions/${id}/join`, {})
      await load()
    } catch (err) {
      setError(err.message || 'پیوستن ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function showResults(id) {
    try {
      setResults(await request('GET', `/competitions/${id}/results`))
    } catch (err) {
      setError(err.message || 'نتایج بارگذاری نشد.')
    }
  }

  return (
    <div className="social-page">
      <header className="social-hero">
        <div>
          <p className="social-kicker">چالش</p>
          <h1>مسابقات</h1>
          <p className="social-sub">مسابقه فردی یا تیمی با امتیاز XP دوره‌ای.</p>
        </div>
      </header>

      {error ? <p className="social-error">{error}</p> : null}

      {canCreate ? (
        <section className="social-panel">
          <h2>مسابقه جدید</h2>
          <div className="social-row">
            <input value={name} onChange={(event) => setName(event.target.value)} placeholder="نام مسابقه" />
            <button type="button" disabled={busy} onClick={createCompetition}>
              ساخت پیش‌نویس
            </button>
          </div>
        </section>
      ) : null}

      <section className="social-panel">
        <h2>فهرست</h2>
        <ul className="social-list">
          {items.length === 0 ? <li className="muted">مسابقه‌ای نیست.</li> : null}
          {items.map((item) => (
            <li key={item.id}>
              <div>
                <strong>{item.name}</strong>
                <span className="muted">
                  {item.status} · {item.type}
                </span>
              </div>
              <div className="social-actions">
                <button type="button" disabled={busy} onClick={() => join(item.id)}>
                  پیوستن
                </button>
                <button type="button" onClick={() => showResults(item.id)}>
                  نتایج
                </button>
                {canCreate && item.status === 'Draft' ? (
                  <button type="button" disabled={busy} onClick={() => setStatus(item.id, 'Active')}>
                    فعال‌سازی
                  </button>
                ) : null}
                {canCreate && item.status === 'Active' ? (
                  <button type="button" disabled={busy} onClick={() => setStatus(item.id, 'Ended')}>
                    پایان
                  </button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      </section>

      {results ? (
        <section className="social-panel">
          <h2>نتایج ({results.status})</h2>
          <ol className="rank-list">
            {(results.entries || []).map((row, index) => (
              <li key={`${row.userId || row.teamId}-${index}`}>
                <span className="rank-num">#{index + 1}</span>
                <span className="rank-name">{row.userId ? `کاربر ${row.userId}` : `تیم ${row.teamId}`}</span>
                <strong className="rank-score">{row.score}</strong>
              </li>
            ))}
          </ol>
        </section>
      ) : null}
    </div>
  )
}
