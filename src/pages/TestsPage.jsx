import { useEffect, useRef, useState } from 'react'
import AnswerSheet from '../components/tests/AnswerSheet'
import ExamNav from '../components/tests/ExamNav'
import ManualTestForm from '../components/tests/ManualTestForm'
import MistakeLogger from '../components/tests/MistakeLogger'
import QuestionCapture from '../components/tests/QuestionCapture'
import SessionSaveForm from '../components/tests/SessionSaveForm'
import TestHistory from '../components/tests/TestHistory'
import TestResult from '../components/tests/TestResult'
import { useCollection } from '../hooks/useCollection'
import { todayInputValue } from '../utils/clock'
import { addOne, deleteOne, updateOne } from '../utils/storage'
import { subjectFromForm } from '../utils/subjects'
import { addAnswer, buildSession, normalizeQuestions, removeAnswer, setAnswerMark } from '../utils/testRecord'
import './TestsPage.css'

const DRAFT_KEY = 'konkoor-help-test-draft'

function freshMeta() {
  return {
    date: todayInputValue(),
    subject: '',
    topic: '',
    source: '',
    duration: '',
  }
}

function emptyDraft() {
  return {
    editingId: null,
    questions: [],
    meta: freshMeta(),
  }
}

function readDraft() {
  try {
    const raw = sessionStorage.getItem(DRAFT_KEY)
    if (!raw) return null
    const data = JSON.parse(raw)
    if (!data || typeof data !== 'object') return null
    const meta = data.meta && typeof data.meta === 'object' ? data.meta : {}
    return {
      editingId: typeof data.editingId === 'string' ? data.editingId : null,
      questions: normalizeQuestions(data.questions),
      meta: {
        date: typeof meta.date === 'string' && meta.date ? meta.date : todayInputValue(),
        subject: typeof meta.subject === 'string' ? meta.subject : '',
        topic: typeof meta.topic === 'string' ? meta.topic : '',
        source: typeof meta.source === 'string' ? meta.source : '',
        duration: typeof meta.duration === 'string' ? meta.duration : '',
      },
    }
  } catch {
    return null
  }
}

export default function TestsPage() {
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const [draft, setDraft] = useState(() => readDraft() || emptyDraft())
  const [activeId, setActiveId] = useState(null)
  const [replaceId, setReplaceId] = useState(null)
  const [manualId, setManualId] = useState(null)
  const [entryMode, setEntryMode] = useState('sheet')
  const [error, setError] = useState('')
  const resultRef = useRef(null)
  const pendingScroll = useRef(false)
  const editing = tests.find((test) => test.id === draft.editingId) || null
  const manualTest = tests.find((test) => test.id === manualId) || null
  const active = tests.find((test) => test.id === activeId) || null

  useEffect(() => {
    sessionStorage.setItem(DRAFT_KEY, JSON.stringify(draft))
  }, [draft])

  useEffect(() => {
    if (!pendingScroll.current || !active || editing) return
    pendingScroll.current = false
    resultRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [active, editing])

  useEffect(() => {
    if (!manualId) return
    document.getElementById('manual-test')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [manualId])

  function applyTest(test) {
    setReplaceId(null)
    setManualId(null)
    setError('')
    setActiveId(null)
    setDraft({
      editingId: test.id,
      questions: normalizeQuestions(test.questions),
      meta: {
        date: test.date || todayInputValue(),
        subject: test.subject || '',
        topic: test.topic || '',
        source: test.source || '',
        duration: test.durationMinutes ? String(test.durationMinutes) : '',
      },
    })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function handleAdd(number, choice) {
    const result = addAnswer(draft.questions, number, choice)
    if (result.error) return result
    setDraft((current) => ({ ...current, questions: result.questions }))
    setError('')
    return result
  }

  function handleMeta(field, value) {
    setDraft((current) => ({ ...current, meta: { ...current.meta, [field]: value } }))
    setError('')
  }

  function handleSubmit(event) {
    event.preventDefault()
    const subject = subjectFromForm(event.currentTarget)
    if (subject.error) {
      setError(subject.error)
      return
    }
    const result = buildSession({ ...draft.meta, subject: subject.id }, draft.questions)
    if (result.error) {
      setError(result.error)
      return
    }

    if (draft.editingId) {
      const saved = updateOne('tests', draft.editingId, result.test)
      if (!saved) {
        setDraft((current) => ({ ...current, editingId: null }))
        setError('این آزمون دیگر در فهرست نیست. دوباره ذخیره کن تا به‌عنوان آزمون جدید اضافه شود.')
        return
      }
      pendingScroll.current = true
      setActiveId(saved.id)
      setDraft((current) => ({
        ...current,
        editingId: null,
        questions: [],
        meta: { ...current.meta, topic: '', duration: '' },
      }))
      setError('')
      return
    }

    const saved = addOne('tests', result.test)
    pendingScroll.current = true
    setActiveId(saved.id)
    setDraft((current) => ({
      ...current,
      questions: [],
      meta: { ...current.meta, topic: '', duration: '' },
    }))
    setError('')
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>آزمون‌ها</h1>
        <p>پاسخ‌نامه برای ثبت سوال‌به‌سوال است. ثبت دستی فقط جمع درست، غلط و سفید را می‌خواهد.</p>
        <ExamNav />
      </header>
      <div className="stack">
        <section className="card test-count-card">
          <strong>{tests.length}</strong>
          <span>آزمون ثبت‌شده</span>
        </section>
        <div className="segment" role="group" aria-label="روش ثبت">
          <button
            type="button"
            className={entryMode === 'sheet' ? 'is-active' : undefined}
            aria-pressed={entryMode === 'sheet'}
            onClick={() => {
              setEntryMode('sheet')
              setManualId(null)
            }}
          >
            پاسخ‌نامه
          </button>
          <button
            type="button"
            className={entryMode === 'manual' ? 'is-active' : undefined}
            aria-pressed={entryMode === 'manual'}
            onClick={() => setEntryMode('manual')}
          >
            ثبت دستی
          </button>
        </div>
        <div hidden={entryMode !== 'manual'}>
          <ManualTestForm
            key={manualTest?.id || 'new-manual'}
            test={manualTest}
            onCancel={() => {
              setManualId(null)
              setEntryMode('sheet')
            }}
            onSaved={(saved) => {
              pendingScroll.current = true
              setActiveId(saved.id)
              setManualId(null)
              setError('')
            }}
          />
        </div>
        <section className="card" hidden={entryMode !== 'sheet'}>
          <h2>{editing ? 'ویرایش آزمون' : 'ثبت پاسخ‌ها'}</h2>
          <p className="card-copy">
            شماره چاپ‌شده روی سوال را بنویس، بعد گزینه را بزن. ترتیب مهم نیست: ۱ و ۲ و ۳، یا ۲ و ۴ و ۶، یا هر شماره دیگری.
          </p>
          {replaceId ? (
            <div className="replace-note">
              <p>این کار پاسخ‌هایی را که هنوز ذخیره نکردی عوض می‌کند.</p>
              <div className="button-row">
                <button
                  type="button"
                  className="button button-primary"
                  onClick={() => {
                    const test = tests.find((item) => item.id === replaceId)
                    if (test) applyTest(test)
                  }}
                >
                  جایگزینی فهرست
                </button>
                <button type="button" className="button" onClick={() => setReplaceId(null)}>
                  انصراف
                </button>
              </div>
            </div>
          ) : null}
          <QuestionCapture key={draft.editingId || activeId || 'new'} onAdd={handleAdd} />
          <AnswerSheet
            questions={draft.questions}
            onMark={(number, mark) => {
              setDraft((current) => ({ ...current, questions: setAnswerMark(current.questions, number, mark) }))
              setError('')
            }}
            onRemove={(number) => {
              setDraft((current) => ({ ...current, questions: removeAnswer(current.questions, number) }))
              setError('')
            }}
          />
        </section>
        {entryMode === 'sheet' && draft.questions.length > 0 ? (
          <SessionSaveForm
            meta={draft.meta}
            editing={Boolean(draft.editingId)}
            questions={draft.questions}
            error={error}
            onChange={handleMeta}
            onSubmit={handleSubmit}
            onCancel={() => {
              setDraft(emptyDraft())
              setError('')
              setReplaceId(null)
            }}
          />
        ) : null}
        {active && !draft.editingId ? (
          <div className="result-block" ref={resultRef}>
            <TestResult test={active} />
            <MistakeLogger key={active.id} test={active} mistakes={mistakes} />
          </div>
        ) : null}
        <TestHistory
          tests={tests}
          activeId={activeId}
          editingId={manualId || draft.editingId}
          onDelete={(test) => {
            mistakes
              .filter((mistake) => mistake.testId === test.id)
              .forEach((mistake) => deleteOne('mistakes', mistake.id))
            deleteOne('tests', test.id)
            if (activeId === test.id) setActiveId(null)
            if (draft.editingId === test.id) {
              setDraft(emptyDraft())
              setError('')
              setReplaceId(null)
            }
            if (manualId === test.id) setManualId(null)
          }}
          onEdit={(test) => {
            if (test.entry === 'manual') {
              if (manualId === test.id) return
              setEntryMode('manual')
              setManualId(test.id)
              return
            }
            setEntryMode('sheet')
            if (draft.editingId === test.id) return
            if (draft.questions.length > 0 && draft.editingId !== test.id) {
              setReplaceId(test.id)
              window.scrollTo({ top: 0, behavior: 'smooth' })
              return
            }
            applyTest(test)
          }}
          onActivate={(id) => {
            if (id === activeId && !draft.editingId) {
              resultRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
              return
            }
            pendingScroll.current = true
            setActiveId(id)
          }}
        />
      </div>
    </div>
  )
}
