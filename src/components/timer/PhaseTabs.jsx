const PHASES = [
  { id: 'focus', label: 'تمرکز' },
  { id: 'shortBreak', label: 'استراحت کوتاه' },
  { id: 'longBreak', label: 'استراحت بلند' },
]

export default function PhaseTabs({ phase, cycleCount, locked, onSelect }) {
  return (
    <div className="phase-tabs" role="tablist" aria-label="مرحله زمان‌سنج">
      {PHASES.map((item) => {
        const active = item.id === phase
        return (
          <button
            key={item.id}
            type="button"
            role="tab"
            aria-selected={active}
            disabled={locked}
            className={active ? 'phase-tab is-active' : 'phase-tab'}
            onClick={() => onSelect(item.id)}
          >
            {item.label}
            {active ? (
              <span className="phase-dots" aria-hidden="true">
                {[0, 1, 2, 3].map((dot) => (
                  <span
                    key={dot}
                    className={dot < cycleCount ? 'phase-dot is-done' : 'phase-dot'}
                  />
                ))}
              </span>
            ) : null}
          </button>
        )
      })}
    </div>
  )
}
