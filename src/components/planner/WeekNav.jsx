export default function WeekNav({ label, onPrevious, onNext, onToday }) {
  return (
    <div className="week-nav">
      <div className="button-row">
        <button type="button" className="button" onClick={onPrevious}>هفته قبل</button>
        <button type="button" className="button" onClick={onToday}>این هفته</button>
        <button type="button" className="button" onClick={onNext}>هفته بعد</button>
      </div>
      <p className="week-label">
        <strong>{label}</strong>
      </p>
    </div>
  )
}
