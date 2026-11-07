// @ts-nocheck
import { todayInputValue } from '../../../utils/clock'
import { addOne } from '../../../utils/storage'

export async function sendNoteToPlanner(note, subject = '') {
  return addOne('tasks', {
    title: note.title,
    done: false,
    dueDate: todayInputValue(),
    subject: subject || '',
    createdAt: new Date().toISOString(),
  })
}
