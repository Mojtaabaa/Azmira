import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import CalendarGrid from '../components/planner/CalendarGrid'
import EventForm from '../components/planner/EventForm'
import SubjectLegend from '../components/planner/SubjectLegend'
import UpcomingExams from '../components/planner/UpcomingExams'
import WeekNav from '../components/planner/WeekNav'
import { getSubjectById } from '../data/subjects'
import { useCollection } from '../hooks/useCollection'
import { todayInputValue } from '../utils/clock'
import {
  DEFAULT_EVENT_MINUTES,
  buildEvent,
  formatMinutesAsTime,
  shiftTimes,
  upcomingExams,
} from '../utils/events'
import { subjectFromForm } from '../utils/subjects'
import { addOne, deleteOne, updateOne } from '../utils/storage'
import { startTimerFromEvent } from '../utils/timerPlanner'
import { selectPhase, setTimerTarget } from '../utils/timerStore'
import { daysOfWeek, formatJalali, saturdayOf, shiftDay } from '../utils/week'
import './PlannerPage.css'

const EMPTY_FORM = {
  id: null,
  title: '',
  type: 'study',
  subject: '',
  date: '',
  startTime: '',
  endTime: '',
}

export default function PlannerPage() {
  const navigate = useNavigate()
  const events = useCollection('events')
  const today = todayInputValue()
  const [anchor, setAnchor] = useState(() => saturdayOf(today))
  const [draft, setDraft] = useState(null)
  const [dropTarget, setDropTarget] = useState(null)
  const [error, setError] = useState('')
  const [clock, setClock] = useState(() => new Date())
  const days = daysOfWeek(anchor)
  const exams = upcomingExams(events, today)
  const nowMinutes = clock.getHours() * 60 + clock.getMinutes()
  const weekLabel = `${days[0].name} ${formatJalali(days[0].date)} تا ${days[6].name} ${formatJalali(days[6].date)}`

  useEffect(() => {
    const id = setInterval(() => setClock(new Date()), 60000)
    return () => clearInterval(id)
  }, [])

  function changeForm(field, value) {
    setDraft((current) => (current ? { ...current, [field]: value } : current))
    setError('')
  }

  function closeDraft() {
    setDraft(null)
    setError('')
  }

  function openCreate(date, startMinutes, endMinutes) {
    const timed = startMinutes != null && Number.isFinite(startMinutes)
    const start = timed ? formatMinutesAsTime(startMinutes) : ''
    const end = timed
      ? formatMinutesAsTime(
          endMinutes != null && endMinutes > startMinutes
            ? endMinutes
            : Math.min(startMinutes + DEFAULT_EVENT_MINUTES, 23 * 60 + 59),
        )
      : ''
    setDraft({
      ...EMPTY_FORM,
      date,
      startTime: start,
      endTime: end,
    })
    setError('')
  }

  function openQuickStudy(subjectId, slot) {
    const subject = getSubjectById(subjectId)
    const minutes = slot.allday || slot.minutes === '' ? null : Number(slot.minutes)
    const start = minutes == null ? '' : formatMinutesAsTime(minutes)
    const endMinutes = minutes == null ? null : Math.min(minutes + DEFAULT_EVENT_MINUTES * 2, 23 * 60 + 59)
    setDropTarget(null)
    setDraft({
      ...EMPTY_FORM,
      title: subject ? `مطالعه ${subject.name}` : 'مطالعه',
      type: 'study',
      subject: subjectId,
      date: slot.date,
      startTime: start,
      endTime: endMinutes == null ? '' : formatMinutesAsTime(endMinutes),
      askTitle: true,
    })
    setError('')
  }

  function openEdit(item) {
    setDraft({
      id: item.id,
      title: item.title || '',
      type: item.type || 'study',
      subject: item.subject || '',
      date: item.date || '',
      startTime: item.startTime || '',
      endTime: item.endTime || '',
    })
    if (item.date) setAnchor(saturdayOf(item.date))
    setError('')
  }

  function handleSubmit(event) {
    event.preventDefault()
    if (!draft) return
    const subject = subjectFromForm(event.currentTarget, { optional: true })
    if (subject.error) {
      setError(subject.error)
      return
    }
    const result = buildEvent({ ...draft, subject: subject.id })
    if (result.error) {
      setError(result.error)
      return
    }

    const current = draft.id ? events.find((item) => item.id === draft.id) : null
    if (draft.id && !current) {
      closeDraft()
      return
    }

    try {
      if (current) {
        const saved = updateOne('events', current.id, result.event)
        if (!saved) {
          closeDraft()
          return
        }
      } else {
        addOne('events', { ...result.event, done: false })
      }
    } catch (saveError) {
      setError(saveError.message || 'این برنامه ذخیره نشد.')
      return
    }

    setAnchor(saturdayOf(result.event.date))
    closeDraft()
  }

  function toggleDone() {
    if (!draft?.id) return
    const current = events.find((item) => item.id === draft.id)
    if (!current) return
    updateOne('events', current.id, { done: !current.done })
  }

  function removeEvent() {
    if (!draft?.id) return
    deleteOne('events', draft.id)
    closeDraft()
  }

  function moveEvent(id, date, minutes) {
    const current = events.find((item) => item.id === id)
    if (!current) return
    const next = minutes == null
      ? { date, startTime: '', endTime: '' }
      : { date, ...shiftTimes(current, minutes) }
    if (
      current.date === next.date
      && (current.startTime || '') === next.startTime
      && (current.endTime || '') === next.endTime
    ) return
    updateOne('events', id, next)
    setDraft((value) => (value?.id === id ? { ...value, ...next } : value))
  }

  function resizeEvent(id, startTime, endTime) {
    const current = events.find((item) => item.id === id)
    if (!current || !startTime || !endTime) return
    if (current.startTime === startTime && current.endTime === endTime) return
    const next = { startTime, endTime }
    updateOne('events', id, next)
    setDraft((value) => (value?.id === id ? { ...value, ...next } : value))
  }

  const editing = events.find((item) => item.id === draft?.id) || null

  return (
    <div className="page planner-page">
      <header className="page-header">
        <h1>برنامه</h1>
        <p>روی جدول بزن و بکش تا بازهٔ ۱۵ دقیقه‌ای بگیری؛ رها کن تا برنامه ساخته شود. یک کلیک هم نیم‌ساعت می‌گذارد. درس را از بالا بکش، برنامه‌ها را جابه‌جا کن و از لبه‌ها زمان را عوض کن. Escape انتخاب را لغو می‌کند.</p>
      </header>
      <div className="stack">
        <UpcomingExams exams={exams} today={today} onEdit={openEdit} />
        <div className="planner-toolbar">
          <WeekNav
            label={weekLabel}
            onPrevious={() => setAnchor(shiftDay(anchor, -7))}
            onNext={() => setAnchor(shiftDay(anchor, 7))}
            onToday={() => setAnchor(saturdayOf(today))}
          />
        </div>
        <SubjectLegend onTarget={setDropTarget} onDrop={openQuickStudy} />
        <CalendarGrid
          days={days}
          events={events}
          today={today}
          nowMinutes={nowMinutes}
          editingId={draft?.id || null}
          dropTarget={dropTarget}
          onCreate={openCreate}
          onEdit={openEdit}
          onMove={moveEvent}
          onResize={resizeEvent}
        />
      </div>
      {draft ? (
        <>
          <button type="button" className="cal-backdrop" aria-label="بستن" onClick={closeDraft} />
          <EventForm
            value={draft}
            editing={Boolean(draft.id)}
            done={Boolean(editing?.done)}
            error={error}
            namePrompt={Boolean(draft.askTitle)}
            onChange={changeForm}
            onSubmit={handleSubmit}
            onCancel={closeDraft}
            onToggle={toggleDone}
            onDelete={removeEvent}
            onStartTimer={() => {
              startTimerFromEvent(draft, { setTimerTarget, selectPhase, navigate })
              closeDraft()
            }}
          />
        </>
      ) : null}
    </div>
  )
}
