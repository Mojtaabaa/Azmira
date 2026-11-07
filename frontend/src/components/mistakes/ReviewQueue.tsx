// @ts-nocheck
import MistakeCard from './MistakeCard'

export default function ReviewQueue({ mistakes, tests, onToggle, onDelete }) {
  return (
    <section className="card">
      <h2>مرور دوباره</h2>
      {mistakes.length === 0 ? (
        <p className="card-copy">اشتباهی برای مرور نمانده.</p>
      ) : (
        <div className="mistake-stack">
          {mistakes.map((mistake) => (
            <MistakeCard
              key={mistake.id}
              mistake={mistake}
              test={tests.find((item) => item.id === mistake.testId) || null}
              onToggle={onToggle}
              onDelete={onDelete}
            />
          ))}
        </div>
      )}
    </section>
  )
}
