// @ts-nocheck
import { formatKonkoorPercentage, konkoorPercentage } from '../../utils/score'

function wrongLabel(test) {
  if (!Array.isArray(test.questions)) return null
  const wrong = test.questions.filter((item) => item.mark === 'wrong').map((item) => item.number)
  return wrong.length > 0 ? `غلط: ${wrong.join('، ')}` : 'غلطی نیست'
}

export default function TestResult({ test }) {
  const score = formatKonkoorPercentage(test.correct, test.wrong, test.totalQuestions)
  const value = konkoorPercentage(test.correct, test.wrong, test.totalQuestions)
  const negative = value != null && value < 0
  const wrongs = wrongLabel(test)

  return (
    <section className="card score-card">
      <p className="card-copy">درصد کنکور</p>
      <p className={negative ? 'score-value is-negative' : 'score-value'}>{score}</p>
      <p className="card-copy">
        {test.correct} درست، {test.wrong} غلط، {test.blank} سفید. درست منهای یک‌سوم غلط‌ها، تقسیم بر تعداد کل.
      </p>
      {wrongs ? <p className="card-copy">{wrongs}</p> : null}
    </section>
  )
}
