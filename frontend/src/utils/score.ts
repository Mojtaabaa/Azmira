// @ts-nocheck
export function konkoorPercentage(correct, wrong, totalQuestions) {
  const right = Number(correct)
  const misses = Number(wrong)
  const total = Number(totalQuestions)

  if (!Number.isFinite(right) || right < 0) return null
  if (!Number.isFinite(misses) || misses < 0) return null
  if (!Number.isFinite(total) || total <= 0) return null

  return ((right - misses / 3) / total) * 100
}

export function formatKonkoorPercentage(correct, wrong, totalQuestions) {
  const value = konkoorPercentage(correct, wrong, totalQuestions)
  if (value == null) return null
  const rounded = Math.round(value * 10) / 10
  return `${rounded.toFixed(1)}%`
}
