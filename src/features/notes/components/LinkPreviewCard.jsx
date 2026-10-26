export default function LinkPreviewCard({ note, x, y }) {
  if (!note) return null
  const lines = (note.content || '').split('\n').filter(Boolean).slice(0, 4).join('\n')
  return (
    <div className="notes-hover-card" style={{ left: x + 12, top: y + 12 }}>
      <strong dir="auto">{note.title}</strong>
      <p dir="auto">{lines || 'خالی است.'}</p>
    </div>
  )
}
