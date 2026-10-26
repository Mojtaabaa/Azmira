import { getSubjectById, listSubjects } from '../../data/subjects'
import { useSettings } from '../../hooks/useSettings'
import { usePopover } from '../../hooks/usePopover'
import '../Popover.css'

export default function SubjectPicker({ value, onChange }) {
  useSettings()
  const { boxRef, open, style, toggle, close } = usePopover(300, 210)
  const current = getSubjectById(value)

  function pick(id) {
    onChange(id)
    close()
  }

  return (
    <span className={current ? 'pick-chip is-set' : 'pick-chip'} ref={boxRef}>
      <button
        type="button"
        className="pick-chip-main"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={toggle}
      >
        <svg viewBox="0 0 24 24" aria-hidden="true" className="pick-chip-icon">
          <path d="M4 12.5V5h7.5L20 13.5 13.5 20 4 12.5zM8 9a1 1 0 1 0 0-.01" />
        </svg>
        {current ? current.name : 'درس'}
      </button>
      {current ? (
        <button type="button" className="pick-chip-x" aria-label="پاک کردن درس" onClick={() => onChange('')}>
          ×
        </button>
      ) : null}
      {open ? (
        <div className="pick-pop is-small" style={style}>
          <ul className="pick-quick" role="listbox" aria-label="درس">
            <li>
              <button type="button" role="option" aria-selected={!current} onClick={() => pick('')}>
                <span>بدون درس</span>
                {!current ? <Mark /> : null}
              </button>
            </li>
            {listSubjects().map((subject) => (
              <li key={subject.id}>
                <button
                  type="button"
                  role="option"
                  aria-selected={subject.id === value}
                  onClick={() => pick(subject.id)}
                >
                  <span>{subject.name}</span>
                  {subject.id === value ? <Mark /> : null}
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </span>
  )
}

function Mark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="pick-quick-mark">
      <path d="M5 12.5 9.2 17 19 7" />
    </svg>
  )
}
