// @ts-nocheck
import MistakeCard from './MistakeCard'

export default function MistakeList({ mistakes, tests, onToggle, onDelete }) {
  return (
    <section className="card">
      <h2>مرورشده‌ها</h2>
      {mistakes.length === 0 ? (
        <p className="card-copy">با این فیلتر، اشتباه مرور‌شده‌ای نیست.</p>
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
