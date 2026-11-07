// @ts-nocheck
import { useState } from 'react'
import { getSubjectById } from '../data/subjects'
import { useAuth } from '../hooks/useAuth'
import { useCollection } from '../hooks/useCollection'
import { backupCounts, downloadAccountExport, importAccountBackup } from '../utils/accountTransfer'
import { downloadText, stamp, tableToCsv } from '../utils/csv'
import { formatMinutes } from '../utils/clock'
import { konkoorPercentage } from '../utils/score'
import { buildWeeklyReport, downloadWeeklyReportPng } from '../utils/weeklyReport'
import './DataPage.css'

const COUNT_LABELS = [
  ['sessions', 'جلسه مطالعه'],
  ['tests', 'آزمون'],
  ['mistakes', 'اشتباه'],
  ['topics', 'مبحث'],
  ['tasks', 'کار'],
  ['events', 'رویداد'],
  ['notes', 'یادداشت'],
  ['images', 'تصویر'],
]

export default function DataPage() {
  const { user } = useAuth()
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const topics = useCollection('topics')
  const tasks = useCollection('tasks')
  const events = useCollection('events')
  const [preview, setPreview] = useState(null)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [dragOver, setDragOver] = useState(false)

  const owned = [
    ['جلسه', sessions.length],
    ['آزمون', tests.length],
    ['اشتباه', mistakes.length],
    ['مبحث', topics.length],
    ['کار', tasks.length],
    ['رویداد', events.length],
  ]

  async function exportJson() {
    setError('')
    setMessage('')
    setBusy(true)
    try {
      await downloadAccountExport()
      setMessage('فایل کامل حساب دانلود شد. مطالعه، تنظیمات، یادداشت و تصویرها داخل آن است.')
    } catch (failure) {
      setError(failure.message || 'خروجی گرفته نشد.')
    } finally {
      setBusy(false)
    }
  }

  function exportCsv(kind) {
    setError('')
    const day = stamp()
    if (kind === 'sessions') {
      downloadText(`sessions-${day}.csv`, tableToCsv([
        { label: 'شروع', value: (row) => row.startTime || '' },
        { label: 'درس', value: (row) => getSubjectById(row.subject)?.name || row.subject || '' },
        { label: 'مبحث', value: (row) => row.topic || '' },
        { label: 'دقیقه', value: (row) => row.durationMinutes || 0 },
      ], sessions))
    } else if (kind === 'tests') {
      downloadText(`tests-${day}.csv`, tableToCsv([
        { label: 'تاریخ', value: (row) => row.date || '' },
        { label: 'درس', value: (row) => getSubjectById(row.subject)?.name || row.subject || '' },
        { label: 'مبحث', value: (row) => row.topic || '' },
        { label: 'درست', value: (row) => row.correct ?? '' },
        { label: 'غلط', value: (row) => row.wrong ?? '' },
        { label: 'کل', value: (row) => row.totalQuestions ?? '' },
        { label: 'درصد', value: (row) => konkoorPercentage(row.correct, row.wrong, row.totalQuestions) ?? '' },
      ], tests))
    } else {
      downloadText(`tasks-${day}.csv`, tableToCsv([
        { label: 'عنوان', value: (row) => row.title || '' },
        { label: 'درس', value: (row) => getSubjectById(row.subject)?.name || row.subject || '' },
        { label: 'موعد', value: (row) => row.dueDate || '' },
        { label: 'انجام', value: (row) => (row.done ? 'بله' : 'خیر') },
      ], tasks))
    }
    setMessage('فایل CSV دانلود شد و در اکسل باز می‌شود.')
  }

  async function exportPoster() {
    setError('')
    try {
      const report = buildWeeklyReport({ sessions, tests, mistakes })
      await downloadWeeklyReportPng(report, { fileName: `week-${stamp()}.png` })
      setMessage('پوستر این هفته دانلود شد.')
    } catch (failure) {
      setError(failure.message || 'پوستر ساخته نشد.')
    }
  }

  async function readFile(file) {
    setMessage('')
    setError('')
    setPreview(null)
    if (!file) return
    let parsed
    try {
      parsed = JSON.parse(await file.text())
    } catch {
      setError('این فایل JSON معتبر نیست.')
      return
    }
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      setError('ساختار فایل پشتیبان شناخته نشد.')
      return
    }
    setPreview({ name: file.name, backup: parsed, counts: backupCounts(parsed) })
  }

  async function commit(mode) {
    if (!preview || busy) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await importAccountBackup(preview.backup, mode, user.id)
      setPreview(null)
      setMessage(mode === 'merge' ? 'داده‌های فایل با حساب ادغام شد.' : 'حساب با فایل جایگزین شد.')
    } catch (failure) {
      setError(failure.message || 'وارد کردن انجام نشد.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="page is-wide">
      <header className="page-header">
        <p className="eyebrow">حساب {user.username}</p>
        <h1>انتقال داده</h1>
        <p>یک خروجی کامل از مطالعه، تنظیمات و یادداشت‌ها بگیر، یا جدول‌ها را برای اکسل جدا کن. وارد کردن هم می‌تواند جایگزین شود و هم به داده‌های فعلی اضافه شود.</p>
      </header>

      <ul className="data-owned" aria-label="موجودی حساب">
        {owned.map(([label, count]) => (
          <li key={label}>
            <strong dir="ltr">{count}</strong>
            <span>{label}</span>
          </li>
        ))}
      </ul>

      <div className="data-layout">
        <section className="card data-panel">
          <h2>خروجی</h2>
          <p className="card-copy">فایل JSON برای برگرداندن کل حساب است. CSV فقط برای دیدن و حساب‌کتاب در اکسل است و دوباره وارد برنامه نمی‌شود.</p>
          <div className="button-row">
            <button type="button" className="button button-primary" disabled={busy} onClick={exportJson}>
              دانلود پشتیبان کامل
            </button>
            <button type="button" className="button" onClick={() => exportCsv('sessions')}>CSV جلسه‌ها</button>
            <button type="button" className="button" onClick={() => exportCsv('tests')}>CSV آزمون‌ها</button>
            <button type="button" className="button" onClick={() => exportCsv('tasks')}>CSV کارها</button>
            <button type="button" className="button" onClick={exportPoster}>پوستر هفته</button>
          </div>
          <p className="data-hint">این هفته {formatMinutes(buildWeeklyReport({ sessions, tests, mistakes }).totalMinutes)} مطالعه ثبت شده است.</p>
        </section>

        <section className="card data-panel">
          <h2>وارد کردن</h2>
          <p className="card-copy">فایل پشتیبان قدیمی هم پذیرفته می‌شود. فایل قدیمی یادداشت ندارد و یادداشت‌های فعلی را پاک نمی‌کند.</p>
          <label
            className={dragOver ? 'dropzone is-hot' : 'dropzone'}
            onDragOver={(event) => {
              event.preventDefault()
              setDragOver(true)
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={(event) => {
              event.preventDefault()
              setDragOver(false)
              readFile(event.dataTransfer.files?.[0])
            }}
          >
            <span>فایل JSON را اینجا رها کن یا انتخاب کن</span>
            <input
              type="file"
              accept="application/json,.json"
              onChange={(event) => {
                readFile(event.target.files?.[0])
                event.target.value = ''
              }}
            />
          </label>

          {preview ? (
            <div className="import-preview">
              <p>
                <strong>{preview.name}</strong>
                {preview.counts.legacy ? ' · پشتیبان قدیمی، بدون یادداشت' : ' · پشتیبان کامل'}
              </p>
              <ul>
                {COUNT_LABELS.map(([key, label]) => (
                  <li key={key}>
                    <span>{label}</span>
                    <strong dir="ltr">{preview.counts[key]}</strong>
                  </li>
                ))}
              </ul>
              <div className="button-row">
                <button type="button" className="button button-primary" disabled={busy} onClick={() => commit('merge')}>
                  ادغام با حساب
                </button>
                <button type="button" className="button button-danger" disabled={busy} onClick={() => commit('replace')}>
                  جایگزینی کامل
                </button>
                <button type="button" className="button" disabled={busy} onClick={() => setPreview(null)}>
                  انصراف
                </button>
              </div>
              <p className="data-hint">ادغام موردهای هم‌شناسه را تازه می‌کند. جایگزینی مطالعه را پاک می‌کند و اگر فایل یادداشت داشته باشد، یادداشت‌ها را هم عوض می‌کند.</p>
            </div>
          ) : null}
        </section>
      </div>

      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
      {message ? <p className="form-message is-ok" role="status">{message}</p> : null}
    </section>
  )
}
