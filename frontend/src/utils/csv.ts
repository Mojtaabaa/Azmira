// @ts-nocheck

function cell(value) {
  const text = value == null ? '' : String(value)
  if (/[",\n\r]/.test(text)) return `"${text.replaceAll('"', '""')}"`
  return text
}

export function tableToCsv(columns, rows) {
  const header = columns.map((column) => cell(column.label)).join(',')
  const body = rows.map((row) => columns.map((column) => cell(column.value(row))).join(','))
  return `\uFEFF${[header, ...body].join('\n')}`
}

export function downloadText(filename, text, type = 'text/csv;charset=utf-8') {
  const blob = new Blob([text], { type })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export function stamp() {
  const day = new Date()
  return [
    day.getFullYear(),
    String(day.getMonth() + 1).padStart(2, '0'),
    String(day.getDate()).padStart(2, '0'),
  ].join('-')
}
