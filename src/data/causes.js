export const CAUSES = [
  { id: 'concept', label: 'مفهوم' },
  { id: 'careless', label: 'بی‌دقتی' },
  { id: 'time', label: 'زمان' },
  { id: 'misread', label: 'بدخوانی' },
]

export function causeLabel(causeId) {
  return CAUSES.find((cause) => cause.id === causeId)?.label || causeId
}
