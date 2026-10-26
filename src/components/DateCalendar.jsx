import { WEEKDAY_LETTERS, buildMonth } from '../utils/calendar'

export default function DateCalendar({ anchor, value, today, max, alone, canNext, onPick, onPrev, onNext }) {
  const month = buildMonth(anchor)

  return (
    <div className={alone ? 'pick-cal is-alone' : 'pick-cal'}>
      <div className="pick-cal-head">
        <strong>{month.title}</strong>
        <span className="pick-cal-nav">
          <button type="button" aria-label="ماه قبل" onClick={onPrev}>
            <Arrow />
          </button>
          <button type="button" aria-label="ماه بعد" disabled={!canNext} onClick={onNext}>
            <Arrow flip />
          </button>
        </span>
      </div>
      <div className="pick-cal-grid">
        {WEEKDAY_LETTERS.map((letter) => (
          <span key={letter} className="pick-cal-letter">{letter}</span>
        ))}
        {month.cells.map((cell, index) => (
          cell ? (
            <button
              key={cell.key}
              type="button"
              className={[
                'pick-cal-day',
                cell.key === value ? 'is-selected' : '',
                cell.key === today ? 'is-today' : '',
                cell.key < today && !max ? 'is-past' : '',
                cell.weekend ? 'is-weekend' : '',
              ].filter(Boolean).join(' ')}
              aria-pressed={cell.key === value}
              disabled={Boolean(max) && cell.key > max}
              onClick={() => onPick(cell.key)}
            >
              {cell.label}
            </button>
          ) : (
            <span key={`gap-${index}`} />
          )
        ))}
      </div>
    </div>
  )
}

function Arrow({ flip }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" style={flip ? { transform: 'scaleX(-1)' } : undefined}>
      <path d="M9 6l6 6-6 6" />
    </svg>
  )
}
