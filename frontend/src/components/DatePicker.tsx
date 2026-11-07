// @ts-nocheck
import { useState } from 'react'
import { usePopover } from '../hooks/usePopover'
import {
  dateChipLabel,
  fullDateLabel,
  nextMonthAnchor,
  parseKey,
  previousMonthAnchor,
  quickDates,
} from '../utils/calendar'
import { todayInputValue } from '../utils/clock'
import DateCalendar from './DateCalendar'
import QuickIcon from './QuickIcon'
import './Popover.css'

export default function DatePicker({
  value,
  onChange,
  variant = 'chip',
  max = '',
  clearable = false,
  quick = true,
  label = 'انتخاب تاریخ',
}) {
  const { boxRef, open, style, toggle: togglePop, close } = usePopover(470, 290)
  const [anchor, setAnchor] = useState(() => new Date())
  const today = todayInputValue()
  const isField = variant === 'field'
  const quickItems = quick ? quickDates(today, max) : []
  const canNext = !max || todayInputValue(nextMonthAnchor(anchor)) <= max

  function toggle() {
    if (!open) setAnchor(value ? parseKey(value) : new Date())
    togglePop()
  }

  function pick(key) {
    onChange(key)
    close()
  }

  const wrapperClass = isField ? 'pick-field' : value ? 'pick-chip is-set' : 'pick-chip'
  const mainClass = isField ? (value ? 'pick-field-main' : 'pick-field-main is-empty') : 'pick-chip-main'
  const text = isField ? (value ? fullDateLabel(value) : label) : dateChipLabel(value, today)

  return (
    <span className={wrapperClass} ref={boxRef}>
      <button
        type="button"
        className={mainClass}
        aria-haspopup="dialog"
        aria-expanded={open}
        onClick={toggle}
      >
        {isField ? <span>{text}</span> : null}
        <svg viewBox="0 0 24 24" aria-hidden="true" className="pick-chip-icon">
          <path d="M5 6h14v13H5zM5 10h14M9 4v4M15 4v4" />
        </svg>
        {isField ? null : text}
      </button>
      {!isField && clearable && value ? (
        <button type="button" className="pick-chip-x" aria-label="پاک کردن تاریخ" onClick={() => onChange('')}>
          ×
        </button>
      ) : null}
      {open ? (
        <div
          className="pick-pop"
          style={style}
          role="dialog"
          aria-label={label}
          onClick={(event) => event.preventDefault()}
        >
          {quickItems.length > 0 || clearable ? (
            <ul className="pick-quick">
              {quickItems.map((item) => (
                <li key={item.id}>
                  <button type="button" onClick={() => pick(todayInputValue(item.date))}>
                    <QuickIcon kind={item.icon} />
                    <span>{item.label}</span>
                    <small>{item.side}</small>
                  </button>
                </li>
              ))}
              {clearable ? (
                <li>
                  <button type="button" onClick={() => pick('')}>
                    <QuickIcon kind="none" />
                    <span>بدون تاریخ</span>
                  </button>
                </li>
              ) : null}
            </ul>
          ) : null}
          <DateCalendar
            anchor={anchor}
            value={value}
            today={today}
            max={max}
            alone={quickItems.length === 0 && !clearable}
            canNext={canNext}
            onPick={pick}
            onPrev={() => setAnchor(previousMonthAnchor(anchor))}
            onNext={() => setAnchor(nextMonthAnchor(anchor))}
          />
        </div>
      ) : null}
    </span>
  )
}
