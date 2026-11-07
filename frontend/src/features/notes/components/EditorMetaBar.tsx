// @ts-nocheck
import { listSubjects } from '../../../data/subjects'
import { sendNoteToPlanner } from '../konkoor/sendToPlanner'
import { NOTE_TEMPLATES } from '../konkoor/templates'
import { exportNote } from '../lib/exportVault'

export default function EditorMetaBar({
  note,
  titleDraft,
  contentDraft,
  setTitleDraft,
  setContentDraft,
  updateNote,
  userId,
  setSaveState,
}) {
  return (
    <div className="notes-editor-actions">
      <select
        aria-label="قالب"
        defaultValue=""
        onChange={async (event) => {
          const key = event.target.value
          event.target.value = ''
          const template = NOTE_TEMPLATES.find((item) => item.id === key)
          if (!template) return
          const body = `${contentDraft || ''}${contentDraft ? '\n\n' : ''}${template.content}`
          setContentDraft(body)
          const patch = { content: body }
          if ((!titleDraft || titleDraft.startsWith('یادداشت')) && template.title) {
            setTitleDraft(template.title)
            patch.title = template.title
          }
          await updateNote(userId, note.id, patch)
        }}
      >
        <option value="">قالب…</option>
        {NOTE_TEMPLATES.map((template) => (
          <option key={template.id} value={template.id}>{template.label}</option>
        ))}
      </select>
      <select aria-label="درس برای کارها" id={`planner-subject-${note.id}`} defaultValue="">
        <option value="">درس (اختیاری)</option>
        {listSubjects().map((subject) => (
          <option key={subject.id} value={subject.id}>{subject.name}</option>
        ))}
      </select>
      <button
        type="button"
        className="button"
        onClick={async () => {
          const subject = document.getElementById(`planner-subject-${note.id}`)?.value || ''
          await sendNoteToPlanner({ ...note, title: titleDraft }, subject)
          setSaveState('saved')
        }}
      >
        ارسال به کارها
      </button>
      <button
        type="button"
        className="button"
        onClick={() => exportNote({ ...note, title: titleDraft, content: contentDraft })}
      >
        خروجی MD
      </button>
    </div>
  )
}
