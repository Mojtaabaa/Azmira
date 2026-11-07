// @ts-nocheck
import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { adoptBrowserDataIfNeeded } from '../utils/adoptBrowserData'
import {
  markLegacyHandled,
  markLegacyImported,
  readBrowserLegacy,
  summarizeLegacy,
  wasLegacyImported,
} from '../utils/legacyData'
import { exportAllData, hasRecords, replaceAllData, subscribe } from '../utils/storage'
import { adoptLegacyTimer } from '../utils/timerStore'

export default function LegacyImport() {
  const { user, importNotice, clearImportNotice } = useAuth()
  const [legacy, setLegacy] = useState(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [localNotice, setLocalNotice] = useState('')
  const doneMessage = localNotice || importNotice || ''

  useEffect(() => {
    function refresh() {
      if (wasLegacyImported(user.id)) {
        setLegacy(null)
        return
      }
      const data = readBrowserLegacy()
      if (!data || hasRecords(exportAllData())) {
        setLegacy(null)
        return
      }
      setLegacy(data)
    }

    const timer = setTimeout(refresh, 0)
    const unsub = subscribe(refresh)
    return () => {
      clearTimeout(timer)
      unsub()
    }
  }, [user.id])

  useEffect(() => {
    let alive = true
    const timer = setTimeout(async () => {
      if (wasLegacyImported(user.id)) return
      if (hasRecords(exportAllData())) return
      if (!readBrowserLegacy()) return
      try {
        const result = await adoptBrowserDataIfNeeded(user.id)
        if (!alive || !result.imported) return
        setLegacy(null)
        setLocalNotice(
          summarizeLegacy(result.summary)
            ? `داده‌های قبلی مرورگر وارد حساب شد (${summarizeLegacy(result.summary)}).`
            : 'داده‌های قبلی مرورگر وارد حساب شد.',
        )
      } catch {
        return
      }
    }, 0)
    return () => {
      alive = false
      clearTimeout(timer)
    }
  }, [user.id])

  if (doneMessage) {
    return (
      <section className="legacy-banner is-ok" role="status">
        <div>
          <h2>وارد شد</h2>
          <p className="card-copy">{doneMessage}</p>
        </div>
        <div className="button-row">
          <button
            type="button"
            className="button"
            onClick={() => {
              setLocalNotice('')
              clearImportNotice?.()
            }}
          >
            باشه
          </button>
        </div>
      </section>
    )
  }

  if (!legacy) return null

  const summary = summarizeLegacy(legacy)

  async function importNow() {
    setBusy(true)
    setError('')
    try {
      await replaceAllData(legacy)
      adoptLegacyTimer(user.id)
      markLegacyImported(user.id)
      setLegacy(null)
      setLocalNotice(`داده‌های قبلی مرورگر وارد حساب شد (${summary}).`)
    } catch (failure) {
      setError(failure.message || 'وارد نشد. دوباره امتحان کن.')
      setBusy(false)
    }
  }

  function skip() {
    markLegacyHandled(user.id)
    setLegacy(null)
  }

  return (
    <section className="legacy-banner" role="status">
      <div>
        <h2>داده‌های قبلی این مرورگر پیدا شد</h2>
        <p className="card-copy">{summary}. به حساب {user.username} اضافه شود؟</p>
        {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
      </div>
      <div className="button-row">
        <button type="button" className="button button-primary" disabled={busy} onClick={importNow}>
          {busy ? 'کمی صبر کن' : 'وارد کردن'}
        </button>
        <button type="button" className="button" disabled={busy} onClick={skip}>
          لازم نیست
        </button>
      </div>
    </section>
  )
}
