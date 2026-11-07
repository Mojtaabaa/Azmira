// @ts-nocheck
import { useRef } from 'react'
import { getSubjectById } from '../../data/subjects'
import { GRID_START, RESIZE_STEP, SLOT_MINUTES, SLOT_PX, eventTypeLabel, minutesOfTime, resizeTimes } from '../../utils/events'

export default function CalendarEvent({
  item,
  style,
  editing,
  dragging,
  onEdit,
  onPreview,
  onCommit,
  onResizePreview,
  onResizeCommit,
}) {
  const move = useRef(null)
  const moved = useRef(false)
  const resize = useRef(null)
  const subject = item.subject ? getSubjectById(item.subject) : null
  const timed = Boolean(item.startTime)
  const className = [
    'cal-event',
    item.type === 'exam' ? 'is-exam' : '',
    item.done ? 'is-done' : '',
    editing ? 'is-editing' : '',
    dragging ? 'is-dragging' : '',
    timed ? 'is-timed' : 'is-allday',
  ].filter(Boolean).join(' ')

  function slotUnder(x, y) {
    const stack = [...document.elementsFromPoint(x, y)].filter((node) => node instanceof Element)
    const head = document.querySelector('.cal-head')
    const belowHead = !head || y >= head.getBoundingClientRect().bottom
    if (belowHead) {
      const time = stack.find((node) => node.matches('[data-slot]') && node.dataset.minutes)
      if (time) return time
    }
    return stack.find((node) => node.matches('[data-slot]')) || null
  }

  function quarterAt(slot, clientY) {
    if (!slot || slot.dataset.allday === 'true') return null
    const base = Number(slot.dataset.minutes)
    if (!Number.isFinite(base)) return null
    const rect = slot.getBoundingClientRect()
    const ratio = rect.height > 0 ? Math.min(0.999, Math.max(0, (clientY - rect.top) / rect.height)) : 0
    return Math.round((base + ratio * SLOT_MINUTES) / RESIZE_STEP) * RESIZE_STEP
  }

  function capture(native) {
    const node = native.currentTarget
    if (!node.setPointerCapture) return
    try {
      node.setPointerCapture(native.pointerId)
    } catch (captureError) {
      void captureError
    }
  }

  function finishMove(edit) {
    if (!move.current) return
    const dropped = moved.current
    move.current = null
    moved.current = false
    if (dropped && edit) onCommit(item.id)
    else if (dropped) onPreview(null)
    else if (edit) onEdit(item)
  }

  function finishResize() {
    if (!resize.current) return
    resize.current = null
    onResizeCommit(item.id)
  }

  return (
    <div
      className={className}
      data-subject={item.subject || 'none'}
      data-custom={subject?.custom ? 'true' : 'false'}
      style={{
        ...style,
        '--subject': subject?.color || 'var(--accent)',
      }}
    >
      {timed ? (
        <button
          type="button"
          className="cal-resize is-start"
          aria-label="تغییر ساعت شروع"
          onPointerDown={(native) => {
            if (native.button !== 0) return
            native.stopPropagation()
            const start = minutesOfTime(item.startTime)
            const end = minutesOfTime(item.endTime)
            if (start == null || end == null) return
            resize.current = { edge: 'start', y: native.clientY, start, end }
            capture(native)
          }}
          onPointerMove={(native) => {
            if (!resize.current || resize.current.edge !== 'start') return
            const deltaMinutes = ((native.clientY - resize.current.y) / SLOT_PX) * SLOT_MINUTES
            const next = resizeTimes(resize.current.start, resize.current.end, 'start', deltaMinutes)
            if (next) onResizePreview({ id: item.id, ...next })
          }}
          onPointerUp={finishResize}
          onLostPointerCapture={finishResize}
        />
      ) : null}
      <button
        type="button"
        className="cal-event-body"
        onPointerDown={(native) => {
          if (native.button !== 0) return
          const startMin = minutesOfTime(item.startTime)
          const box = native.currentTarget.closest('.cal-event')?.getBoundingClientRect()
          const rawOffset = box && startMin != null
            ? ((native.clientY - box.top) / SLOT_PX) * SLOT_MINUTES
            : 0
          move.current = {
            x: native.clientX,
            y: native.clientY,
            offsetMin: Math.round(rawOffset / RESIZE_STEP) * RESIZE_STEP,
          }
          moved.current = false
          capture(native)
        }}
        onPointerMove={(native) => {
          if (!move.current) return
          const distance = Math.hypot(native.clientX - move.current.x, native.clientY - move.current.y)
          if (distance < 6) return
          moved.current = true
          const slot = slotUnder(native.clientX, native.clientY)
          const pointed = quarterAt(slot, native.clientY)
          const startMin = minutesOfTime(item.startTime)
          const endMin = minutesOfTime(item.endTime)
          const duration = startMin != null && endMin != null && endMin > startMin ? endMin - startMin : 60
          const minutes = pointed == null ? '' : Math.max(GRID_START, pointed - move.current.offsetMin)
          onPreview({
            id: item.id,
            title: item.title,
            subject: item.subject || 'none',
            duration,
            x: native.clientX,
            y: native.clientY,
            date: slot?.dataset.date || '',
            minutes,
            allday: slot?.dataset.allday === 'true',
          })
        }}
        onPointerUp={() => finishMove(true)}
        onLostPointerCapture={() => finishMove(false)}
      >
        <strong>{item.title}</strong>
        {timed ? (
          <span className="plan-time" dir="ltr">{item.startTime}–{item.endTime}</span>
        ) : (
          <span>{eventTypeLabel(item.type)}{subject ? ` · ${subject.name}` : ''}</span>
        )}
      </button>
      {timed ? (
        <button
          type="button"
          className="cal-resize is-end"
          aria-label="تغییر ساعت پایان"
          onPointerDown={(native) => {
            if (native.button !== 0) return
            native.stopPropagation()
            const start = minutesOfTime(item.startTime)
            const end = minutesOfTime(item.endTime)
            if (start == null || end == null) return
            resize.current = { edge: 'end', y: native.clientY, start, end }
            capture(native)
          }}
          onPointerMove={(native) => {
            if (!resize.current || resize.current.edge !== 'end') return
            const deltaMinutes = ((native.clientY - resize.current.y) / SLOT_PX) * SLOT_MINUTES
            const next = resizeTimes(resize.current.start, resize.current.end, 'end', deltaMinutes)
            if (next) onResizePreview({ id: item.id, ...next })
          }}
          onPointerUp={finishResize}
          onLostPointerCapture={finishResize}
        />
      ) : null}
    </div>
  )
}
