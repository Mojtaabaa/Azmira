// @ts-nocheck
import { useSettings } from '../../hooks/useSettings'
import DatePicker from '../DatePicker'
import { isRealDateString } from '../../utils/clock'
import { konkoorDay, OFFICIAL_KONKOOR_DAY } from '../../utils/konkoor'
import { updateSettings } from '../../utils/storage'
import { formatJalali } from '../../utils/week'

export default function KonkoorDateForm() {
  const settings = useSettings()
  const day = konkoorDay(settings)

  function save(value) {
    if (!value) {
      if (settings.konkoorDate) updateSettings({ konkoorDate: '' })
      return
    }
    if (!isRealDateString(value) || value === settings.konkoorDate) return
    updateSettings({ konkoorDate: value })
  }

  return (
    <section className="card" id="konkoor-date">
      <h2>تاریخ کنکور</h2>
      <p className="card-copy">
        ساعت شروع 08:00 به وقت تهران است. تاریخ پیش‌فرض {OFFICIAL_KONKOOR_DAY} است
        {' '}
        ({formatJalali(OFFICIAL_KONKOOR_DAY, true)}).
      </p>
      <form className="length-form date-field" noValidate onSubmit={(event) => event.preventDefault()}>
        <label>
          <span>تاریخ</span>
          <DatePicker variant="field" quick={false} value={day} onChange={save} />
          <span />
        </label>
      </form>
    </section>
  )
}
