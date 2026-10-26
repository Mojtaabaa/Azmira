import { useNotesUiStore } from '../store/uiStore'

const ROWS = [
  ['Ctrl/Cmd + P', 'فرمان‌ها'],
  ['Ctrl/Cmd + O', 'جابه‌جایی سریع'],
  ['Ctrl/Cmd + Shift + F', 'جستجو'],
  ['Ctrl/Cmd + E', 'تعویض حالت ویرایش'],
  ['Ctrl/Cmd + D', 'یادداشت امروز'],
  ['Ctrl/Cmd + B', 'ضخیم'],
  ['Ctrl/Cmd + I', 'کج'],
  ['[[ یا آیکون ⟦⟧', 'پیوند به یادداشت (گراف)'],
  ['Paste / Drop', 'درج تصویر'],
  ['?', 'همین راهنما'],
  ['فرمان‌ها → خروجی', 'خروجی کل مخزن'],
  ['نوار ابزار → سطل', 'بازیابی حذف‌شده‌ها'],
]

export default function HelpModal() {
  const setHelpOpen = useNotesUiStore((state) => state.setHelpOpen)
  return (
    <div className="notes-modal-backdrop" onClick={() => setHelpOpen(false)}>
      <div className="notes-modal" role="dialog" aria-label="میانبرها" onClick={(event) => event.stopPropagation()}>
        <h2>میانبرها</h2>
        <ul className="notes-help-list">
          {ROWS.map(([key, label]) => (
            <li key={key}>
              <kbd>{key}</kbd>
              <span>{label}</span>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
