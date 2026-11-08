// @ts-nocheck
import { useCallback, useEffect, useMemo, useState } from 'react'
import AvatarPortrait from '../avatar/AvatarPortrait'
import { readLocalPrototype, writeLocalPrototype } from '../avatar/assets'
import EmptyState from '../components/ui/EmptyState'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './SocialGamification.css'

export default function StorePage() {
  const { status } = useAuth()
  const [catalog, setCatalog] = useState([])
  const [inventory, setInventory] = useState([])
  const [loadout, setLoadout] = useState(null)
  const [coins, setCoins] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [category, setCategory] = useState('all')
  const [affordableOnly, setAffordableOnly] = useState(false)
  const [localAppearance, setLocalAppearance] = useState(() => readLocalPrototype())

  const ownedIds = useMemo(() => new Set((inventory || []).map((item) => item.itemId)), [inventory])

  function applyLocalPrototype(partial) {
    const next = writeLocalPrototype(partial)
    setLocalAppearance(next)
  }

  const load = useCallback(async () => {
    try {
      setError('')
      const [store, inv, me] = await Promise.all([
        request('GET', '/store'),
        request('GET', '/store/inventory'),
        request('GET', '/gamification/me'),
      ])
      setCatalog(store.items || [])
      setInventory(inv.items || [])
      setLoadout(inv.loadout)
      setCoins(me.coins)
    } catch (err) {
      setError(err.message || 'بارگذاری فروشگاه ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void load()
  }, [status, load])

  const categories = useMemo(() => {
    const set = new Set((catalog || []).map((item) => item.category).filter(Boolean))
    return ['all', ...set]
  }, [catalog])

  const visible = useMemo(() => {
    return (catalog || []).filter((item) => {
      if (category !== 'all' && item.category !== category) return false
      if (affordableOnly && coins != null && Number(item.priceCoins) > Number(coins)) return false
      return true
    })
  }, [catalog, category, affordableOnly, coins])

  async function purchase(itemId) {
    setBusy(true)
    try {
      const result = await request('POST', '/store/purchase', { itemId })
      setCoins(result.coins)
      await load()
    } catch (err) {
      setError(err.message || 'خرید ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function activate(itemId) {
    setBusy(true)
    try {
      const result = await request('POST', '/store/activate', { itemId })
      setLoadout(result.loadout)
      await load()
    } catch (err) {
      setError(err.message || 'فعال‌سازی ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="social-page">
      <header className="social-hero">
        <div>
          <p className="social-kicker">آواتار</p>
          <h1>فروشگاه سکه</h1>
          <p className="social-sub">فقط با سکه خرید کن — XP قابل خرج نیست. موجودی: {coins ?? '…'} سکه</p>
        </div>
        <div className="store-avatar-panel">
          <AvatarPortrait
            size="lg"
            loadout={loadout}
            inventory={inventory}
            localOverrides={localAppearance}
          />
          <div className="store-avatar-prototype" role="group" aria-label="پروتوتایپ ظاهری محلی">
            <p className="muted">
              پیش‌نمایش محلی فقط روی این دستگاه است و با موجودی سرور یکی نیست.
            </p>
            <div className="store-avatar-prototype__actions">
              <button type="button" className="button button-secondary" disabled={busy} onClick={() => applyLocalPrototype({ accessory: 'glasses' })}>عینک</button>
              <button type="button" className="button button-secondary" disabled={busy} onClick={() => applyLocalPrototype({ companion: 'owl' })}>جغد</button>
              <button type="button" className="button button-secondary" disabled={busy} onClick={() => applyLocalPrototype({ hairStyle: 'bun', accessory: 'none', companion: 'none' })}>بازنشانی ظاهری</button>
            </div>
          </div>
        </div>
      </header>

      {error ? <p className="social-error">{error}</p> : null}

      <section className="social-panel">
        <h2>کاتالوگ</h2>
        <div className="store-filters">
          <div className="store-filter-chips">
            {categories.map((cat) => (
              <button
                key={cat}
                type="button"
                className={`store-filter-chip${category === cat ? ' is-active' : ''}`}
                onClick={() => setCategory(cat)}
              >
                {cat === 'all' ? 'همه' : cat}
              </button>
            ))}
          </div>
          <label className="social-check">
            <input type="checkbox" checked={affordableOnly} onChange={(e) => setAffordableOnly(e.target.checked)} />
            فقط قابل خرید با موجودی فعلی
          </label>
        </div>
        {visible.length === 0 ? (
          <EmptyState
            pillar="achieve"
            title="آیتمی در این فیلتر نیست"
            description="فیلتر را عوض کن یا سکه بیشتری از مأموریت‌ها بگیر."
            actionTo="/missions"
            actionLabel="مأموریت‌ها"
          />
        ) : (
          <div className="store-grid">
            {visible.map((item) => {
              const owned = ownedIds.has(item.id)
              const tooExpensive = coins != null && Number(item.priceCoins) > Number(coins)
              return (
                <article key={item.id} className="store-card">
                  <div className={`store-thumb ${item.cssClass}`} />
                  <h3>{item.title}</h3>
                  <p className="muted">
                    {item.category} · {item.priceCoins === 0 ? 'رایگان' : `${item.priceCoins} سکه`}
                    {owned ? ' · مال تو' : ''}
                  </p>
                  <div className="social-actions">
                    {!owned || item.stackable ? (
                      <button type="button" disabled={busy || tooExpensive} onClick={() => purchase(item.id)}>
                        {tooExpensive ? 'سکه ناکافی' : 'خرید'}
                      </button>
                    ) : (
                      <button type="button" disabled={busy} onClick={() => activate(item.id)}>
                        فعال‌سازی
                      </button>
                    )}
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </section>

      <section className="social-panel">
        <h2>موجودی سرور</h2>
        {inventory.length === 0 ? (
          <EmptyState
            pillar="achieve"
            title="هنوز چیزی نخریده‌ای"
            description="از کاتالوگ بالا یک آیتم بخر تا در موجودی سرور بیاید."
          />
        ) : (
          <ul className="social-list">
            {inventory.map((item) => (
              <li key={item.id}>
                <div>
                  <strong>{item.title}</strong>
                  <span className="muted">
                    {item.category}
                    {item.equipped ? ' · فعال' : ''}
                  </span>
                </div>
                <button type="button" disabled={busy} onClick={() => activate(item.itemId)}>
                  فعال
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
