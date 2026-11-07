// @ts-nocheck
export default function AnswerSheet({ questions, onMark, onRemove }) {
  if (questions.length === 0) {
    return <p className="card-copy">هنوز سوالی وارد نشده. فهرست به هر ترتیبی بزرگ می‌شود.</p>
  }

  return (
    <ul className="sheet">
      {questions.map((item) => (
        <li key={item.number} className={`sheet-row is-${item.mark || 'open'}`}>
          <strong>س{item.number}</strong>
          <span className="sheet-choice">{item.choice === 'blank' ? 'سفید' : item.choice}</span>
          {item.choice === 'blank' ? (
            <span className="sheet-note">بدون پاسخ</span>
          ) : (
            <div className="mark-pair" role="group" aria-label={`علامت سوال ${item.number}`}>
              <button
                type="button"
                className={item.mark === 'right' ? 'mark-button is-right' : 'mark-button'}
                aria-pressed={item.mark === 'right'}
                onClick={() => onMark(item.number, 'right')}
              >
                درست
              </button>
              <button
                type="button"
                className={item.mark === 'wrong' ? 'mark-button is-wrong' : 'mark-button'}
                aria-pressed={item.mark === 'wrong'}
                onClick={() => onMark(item.number, 'wrong')}
              >
                غلط
              </button>
            </div>
          )}
          <button type="button" className="button button-danger sheet-remove" onClick={() => onRemove(item.number)}>
            حذف
          </button>
        </li>
      ))}
    </ul>
  )
}
