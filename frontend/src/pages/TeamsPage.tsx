// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SocialGamification.css'

export default function TeamsPage() {
  const { status } = useAuth()
  const [teams, setTeams] = useState([])
  const [name, setName] = useState('')
  const [selected, setSelected] = useState(null)
  const [detail, setDetail] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setError('')
      const payload = await request('GET', '/teams')
      setTeams(payload.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری تیم‌ها ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  async function createTeam() {
    if (!name.trim()) return
    setBusy(true)
    try {
      await request('POST', '/teams', { name: name.trim() })
      setName('')
      await load()
    } catch (err) {
      setError(err.message || 'ساخت تیم ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function joinTeam(id) {
    setBusy(true)
    try {
      await request('POST', `/teams/${id}/join`, {})
      await load()
    } catch (err) {
      setError(err.message || 'پیوستن ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function openTeam(id) {
    setSelected(id)
    try {
      setDetail(await request('GET', `/teams/${id}`))
    } catch (err) {
      setError(err.message || 'جزئیات تیم بارگذاری نشد.')
    }
  }

  return (
    <div className="social-page">
      <header className="social-hero">
        <div>
          <p className="social-kicker">هم‌تیمی</p>
          <h1>تیم‌ها</h1>
          <p className="social-sub">بساز، دعوت کن، و XP تیمی جمع کنید.</p>
        </div>
      </header>

      {error ? <p className="social-error">{error}</p> : null}

      <section className="social-panel">
        <h2>ساخت تیم</h2>
        <div className="social-row">
          <input value={name} onChange={(event) => setName(event.target.value)} placeholder="نام تیم" />
          <button type="button" disabled={busy} onClick={createTeam}>
            ساخت
          </button>
        </div>
      </section>

      <section className="social-panel">
        <h2>فهرست</h2>
        <ul className="social-list">
          {teams.length === 0 ? <li className="muted">هنوز تیمی نیست.</li> : null}
          {teams.map((team) => (
            <li key={team.id}>
              <div>
                <strong>{team.name}</strong>
                <span className="muted">
                  {team.memberCount} عضو {team.isMember ? '· عضو هستی' : ''}
                </span>
              </div>
              <div className="social-actions">
                {!team.isMember ? (
                  <button type="button" disabled={busy} onClick={() => joinTeam(team.id)}>
                    پیوستن
                  </button>
                ) : null}
                <button type="button" onClick={() => openTeam(team.id)}>
                  جزئیات
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>

      {selected && detail ? (
        <section className="social-panel">
          <h2>
            {detail.name} · {detail.teamXp} XP تیمی
          </h2>
          <p>{detail.description || 'بدون توضیح'}</p>
          <h3>اعضا</h3>
          <ul className="social-list compact">
            {(detail.members || []).map((member) => (
              <li key={`${member.userId}-${member.joinedAt}`}>
                کاربر {member.userId} · {member.role}
                {member.leftAt ? ' (خارج شده)' : ''}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  )
}
