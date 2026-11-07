// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import CapabilityGate from '../components/CapabilityGate'
import { request } from '../utils/api'
import './SchoolEdition.css'

export default function PlatformAdminPage() {
  return (
    <CapabilityGate require="platformAdmin" title="ادمین سامانه">
      <PlatformAdminInner />
    </CapabilityGate>
  )
}

function PlatformAdminInner() {
  const [dash, setDash] = useState(null)
  const [users, setUsers] = useState([])
  const [requests, setRequests] = useState([])
  const [query, setQuery] = useState('')
  const [schoolName, setSchoolName] = useState('')
  const [ownerUserId, setOwnerUserId] = useState('')
  const [city, setCity] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setError('')
      const [d, u, r] = await Promise.all([
        request('GET', '/platform/dashboard'),
        request('GET', `/platform/users?q=${encodeURIComponent(query)}&take=80`),
        request('GET', '/platform/school-requests'),
      ])
      setDash(d)
      setUsers(u.items || [])
      setRequests(r.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری پنل ادمین ممکن نشد.')
    }
  }, [query])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div className="school-page">
      <header className="school-hero">
        <div>
          <p className="school-kicker">پلتفرم</p>
          <h1>ادمین سامانه</h1>
          <p className="school-sub">مدیریت نقش‌های سراسری، تأیید درخواست مدرسه، و ساخت مستقیم مدرسه.</p>
        </div>
      </header>
      {error ? <p className="school-error">{error}</p> : null}
      {message ? <p className="school-ok">{message}</p> : null}

      <section className="school-panel">
        <h2>شاخص‌ها</h2>
        <div className="school-stats">
          <div><strong>{dash?.users ?? '—'}</strong><span>کاربران</span></div>
          <div><strong>{dash?.platformAdmins ?? '—'}</strong><span>ادمین سامانه</span></div>
          <div><strong>{dash?.schools ?? '—'}</strong><span>مدارس</span></div>
          <div><strong>{dash?.pendingSchoolRequests ?? '—'}</strong><span>درخواست در انتظار</span></div>
        </div>
      </section>

      <section className="school-panel">
        <h2>درخواست‌های مدرسه</h2>
        {requests.length === 0 ? <p className="muted">درخواستی در صف نیست.</p> : null}
        <ul className="school-list">
          {requests.map((item) => (
            <li key={item.id}>
              <div>
                <strong>{item.name}</strong>
                <span className="muted">
                  {item.city || '—'} · {item.requesterDisplayName || item.requesterUsername || item.requesterUserId}
                </span>
              </div>
              <div className="school-row" style={{ marginBottom: 0 }}>
                <button
                  type="button"
                  disabled={busy}
                  onClick={async () => {
                    setBusy(true)
                    try {
                      await request('POST', `/platform/school-requests/${item.id}/approve`, {})
                      setMessage(`مدرسه «${item.name}» تأیید شد.`)
                      await load()
                    } catch (err) {
                      setError(err.message)
                    } finally {
                      setBusy(false)
                    }
                  }}
                >
                  تأیید
                </button>
                <button
                  type="button"
                  disabled={busy}
                  onClick={async () => {
                    setBusy(true)
                    try {
                      await request('POST', `/platform/school-requests/${item.id}/reject`, { notes: 'رد توسط ادمین' })
                      setMessage(`درخواست «${item.name}» رد شد.`)
                      await load()
                    } catch (err) {
                      setError(err.message)
                    } finally {
                      setBusy(false)
                    }
                  }}
                >
                  رد
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>

      <section className="school-panel">
        <h2>ساخت مستقیم مدرسه</h2>
        <div className="school-row">
          <input value={ownerUserId} onChange={(e) => setOwnerUserId(e.target.value)} placeholder="ownerUserId" />
          <input value={schoolName} onChange={(e) => setSchoolName(e.target.value)} placeholder="نام مدرسه" />
          <input value={city} onChange={(e) => setCity(e.target.value)} placeholder="شهر" />
          <button
            type="button"
            disabled={busy}
            onClick={async () => {
              setBusy(true)
              try {
                await request('POST', '/platform/schools', {
                  ownerUserId: Number(ownerUserId),
                  name: schoolName.trim(),
                  city: city.trim() || undefined,
                })
                setSchoolName('')
                setOwnerUserId('')
                setCity('')
                setMessage('مدرسه ساخته شد.')
                await load()
              } catch (err) {
                setError(err.message)
              } finally {
                setBusy(false)
              }
            }}
          >
            ایجاد
          </button>
        </div>
      </section>

      <section className="school-panel">
        <h2>کاربران سامانه</h2>
        <div className="school-row">
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="جستجو نام کاربری / نمایشی" />
          <button type="button" onClick={load}>جستجو</button>
        </div>
        <div className="school-table-wrap">
          <table className="school-table">
            <thead>
              <tr>
                <th>شناسه</th>
                <th>نام کاربری</th>
                <th>نمایشی</th>
                <th>نقش سراسری</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {users.map((item) => (
                <tr key={item.id}>
                  <td>{item.id}</td>
                  <td>{item.username}</td>
                  <td>{item.displayName || '—'}</td>
                  <td>{item.isPlatformAdmin ? 'ادمین سامانه' : 'عادی'}</td>
                  <td>
                    {item.isPlatformAdmin ? (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={async () => {
                          setBusy(true)
                          try {
                            await request('POST', `/platform/users/${item.id}/role`, { platformRole: 0 })
                            await load()
                          } catch (err) {
                            setError(err.message)
                          } finally {
                            setBusy(false)
                          }
                        }}
                      >
                        حذف ادمین
                      </button>
                    ) : (
                      <button
                        type="button"
                        disabled={busy}
                        onClick={async () => {
                          setBusy(true)
                          try {
                            await request('POST', `/platform/users/${item.id}/role`, { platformRole: 100 })
                            await load()
                          } catch (err) {
                            setError(err.message)
                          } finally {
                            setBusy(false)
                          }
                        }}
                      >
                        ارتقا به ادمین
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
