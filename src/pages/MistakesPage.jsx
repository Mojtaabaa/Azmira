import { useMemo, useState } from 'react'
import ExamNav from '../components/tests/ExamNav'
import MistakeFilters from '../components/mistakes/MistakeFilters'
import MistakeList from '../components/mistakes/MistakeList'
import ReviewQueue from '../components/mistakes/ReviewQueue'
import { useCollection } from '../hooks/useCollection'
import { deleteOne, updateOne } from '../utils/storage'
import './TestsPage.css'

const EMPTY_FILTERS = {
  subject: '',
  topic: '',
  cause: '',
  reviewed: 'all',
}

function matches(mistake, filters, reviewedMode) {
  if (filters.subject && mistake.subject !== filters.subject) return false
  if (filters.topic && mistake.topic !== filters.topic) return false
  if (filters.cause && mistake.cause !== filters.cause) return false
  if (reviewedMode === 'reviewed' && !mistake.reviewed) return false
  if (reviewedMode === 'unreviewed' && mistake.reviewed) return false
  return true
}

export default function MistakesPage() {
  const tests = useCollection('tests')
  const mistakes = useCollection('mistakes')
  const [filters, setFilters] = useState(EMPTY_FILTERS)

  const topics = useMemo(() => {
    const names = mistakes
      .filter((mistake) => !filters.subject || mistake.subject === filters.subject)
      .map((mistake) => mistake.topic)
      .filter(Boolean)
    return [...new Set(names)].sort((a, b) => a.localeCompare(b, 'fa'))
  }, [mistakes, filters.subject])

  const activeFilters = topics.includes(filters.topic) ? filters : { ...filters, topic: '' }
  const newestFirst = mistakes.slice().reverse()
  const queue = newestFirst.filter((mistake) => matches(mistake, activeFilters, 'unreviewed'))
  const listed = newestFirst.filter((mistake) => matches(mistake, activeFilters, 'reviewed'))
  const showQueue = filters.reviewed !== 'reviewed'
  const showList = filters.reviewed !== 'unreviewed'

  function changeFilters(patch) {
    setFilters((current) => ({ ...current, ...patch }))
  }

  function toggleReviewed(mistake) {
    updateOne('mistakes', mistake.id, { reviewed: !mistake.reviewed })
  }

  function removeMistake(mistake) {
    deleteOne('mistakes', mistake.id)
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>اشتباهات</h1>
        <p>آنچه را غلط زدی صافی کن و آن‌هایی را که هنوز مرور نکردی کار کن.</p>
        <ExamNav />
      </header>
      {mistakes.length === 0 ? (
        <section className="card stack-card">
          <p className="card-copy">هنوز اشتباهی ثبت نشده. اول یک آزمون ذخیره کن، بعد سوال‌های غلط را بنویس.</p>
        </section>
      ) : (
        <div className="stack">
          <MistakeFilters filters={activeFilters} topics={topics} onChange={changeFilters} />
          {showQueue ? (
            <ReviewQueue mistakes={queue} tests={tests} onToggle={toggleReviewed} onDelete={removeMistake} />
          ) : null}
          {showList ? (
            <MistakeList mistakes={listed} tests={tests} onToggle={toggleReviewed} onDelete={removeMistake} />
          ) : null}
        </div>
      )}
    </div>
  )
}
