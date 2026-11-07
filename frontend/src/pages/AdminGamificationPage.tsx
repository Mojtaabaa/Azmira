// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './AdminGamificationPage.css'

export default function AdminGamificationPage() {
  const { user, workspaces, activeWorkspaceId } = useAuth()
  const [profile, setProfile] = useState(null)
  const [badges, setBadges] = useState([])
  const [quests, setQuests] = useState([])
  const [rewards, setRewards] = useState([])
  const [storeTitle, setStoreTitle] = useState('')
  const [storeCode, setStoreCode] = useState('')
  const [storePrice, setStorePrice] = useState(50)
  const [rulesText, setRulesText] = useState('')
  const [levelsText, setLevelsText] = useState('')
  const [streakText, setStreakText] = useState('')
  const [dailyXpCap, setDailyXpCap] = useState(200)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const canAdmin = user?.platformRole === 100 || active?.role === 'schoolOwner' || active?.role === 'schoolAdministrator'

  const load = useCallback(async () => {
    if (!canAdmin) return
    try {
      setError('')
      const [profilePayload, badgePayload, questPayload, rewardPayload] = await Promise.all([
        request('GET', '/admin/gamification/profile'),
        request('GET', '/admin/gamification/badges'),
        request('GET', '/admin/gamification/quests'),
        request('GET', '/rewards'),
      ])
      setProfile(profilePayload)
      setDailyXpCap(profilePayload.dailyXpCap ?? 200)
      setRulesText(JSON.stringify(profilePayload.rules, null, 2))
      setLevelsText(JSON.stringify(profilePayload.levels, null, 2))
      setStreakText(JSON.stringify(profilePayload.streak, null, 2))
      setBadges(badgePayload.items || [])
      setQuests(questPayload.items || [])
      setRewards(rewardPayload.items || [])
    } catch (err) {
      setError(err.message || 'بارگذاری تنظیمات ممکن نشد.')
    }
  }, [canAdmin])

  useEffect(() => {
    void load()
  }, [load])

  async function save() {
    try {
      setMessage('')
      setError('')
      await request('PUT', '/admin/gamification/profile', {
        dailyXpCap: Number(dailyXpCap),
        rules: JSON.parse(rulesText),
        levels: JSON.parse(levelsText),
        streak: JSON.parse(streakText),
      })
      setMessage('ذخیره شد.')
      await load()
    } catch (err) {
      setError(err.message || 'ذخیره ممکن نشد. JSON را بررسی کن.')
    }
  }

  if (!canAdmin) {
    return (
      <div className="admin-game-page">
        <h1>مدیریت بازی‌سازی</h1>
        <p>فقط مدیر پلتفرم یا مدیر مدرسه به این صفحه دسترسی دارد.</p>
      </div>
    )
  }

  return (
    <div className="admin-game-page">
      <header>
        <h1>مدیریت بازی‌سازی</h1>
        <p>
          پروفایل: {profile?.name || '…'} {profile?.isSystem ? '(سیستمی — با ذخیره نسخه فضای کاری ساخته می‌شود)' : ''}
        </p>
      </header>

      <label className="admin-field">
        سقف XP روزانه
        <input type="number" min={1} value={dailyXpCap} onChange={(event) => setDailyXpCap(event.target.value)} />
      </label>

      <label className="admin-field">
        قوانین XP
        <textarea rows={12} value={rulesText} onChange={(event) => setRulesText(event.target.value)} spellCheck={false} />
      </label>

      <label className="admin-field">
        سطوح
        <textarea rows={10} value={levelsText} onChange={(event) => setLevelsText(event.target.value)} spellCheck={false} />
      </label>

      <label className="admin-field">
        تنظیمات استریک
        <textarea rows={8} value={streakText} onChange={(event) => setStreakText(event.target.value)} spellCheck={false} />
      </label>

      <button type="button" className="admin-save" onClick={save}>
        ذخیره پروفایل
      </button>
      {message ? <p className="admin-ok">{message}</p> : null}
      {error ? <p className="admin-error">{error}</p> : null}

      <section>
        <h2>نشان‌ها ({badges.length})</h2>
        <ul className="admin-list">
          {badges.map((badge) => (
            <li key={badge.id}>
              <strong>{badge.title}</strong>
              <span>
                {badge.tier} · {badge.code}
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section>
        <h2>ماموریت‌ها ({quests.length})</h2>
        <ul className="admin-list">
          {quests.map((quest) => (
            <li key={quest.id}>
              <strong>{quest.title}</strong>
              <span>
                {quest.kind} · هدف {quest.targetValue} · +{quest.rewardXp} XP
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section>
        <h2>آیتم فروشگاه فضای کاری</h2>
        <div className="admin-inline">
          <input placeholder="کد" value={storeCode} onChange={(event) => setStoreCode(event.target.value)} />
          <input placeholder="عنوان" value={storeTitle} onChange={(event) => setStoreTitle(event.target.value)} />
          <input
            type="number"
            min={0}
            value={storePrice}
            onChange={(event) => setStorePrice(event.target.value)}
          />
          <button
            type="button"
            onClick={async () => {
              try {
                setMessage('')
                setError('')
                await request('POST', '/admin/store/items', {
                  code: storeCode,
                  title: storeTitle,
                  category: 'Accessory',
                  priceCoins: Number(storePrice),
                  assetKey: storeCode,
                  cssClass: `avatar-acc avatar-acc-${storeCode}`,
                })
                setMessage('آیتم فروشگاه ذخیره شد.')
                setStoreCode('')
                setStoreTitle('')
              } catch (err) {
                setError(err.message || 'ذخیره آیتم ممکن نشد.')
              }
            }}
          >
            افزودن آیتم
          </button>
        </div>
      </section>

      <section>
        <h2>تأیید پاداش‌ها</h2>
        <ul className="admin-list">
          {rewards.filter((item) => item.status === 'Pending').length === 0 ? (
            <li>پاداش در انتظاری نیست.</li>
          ) : null}
          {rewards
            .filter((item) => item.status === 'Pending')
            .map((item) => (
              <li key={item.id}>
                <strong>{item.title}</strong>
                <span>
                  کاربر {item.userId} · {item.kind}
                </span>
                <button
                  type="button"
                  onClick={async () => {
                    await request('POST', `/rewards/${item.id}/approve`, { approve: true })
                    await load()
                  }}
                >
                  تأیید
                </button>
              </li>
            ))}
        </ul>
      </section>
    </div>
  )
}
