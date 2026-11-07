// @ts-nocheck
export const CAUSES = [
  { id: 'concept', label: 'مفهوم', colorVar: '--danger' },
  { id: 'careless', label: 'بی‌دقتی', colorVar: '--warning' },
  { id: 'time', label: 'زمان', colorVar: '--info' },
  { id: 'misread', label: 'بدخوانی', colorVar: '--text-muted' },
]

export function causeLabel(causeId) {
  return CAUSES.find((cause) => cause.id === causeId)?.label || causeId
}

export function causeColorVar(causeId) {
  return CAUSES.find((cause) => cause.id === causeId)?.colorVar || '--text-muted'
}
