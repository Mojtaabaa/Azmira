// @ts-nocheck
import { useState } from 'react'
import { useAuth } from '../../hooks/useAuth'
import {
  clearLegacySkip,
  markLegacyImported,
  readBrowserLegacy,
  summarizeLegacy,
} from '../../utils/legacyData'
import { exportAllData, hasRecords, replaceAllData } from '../../utils/storage'
import { adoptLegacyTimer } from '../../utils/timerStore'

export default function BrowserImport() {
  const { user } = useAuth()
  const [legacy, setLegacy] = useState(() => readBrowserLegacy())
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  function refresh() {
    setLegacy(readBrowserLegacy())
    setMessage('')
    setError('')
  }

  if (!legacy) {
    return (
      <section className="card">
        <h2>وارد کردن از مرورگر</h2>
        <p className="card-copy">
          اگر قبلاً بدون حساب در همین مرورگر کار کرده باشی، داده‌هایش اینجا پیدا می‌شود. الان چیزی پیدا نشد.
        </p>
        <div className="button-row">
          <button type="button" className="button" onClick={refresh}>
            دوباره بررسی کن
          </button>
        </div>
      </section>
    )
  }

  const summary = summarizeLegacy(legacy)
  const accountHasData = hasRecords(exportAllData())

  async function importNow() {
    if (accountHasData) {
      const ok = window.confirm('داده‌های فعلی حساب با داده‌های مرورگر عوض شود؟')
      if (!ok) return
    }
    setBusy(true)
    setError('')
    setMessage('')
    try {
      clearLegacySkip(user.id)
      await replaceAllData(legacy)
      adoptLegacyTimer(user.id)
      markLegacyImported(user.id)
      setMessage('داده‌های مرورگر وارد حساب شد.')
      setLegacy(readBrowserLegacy())
    } catch (failure) {
      setError(failure.message || 'وارد نشد. دوباره امتحان کن.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card">
      <h2>وارد کردن از مرورگر</h2>
      <p className="card-copy">
        این داده‌ها هنوز در مرورگر هست: {summary}.
        {accountHasData
          ? ' وارد کردنش داده‌های فعلی حساب را عوض می‌کند.'
          : ' می‌توانی همین الان به حسابت اضافه کنی.'}
      </p>
      <div className="button-row">
        <button type="button" className="button button-primary" disabled={busy} onClick={importNow}>
          {busy ? 'کمی صبر کن' : 'وارد کردن به حساب'}
        </button>
        <button type="button" className="button" disabled={busy} onClick={refresh}>
          دوباره بررسی کن
        </button>
      </div>
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
      {message ? <p className="form-message is-ok" role="status">{message}</p> : null}
    </section>
  )
}
