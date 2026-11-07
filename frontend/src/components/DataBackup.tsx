// @ts-nocheck
import { Link } from 'react-router-dom'
import { useCollection } from '../hooks/useCollection'

export default function DataBackup() {
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const topics = useCollection('topics')
  const tasks = useCollection('tasks')
  const events = useCollection('events')
  const total = sessions.length + tests.length + mistakes.length + topics.length + tasks.length + events.length

  return (
    <section className="card" id="backup">
      <h2>انتقال داده</h2>
      <p className="card-copy">
        خروجی کامل حساب، فایل اکسل و وارد کردن پشتیبان در صفحه جدا است. الان {total} مورد مطالعه در این حساب ثبت شده.
      </p>
      <div className="button-row">
        <Link className="button button-primary" to="/data">رفتن به انتقال داده</Link>
      </div>
    </section>
  )
}
