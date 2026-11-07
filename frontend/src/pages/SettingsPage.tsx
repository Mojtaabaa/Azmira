// @ts-nocheck
import { useEffect } from 'react'
import DataBackup from '../components/DataBackup'
import BrowserImport from '../components/settings/BrowserImport'
import ClearData from '../components/settings/ClearData'
import DailyGoalForm from '../components/settings/DailyGoalForm'
import KonkoorDateForm from '../components/settings/KonkoorDateForm'
import SubjectNames from '../components/settings/SubjectNames'
import ThemeSettings from '../components/settings/ThemeSettings'
import TimerLengthForm from '../components/settings/TimerLengthForm'
import WeightForm from '../components/settings/WeightForm'
import './SettingsPage.css'

export default function SettingsPage() {
  useEffect(() => {
    const id = window.location.hash.replace('#', '')
    if (!id) return
    document.getElementById(id)?.scrollIntoView({ block: 'start' })
  }, [])

  return (
    <section className="page">
      <header className="page-header">
        <h1>تنظیمات</h1>
        <p>ظاهر، زمان‌سنج، هدف‌ها، تاریخ کنکور، درس‌ها و پاک کردن حساب. گرفتن و برگرداندن فایل پشتیبان در انتقال داده است.</p>
      </header>
      <div className="stack">
        <ThemeSettings />
        <TimerLengthForm />
        <DailyGoalForm />
        <KonkoorDateForm />
        <SubjectNames />
        <WeightForm />
        <BrowserImport />
        <DataBackup />
        <ClearData />
      </div>
    </section>
  )
}
