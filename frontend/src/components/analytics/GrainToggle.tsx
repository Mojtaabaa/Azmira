// @ts-nocheck
import { GRAINS } from '../../utils/analytics'

export default function GrainToggle({ grain, onChange }) {
  return (
    <div className="analytics-filters" role="group" aria-label="نمایش زمان">
      {GRAINS.map((item) => {
        const active = item.id === grain
        return (
          <button
            key={item.id}
            type="button"
            className={active ? 'is-active' : undefined}
            aria-pressed={active}
            onClick={() => onChange(item.id)}
          >
            {item.label}
          </button>
        )
      })}
    </div>
  )
}
