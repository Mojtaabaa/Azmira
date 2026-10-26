import { useState } from 'react'
import TopicBoard from '../components/topics/TopicBoard'
import TopicForm from '../components/topics/TopicForm'
import { getSubjectById } from '../data/subjects'
import { useCollection } from '../hooks/useCollection'
import { addOne, deleteOne, updateOne } from '../utils/storage'
import { buildTopic, coverageOf, studiedTopics } from '../utils/topics'
import './TopicsPage.css'

export default function TopicsPage() {
  const topics = useCollection('topics')
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const [error, setError] = useState('')
  const suggestions = studiedTopics(sessions, tests, topics)
  const coverage = coverageOf(topics)

  function addTopic(topic) {
    const result = buildTopic(topic, topics)
    if (result.error) {
      setError(result.error)
      return
    }
    addOne('topics', result.topic)
    setError('')
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>مبحث‌ها</h1>
        <p>برای هر درس مبحث‌ها را بنویس. وضعیت را بزن تا از نخوانده به در حال خواندن و تمام برسد. نوار، سهم مبحث‌های تمام‌شده و نیمه‌تمام را نشان می‌دهد.</p>
      </header>
      <div className="stack">
        <section className="card">
          <div className="goal-figures">
            <h2>پوشش کل</h2>
            <strong dir="ltr">{coverage}%</strong>
          </div>
          <div className="meter" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={coverage} aria-label="پوشش کل">
            <span style={{ width: `${coverage}%` }} />
          </div>
          <TopicForm existing={topics} onCreate={addTopic} />
          {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
        </section>
        <TopicBoard
          topics={topics}
          onStatus={(topic, status) => updateOne('topics', topic.id, { status })}
          onDelete={(id) => deleteOne('topics', id)}
        />
        {suggestions.length > 0 ? (
          <section className="card">
            <h2>از مطالعه‌ها</h2>
            <p className="card-copy">این مبحث‌ها در جلسه یا آزمون آمده‌اند و هنوز در فهرست نیستند.</p>
            <ul className="suggestion-list">
              {suggestions.map((item) => (
                <li key={`${item.subject}::${item.name}`}>
                  <span>{getSubjectById(item.subject)?.name} · {item.name}</span>
                  <button type="button" className="button" onClick={() => addTopic(item)}>
                    افزودن
                  </button>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </div>
    </div>
  )
}
