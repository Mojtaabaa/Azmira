// @ts-nocheck
import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './LearningIntelligence.css'

export default function AiCoachPage() {
  const { status, user, workspaces, activeWorkspaceId } = useAuth()
  const [settings, setSettings] = useState(null)
  const [analysis, setAnalysis] = useState(null)
  const [report, setReport] = useState(null)
  const [plan, setPlan] = useState(null)
  const [availableMinutes, setAvailableMinutes] = useState(120)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [chatInput, setChatInput] = useState('')
  const [threadId, setThreadId] = useState(null)
  const [chatTurns, setChatTurns] = useState([])

  const active = (workspaces || []).find((item) => item.id === activeWorkspaceId)
  const canAdmin =
    user?.platformRole === 100 ||
    active?.role === 'schoolOwner' ||
    active?.role === 'schoolAdministrator' ||
    active?.type === 'personal'

  const loadSettings = useCallback(async () => {
    try {
      setSettings(await request('GET', '/ai/settings'))
    } catch (err) {
      setError(err.message || 'بارگذاری تنظیمات AI ممکن نشد.')
    }
  }, [])

  useEffect(() => {
    if (status === 'authed') void loadSettings()
  }, [status, loadSettings])

  async function saveSettings(next) {
    setBusy(true)
    try {
      setMessage('')
      setSettings(await request('PUT', '/ai/settings', next))
      setMessage('تنظیمات ذخیره شد.')
    } catch (err) {
      setError(err.message || 'ذخیره ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function runAnalyze() {
    setBusy(true)
    try {
      setError('')
      setAnalysis(await request('POST', '/ai/coach/analyze', {}))
    } catch (err) {
      setError(err.message || 'تحلیل ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function runReport() {
    setBusy(true)
    try {
      setError('')
      setReport(await request('POST', '/ai/report/weekly', {}))
    } catch (err) {
      setError(err.message || 'گزارش ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function runPlan() {
    setBusy(true)
    try {
      setError('')
      setPlan(await request('POST', '/ai/planner/suggest', { availableMinutes: Number(availableMinutes) }))
    } catch (err) {
      setError(err.message || 'برنامه‌ریزی ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  async function sendChat(event) {
    event.preventDefault()
    if (!chatInput.trim()) return
    const userText = chatInput.trim()
    setChatInput('')
    setChatTurns((prev) => [...prev, { role: 'user', content: userText }])
    setBusy(true)
    try {
      setError('')
      const payload = await request('POST', '/coach/chat', {
        message: userText,
        threadId: threadId || undefined,
      })
      setThreadId(payload.threadId)
      setChatTurns((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: payload.reply,
          confidence: payload.confidence,
          evidenceRefs: payload.evidenceRefs || [],
          provider: payload.provider,
        },
      ])
    } catch (err) {
      setError(err.message || 'گفتگو ممکن نشد.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="learn-page">
      <header className="learn-hero">
        <div>
          <p className="learn-kicker">مربی هوشمند</p>
          <h1>AI Coach</h1>
          <p className="learn-sub">فقط داده مجاز فضای کاری · پیشنهادها نیازمند تأیید · حقایق از AI جدا هستند.</p>
        </div>
      </header>

      {error ? <p className="learn-error">{error}</p> : null}
      {message ? <p className="learn-ok">{message}</p> : null}

      {canAdmin && settings ? (
        <section className="learn-panel">
          <h2>تنظیمات (مدیر)</h2>
          <label className="learn-check">
            <input
              type="checkbox"
              checked={!!settings.enabled}
              disabled={busy}
              onChange={(e) => saveSettings({ ...settings, enabled: e.target.checked })}
            />
            فعال‌سازی AI برای این فضا
          </label>
          <div className="learn-row">
            <select
              value={settings.providerCode || 'heuristic'}
              onChange={(e) => saveSettings({ ...settings, providerCode: e.target.value })}
              disabled={busy}
            >
              {(settings.availableProviders || ['heuristic', 'null', 'openai']).map((code) => (
                <option key={code} value={code}>
                  {code}
                </option>
              ))}
            </select>
            <input
              value={settings.model || ''}
              onChange={(e) => setSettings((prev) => ({ ...prev, model: e.target.value }))}
              onBlur={() => saveSettings(settings)}
              placeholder="model (اختیاری)"
            />
          </div>
        </section>
      ) : null}

      <section className="learn-panel">
        <h2>گفتگو با مربی</h2>
        <p className="learn-note">هر پاسخ با confidence و evidenceRefs می‌آید؛ بدون کلید LLM هم مسیر heuristic فعال است.</p>
        <ul className="learn-list">
          {chatTurns.map((turn, index) => (
            <li key={`${turn.role}-${index}`}>
              <div>
                <strong>{turn.role === 'user' ? 'تو' : 'مربی'}</strong>
                <span>{turn.content}</span>
                {turn.confidence ? (
                  <span className="muted">
                    {' '}
                    · سطح شواهد: {turn.confidence} (نه قطعیت)
                    {turn.provider ? ` · ${turn.provider}` : ''}
                  </span>
                ) : null}
                {turn.evidenceRefs?.length ? (
                  <pre className="facts-box">{JSON.stringify(turn.evidenceRefs, null, 2)}</pre>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
        <form className="learn-row" onSubmit={sendChat}>
          <input
            value={chatInput}
            onChange={(e) => setChatInput(e.target.value)}
            placeholder="سوالت درباره یادگیری…"
            disabled={busy}
            style={{ flex: 1 }}
          />
          <button type="submit" disabled={busy || !chatInput.trim()}>
            ارسال
          </button>
        </form>
      </section>

      <section className="learn-panel">
        <h2>اقدامات ساخت‌یافته</h2>
        <div className="learn-actions">
          <button type="button" disabled={busy} onClick={runAnalyze}>
            تحلیل هفتگی
          </button>
          <button type="button" disabled={busy} onClick={runReport}>
            گزارش هفتگی
          </button>
          <input
            type="number"
            min={30}
            value={availableMinutes}
            onChange={(e) => setAvailableMinutes(e.target.value)}
            style={{ width: 100 }}
          />
          <button type="button" disabled={busy} onClick={runPlan}>
            پیشنهاد برنامه
          </button>
        </div>
      </section>

      {analysis ? (
        <section className="learn-panel">
          <h2>تحلیل مربی</h2>
          <p className="learn-note">{analysis.note}</p>
          <pre className="facts-box">{JSON.stringify(analysis.facts, null, 2)}</pre>
          <p>{typeof analysis.analysis === 'string' ? analysis.analysis : JSON.stringify(analysis.analysis)}</p>
          <ul className="learn-list">
            {(analysis.recommendations || []).map((item) => (
              <li key={item.id}>
                <div>
                  <strong>{item.title}</strong>
                  <span className="muted">{item.body}</span>
                </div>
                <div className="learn-actions">
                  <button
                    type="button"
                    onClick={async () => {
                      await request('POST', `/ai/recommendations/${item.id}/resolve`, { approve: true })
                      setMessage(`پیشنهاد «${item.title}» تأیید شد.`)
                    }}
                  >
                    تأیید
                  </button>
                  <button
                    type="button"
                    onClick={async () => {
                      await request('POST', `/ai/recommendations/${item.id}/resolve`, { approve: false })
                      setMessage(`پیشنهاد «${item.title}» رد شد.`)
                    }}
                  >
                    رد
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {report ? (
        <section className="learn-panel">
          <h2>گزارش {report.periodKey}</h2>
          <p className="learn-note">{report.note || 'facts واقعی · narrative پیشنهادی AI'}</p>
          <pre className="facts-box">{JSON.stringify(report.facts, null, 2)}</pre>
          <p className="narrative">{report.narrative}</p>
        </section>
      ) : null}

      {plan ? (
        <section className="learn-panel">
          <h2>{plan.title}</h2>
          <p className="learn-note">{plan.note}</p>
          <pre className="facts-box">{JSON.stringify(plan.schedule, null, 2)}</pre>
          <div className="learn-actions">
            <button
              type="button"
              onClick={async () => {
                const resolved = await request('POST', `/ai/planner/${plan.id}/resolve`, {
                  approve: true,
                  schedule: plan.schedule,
                })
                setPlan({ ...plan, status: resolved.status })
                setMessage('برنامه تأیید شد.')
              }}
            >
              تأیید برنامه
            </button>
            <button
              type="button"
              onClick={async () => {
                await request('POST', `/ai/planner/${plan.id}/resolve`, { approve: false })
                setMessage('برنامه رد شد.')
              }}
            >
              رد
            </button>
          </div>
        </section>
      ) : null}
    </div>
  )
}
