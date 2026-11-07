import { useEffect, useRef, useState } from 'react'
import {
  DEFAULT_EVENT_MINUTES,
  GRID_END,
  GRID_START,
  SLOT_MINUTES,
  SLOT_PX,
  clampGridMinutes,
  eventsOnDay,
  formatHourClockLabel,
  formatMinutesAsClock,
  layoutTimed,
  minutesOfTime,
  rangeFromDrag,
  snapMinutes,
  slotMinutes,
} from '../../utils/events'
import { getSubjectById } from '../../data/subjects'
import { formatJalali } from '../../utils/week'
import CalendarEvent from './CalendarEvent'

const SLOTS = slotMinutes()
const GRID_HEIGHT = SLOTS.length * SLOT_PX
const HOURS = []
for (let hour = GRID_START / 60; hour <= GRID_END / 60; hour += 1) HOURS.push(hour)

function minutesFromPoint(clientX, clientY) {
  const stack = document.elementsFromPoint(clientX, clientY)
  const slot = stack.find((node) => node instanceof Element && node.matches('[data-slot="time"]'))
  if (!slot) return null
  const base = Number(slot.dataset.minutes)
  if (!Number.isFinite(base)) return null
  const date = slot.dataset.date || ''
  const rect = slot.getBoundingClientRect()
  const ratio = rect.height > 0 ? Math.min(0.999, Math.max(0, (clientY - rect.top) / rect.height)) : 0
  const minutes = clampGridMinutes(snapMinutes(base + ratio * SLOT_MINUTES))
  return { date, minutes }
}

function capturePointer(node, pointerId) {
  if (!node?.setPointerCapture) return
  try {
    node.setPointerCapture(pointerId)
  } catch {
    void 0
  }
}

export default function CalendarGrid({
  days,
  events,
  today,
  nowMinutes,
  editingId,
  dropTarget,
  onCreate,
  onEdit,
  onMove,
  onResize,
}) {
  const scrollRef = useRef(null)
  const previewRef = useRef(null)
  const resizeRef = useRef(null)
  const createRef = useRef(null)
  const scrolledWeek = useRef('')
  const [preview, setPreview] = useState(null)
  const [resize, setResize] = useState(null)
  const [selecting, setSelecting] = useState(null)
  const weekKey = days[0]?.date || ''

  function updatePreview(value) {
    previewRef.current = value
    setPreview(value)
  }

  function updateSelecting(value) {
    createRef.current = value
    setSelecting(value)
  }

  useEffect(() => {
    const node = scrollRef.current
    if (!node || scrolledWeek.current === weekKey) return
    scrolledWeek.current = weekKey
    const focus = days.some((day) => day.date === today) ? nowMinutes : 8 * 60
    const top = ((Math.min(Math.max(focus, GRID_START), GRID_END) - GRID_START - 60) / SLOT_MINUTES) * SLOT_PX
    node.scrollTop = Math.max(0, top)
  }, [days, nowMinutes, today, weekKey])

  useEffect(() => {
    if (!selecting) return undefined
    function onKey(event) {
      if (event.key !== 'Escape') return
      updateSelecting(null)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [selecting])

  function commit(id) {
    const next = previewRef.current
    updatePreview(null)
    if (!next?.date) return
    onMove(id, next.date, next.allday || next.minutes === '' ? null : Number(next.minutes))
  }

  function updateResize(value) {
    resizeRef.current = value
    setResize(value)
  }

  function commitResize(id) {
    const next = resizeRef.current
    updateResize(null)
    if (!next || next.id !== id) return
    onResize(id, next.startTime, next.endTime)
  }

  function autoScroll(clientY) {
    const scroller = scrollRef.current
    if (!scroller) return
    const rect = scroller.getBoundingClientRect()
    const edge = 40
    if (clientY < rect.top + edge) scroller.scrollTop -= 22
    else if (clientY > rect.bottom - edge) scroller.scrollTop += 22
  }

  function beginCreate(native, date, minutes) {
    if (native.button !== 0 || createRef.current) return
    native.preventDefault()
    updateSelecting({
      date,
      origin: minutes,
      current: minutes,
      moved: false,
      x: native.clientX,
      y: native.clientY,
    })
    capturePointer(native.currentTarget, native.pointerId)
  }

  function moveCreate(native) {
    const current = createRef.current
    if (!current) return
    const distance = Math.hypot(native.clientX - current.x, native.clientY - current.y)
    autoScroll(native.clientY)
    const hit = minutesFromPoint(native.clientX, native.clientY)
    if (!hit || hit.date !== current.date) {
      if (distance >= 4) updateSelecting({ ...current, moved: true })
      return
    }
    updateSelecting({
      ...current,
      current: hit.minutes,
      moved: current.moved || distance >= 4,
    })
  }

  function endCreate() {
    const current = createRef.current
    if (!current) return
    updateSelecting(null)
    const range = current.moved
      ? rangeFromDrag(current.origin, current.current)
      : {
          start: current.origin,
          end: Math.min(GRID_END, current.origin + DEFAULT_EVENT_MINUTES),
        }
    onCreate(current.date, range.start, range.end)
  }

  const selection = selecting
    ? rangeFromDrag(selecting.origin, selecting.current)
    : null

  return (
    <div className={selecting ? 'cal-scroll is-selecting' : 'cal-scroll'} ref={scrollRef}>
      <div className="cal-frame">
        <div className="cal-row cal-head">
          <div className="cal-gutter" />
          {days.map((day) => (
            <button
              key={day.date}
              type="button"
              className={[
                'cal-day',
                day.date === today ? 'is-today' : '',
                dropTarget?.date === day.date && dropTarget.allday ? 'is-target' : '',
              ].filter(Boolean).join(' ')}
              data-slot="allday"
              data-date={day.date}
              data-allday="true"
              onClick={() => onCreate(day.date, null, null)}
            >
              <strong>{day.name}</strong>
              <span className="day-jalali">{formatJalali(day.date)}</span>
            </button>
          ))}
        </div>
        <div className="cal-row cal-allday">
          <div className="cal-gutter">کل روز</div>
          {days.map((day) => {
            const allDay = eventsOnDay(events, day.date).filter((item) => !item.startTime)
            const targeted = (preview?.date === day.date && (preview.allday || preview.minutes === ''))
              || (dropTarget?.date === day.date && dropTarget.allday)
            return (
              <div
                key={day.date}
                className={targeted ? 'cal-allday-cell is-target' : 'cal-allday-cell'}
                data-slot="allday"
                data-date={day.date}
                data-allday="true"
                onClick={(native) => {
                  if (native.target !== native.currentTarget) return
                  onCreate(day.date, null, null)
                }}
              >
                {allDay.map((item) => (
                  <CalendarEvent
                    key={item.id}
                    item={item}
                    editing={editingId === item.id}
                    dragging={preview?.id === item.id}
                    onEdit={onEdit}
                    onPreview={updatePreview}
                    onCommit={commit}
                    onResizePreview={updateResize}
                    onResizeCommit={commitResize}
                  />
                ))}
              </div>
            )
          })}
        </div>
        <div className="cal-row cal-body">
          <div className="cal-gutter cal-times" style={{ height: GRID_HEIGHT }}>
            {HOURS.map((hour) => (
              <span
                key={hour}
                className={hour === GRID_END / 60 ? 'cal-hour is-end' : 'cal-hour'}
                style={{ top: ((hour * 60 - GRID_START) / SLOT_MINUTES) * SLOT_PX }}
              >
                <span dir="ltr">{formatHourClockLabel(hour)}</span>
              </span>
            ))}
          </div>
          {days.map((day) => {
            const placed = layoutTimed(eventsOnDay(events, day.date))
            return (
              <div key={day.date} className={day.date === today ? 'cal-col is-today' : 'cal-col'}>
                {SLOTS.map((minutes) => {
                  const previewStart = preview?.date === day.date && preview.minutes !== '' && !preview.allday
                    ? Number(preview.minutes)
                    : null
                  const previewEnd = previewStart == null ? null : previewStart + (preview.duration || 60)
                  const previewHit = previewEnd != null
                    && minutes < previewEnd
                    && minutes + SLOT_MINUTES > previewStart
                  const dropHit = dropTarget?.date === day.date
                    && !dropTarget.allday
                    && Number(dropTarget.minutes) === minutes
                  const selectHit = selection
                    && selecting?.date === day.date
                    && minutes >= selection.start
                    && minutes < selection.end
                  return (
                    <button
                      key={minutes}
                      type="button"
                      className={[
                        'cal-slot',
                        minutes % 60 === 0 ? 'is-hour' : '',
                        minutes % 60 === 30 ? 'is-half' : '',
                        previewHit || dropHit ? 'is-target' : '',
                        selectHit ? 'is-selecting' : '',
                      ].filter(Boolean).join(' ')}
                      style={{ height: SLOT_PX }}
                      data-slot="time"
                      data-date={day.date}
                      data-minutes={String(minutes)}
                      aria-label={`افزودن ${day.name} ${formatMinutesAsClock(minutes)}`}
                      onPointerDown={(native) => beginCreate(native, day.date, minutes)}
                      onPointerMove={moveCreate}
                      onPointerUp={endCreate}
                      onPointerCancel={endCreate}
                      onLostPointerCapture={endCreate}
                    />
                  )
                })}
                {day.date === today && nowMinutes >= GRID_START && nowMinutes <= GRID_END ? (
                  <div
                    className="cal-now"
                    data-label={formatMinutesAsClock(nowMinutes)}
                    style={{ top: ((nowMinutes - GRID_START) / SLOT_MINUTES) * SLOT_PX }}
                  />
                ) : null}
                {selection && selecting?.date === day.date ? (
                  <div
                    className="cal-select"
                    style={{
                      top: ((selection.start - GRID_START) / SLOT_MINUTES) * SLOT_PX,
                      height: Math.max(SLOT_PX - 2, ((selection.end - selection.start) / SLOT_MINUTES) * SLOT_PX - 2),
                    }}
                  >
                    <strong>برنامهٔ جدید</strong>
                    <span dir="ltr">
                      {formatMinutesAsClock(selection.start)}–{formatMinutesAsClock(selection.end)}
                    </span>
                  </div>
                ) : null}
                {preview?.date === day.date && preview.minutes !== '' && !preview.allday ? (
                  <div
                    className="cal-preview"
                    data-subject={preview.subject || 'none'}
                    style={{
                      top: ((Number(preview.minutes) - GRID_START) / SLOT_MINUTES) * SLOT_PX,
                      height: Math.max(SLOT_PX - 2, ((preview.duration || 60) / SLOT_MINUTES) * SLOT_PX - 2),
                      '--subject': getSubjectById(preview.subject)?.color || 'var(--accent)',
                    }}
                  >
                    <strong>{preview.title}</strong>
                    <span dir="ltr">{formatMinutesAsClock(Number(preview.minutes))}</span>
                  </div>
                ) : null}
                {placed.map((entry) => {
                  const live = resize?.id === entry.event.id ? resize : null
                  const start = live ? minutesOfTime(live.startTime) : entry.start
                  const end = live ? minutesOfTime(live.endTime) : entry.end
                  const top = ((start - GRID_START) / SLOT_MINUTES) * SLOT_PX
                  const height = Math.max(SLOT_PX - 2, ((end - start) / SLOT_MINUTES) * SLOT_PX - 2)
                  const width = 100 / entry.laneCount
                  const shown = live
                    ? { ...entry.event, startTime: live.startTime, endTime: live.endTime }
                    : entry.event
                  return (
                    <CalendarEvent
                      key={entry.event.id}
                      item={shown}
                      editing={editingId === entry.event.id}
                      dragging={preview?.id === entry.event.id}
                      onEdit={onEdit}
                      onPreview={updatePreview}
                      onCommit={commit}
                      onResizePreview={updateResize}
                      onResizeCommit={commitResize}
                      style={{
                        top,
                        height,
                        insetInlineStart: `calc(${entry.lane * width}% + 2px)`,
                        width: `calc(${width}% - 4px)`,
                      }}
                    />
                  )
                })}
              </div>
            )
          })}
        </div>
      </div>
      {preview?.title && (preview.allday || preview.minutes === '') ? (
        <div className="cal-ghost" style={{ left: preview.x, top: preview.y }}>{preview.title}</div>
      ) : null}
    </div>
  )
}
