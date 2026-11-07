import { useState } from 'react'
import { clearAllData } from '../../utils/storage'
import { clearTimer } from '../../utils/timerStore'

const TEST_DRAFT_KEY = 'konkoor-help-test-draft'

export default function ClearData() {
  const [confirming, setConfirming] = useState(false)
  const [done, setDone] = useState(false)

  function wipe() {
    clearAllData()
    clearTimer()
    setConfirming(false)
    setDone(true)
    try {
      sessionStorage.removeItem(TEST_DRAFT_KEY)
    } catch {
      return
    }
  }

  return (
    <section className="card" id="clear-data">
      <h2>حذف همه داده‌ها</h2>
      <p className="card-copy">
        جلسه‌ها، آزمون‌ها، اشتباهات، مبحث‌ها، کارها، برنامه، درس‌های اضافه، تنظیمات و زمان‌سنج پاک می‌شوند.
        تاریخ کنکور به 2028-07-01 برمی‌گردد. فقط داده‌های حساب خودت پاک می‌شود.
        اگر فایل پشتیبان می‌خواهی، اول یک پشتیبان دانلود کن.
      </p>
      {confirming ? (
        <div className="button-row">
          <button type="button" className="button button-danger is-solid" onClick={wipe}>
            تایید حذف
          </button>
          <button type="button" className="button" onClick={() => setConfirming(false)}>
            انصراف
          </button>
        </div>
      ) : (
        <div className="button-row">
          <button
            type="button"
            className="button button-danger"
            onClick={() => {
              setDone(false)
              setConfirming(true)
            }}
          >
            حذف همه داده‌ها
          </button>
        </div>
      )}
      {done ? (
        <p className="form-message" role="status">همه داده‌ها پاک شد.</p>
      ) : null}
    </section>
  )
}
