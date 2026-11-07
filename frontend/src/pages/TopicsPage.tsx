// @ts-nocheck
import { useMemo, useState } from 'react'
import TopicBoard from '../components/topics/TopicBoard'
import TopicForm from '../components/topics/TopicForm'
import { getSubjectById } from '../data/subjects'
import { useCollection } from '../hooks/useCollection'
import { useSettings } from '../hooks/useSettings'
import { addOne, deleteOne, updateOne, updateSettings } from '../utils/storage'
import {
  buildTopic,
  coverageOf,
  managedTopicsBySubject,
  studiedTopics,
  topicHideKey,
} from '../utils/topics'
import './TopicsPage.css'

export default function TopicsPage() {
  const settings = useSettings()
  const topics = useCollection('topics')
  const sessions = useCollection('sessions')
  const tests = useCollection('tests')
  const [error, setError] = useState('')
  const hiddenTopics = settings.hiddenTopics || []
  const groups = useMemo(
    () => managedTopicsBySubject(topics, sessions, tests, hiddenTopics),
    [topics, sessions, tests, hiddenTopics],
  )
  const suggestions = studiedTopics(sessions, tests, topics, hiddenTopics)
  const coverage = coverageOf(topics)

  function addTopic(topic) {
    const result = buildTopic(topic, topics)
    if (result.error) {
      setError(result.error)
      return
    }
    const hideKey = topicHideKey(result.topic.subject, result.topic.name)
    if (hiddenTopics.includes(hideKey)) {
      updateSettings({
        hiddenTopics: hiddenTopics.filter((item) => item !== hideKey),
      })
    }
    addOne('topics', result.topic)
    setError('')
  }

  function removeTopic(topic) {
    const hideKey = topicHideKey(topic.subject, topic.name)
    ;(topic.ids || []).forEach((id) => deleteOne('topics', id))
    if (topic.id && !(topic.ids || []).includes(topic.id)) deleteOne('topics', topic.id)
    if (!hiddenTopics.includes(hideKey)) {
      updateSettings({ hiddenTopics: [...hiddenTopics, hideKey] })
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>مباحث</h1>
        <p>
          مباحث هر درس را اینجا ببین و مدیریت کن. حذف، مبحث را از فهرست و از پیشنهادهای زمان‌سنج برمی‌دارد.
        </p>
      </header>
      <div className="stack">
        <section className="card">
          <div className="goal-figures">
            <h2>پیشرفت کلی</h2>
            <strong dir="ltr">{coverage}%</strong>
          </div>
          <div className="meter" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={coverage} aria-label="پیشرفت کلی">
            <span style={{ width: `${coverage}%` }} />
          </div>
          <TopicForm existing={topics} onCreate={addTopic} />
          {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
        </section>
        <TopicBoard
          groups={groups}
          onStatus={(topic, status) => {
            ;(topic.ids || []).forEach((id) => updateOne('topics', id, { status }))
            if (topic.id && !(topic.ids || []).includes(topic.id)) {
              updateOne('topics', topic.id, { status })
            }
          }}
          onDelete={removeTopic}
        />
        {suggestions.length > 0 ? (
          <section className="card">
            <h2>پیشنهاد از جلسه‌ها</h2>
            <p className="card-copy">این مباحث را در جلسه یا آزمون دیده‌ای و هنوز به فهرست اضافه نکرده‌ای.</p>
            <ul className="suggestion-list">
              {suggestions.map((item) => (
                <li key={`${item.subject}::${item.name}`}>
                  <span>{getSubjectById(item.subject)?.name} · {item.name}</span>
                  <span className="topic-actions">
                    <button type="button" className="button" onClick={() => addTopic(item)}>
                      افزودن
                    </button>
                    <button
                      type="button"
                      className="button button-danger"
                      onClick={() => removeTopic({ ...item, ids: [], id: null, saved: false })}
                    >
                      حذف
                    </button>
                  </span>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </div>
    </div>
  )
}
