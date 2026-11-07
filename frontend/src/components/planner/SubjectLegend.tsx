// @ts-nocheck
import { useRef, useState } from 'react'
import { listSubjects } from '../../data/subjects'
import { useSettings } from '../../hooks/useSettings'

function slotAt(x, y, node) {
  node.style.pointerEvents = 'none'
  const hit = document.elementFromPoint(x, y)
  node.style.pointerEvents = ''
  const slot = hit?.closest('[data-slot]')
  if (!slot?.dataset.date) return null
  return {
    date: slot.dataset.date,
    minutes: slot.dataset.minutes || '',
    allday: slot.dataset.allday === 'true' || slot.dataset.minutes === undefined || slot.dataset.minutes === '',
  }
}

export default function SubjectLegend({ onTarget, onDrop }) {
  useSettings()
  const drag = useRef(null)
  const [ghost, setGhost] = useState(null)
  const subjects = listSubjects()

  function endDrag(node) {
    const current = drag.current
    if (!current) return
    const slot = current.moved ? slotAt(current.x, current.y, node) : null
    drag.current = null
    setGhost(null)
    onTarget(null)
    if (!slot) return
    onDrop(current.subject.id, slot)
  }

  return (
    <div className="subject-drop">
      <p>درس را روی یک ربع‌ساعت یا روز بکش تا مطالعه ثبت شود.</p>
      <ul className="subject-legend" aria-label="درس‌ها">
        {subjects.map((subject) => (
          <li key={subject.id}>
            <button
              type="button"
              className="subject-chip"
              data-subject={subject.id}
              data-custom={subject.custom ? 'true' : 'false'}
              style={{ '--subject': subject.color }}
              aria-label={`مطالعه ${subject.name}`}
              onPointerDown={(native) => {
                if (native.button !== 0) return
                drag.current = {
                  subject,
                  x: native.clientX,
                  y: native.clientY,
                  moved: false,
                }
                if (native.currentTarget.setPointerCapture) {
                  try {
                    native.currentTarget.setPointerCapture(native.pointerId)
                  } catch (captureError) {
                    void captureError
                  }
                }
              }}
              onPointerMove={(native) => {
                const current = drag.current
                if (!current) return
                const distance = Math.hypot(native.clientX - current.x, native.clientY - current.y)
                if (!current.moved && distance < 6) return
                current.moved = true
                current.x = native.clientX
                current.y = native.clientY
                const slot = slotAt(native.clientX, native.clientY, native.currentTarget)
                setGhost({ name: current.subject.name, x: native.clientX, y: native.clientY })
                onTarget(slot)
              }}
              onPointerUp={(native) => endDrag(native.currentTarget)}
              onLostPointerCapture={(native) => endDrag(native.currentTarget)}
            >
              {subject.name}
            </button>
          </li>
        ))}
      </ul>
      {ghost ? (
        <div className="cal-ghost" style={{ left: ghost.x, top: ghost.y }}>{ghost.name}</div>
      ) : null}
    </div>
  )
}
