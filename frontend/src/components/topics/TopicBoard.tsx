// @ts-nocheck
import { useState } from 'react'
import { coverageOf, nextTopicStatus, topicStatusLabel } from '../../utils/topics'

export default function TopicBoard({ groups, onStatus, onDelete }) {
  const [confirming, setConfirming] = useState('')

  if (!groups.length) {
    return <p className="card-copy">هنوز مبحثی ثبت نشده.</p>
  }

  return (
    <div className="topic-groups">
      {groups.map((group) => {
        const savedTopics = group.topics.filter((topic) => topic.saved)
        const coverage = coverageOf(savedTopics.map((topic) => ({ status: topic.status })))
        return (
          <section key={group.subject.id} className="card">
            <div className="goal-figures">
              <h2>{group.subject.name}</h2>
              {savedTopics.length > 0 ? <strong dir="ltr">{coverage}%</strong> : null}
            </div>
            {savedTopics.length > 0 ? (
              <div
                className="meter"
                role="progressbar"
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={coverage}
                aria-label={group.subject.name}
              >
                <span style={{ width: `${coverage}%` }} />
              </div>
            ) : (
              <p className="card-copy">این نام‌ها از جلسه‌ها آمده‌اند؛ می‌توانی حذفشان کنی تا در پیشنهادها نیایند.</p>
            )}
            <ul className="topic-list">
              {group.topics.map((topic) => (
                <li key={topic.key}>
                  <span className="topic-name">
                    {topic.name}
                    {!topic.saved ? <em className="topic-tag">از جلسه‌ها</em> : null}
                  </span>
                  <span className="topic-actions">
                    {topic.saved ? (
                      <button
                        type="button"
                        className={topic.status === 'done' ? 'button button-primary' : 'button'}
                        aria-label={`وضعیت ${topic.name}`}
                        onClick={() => onStatus(topic, nextTopicStatus(topic.status))}
                      >
                        {topicStatusLabel(topic.status)}
                      </button>
                    ) : null}
                    {confirming === topic.key ? (
                      <button
                        type="button"
                        className="button button-danger is-solid"
                        onClick={() => {
                          onDelete(topic)
                          setConfirming('')
                        }}
                      >
                        تایید حذف
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="button button-danger"
                        onClick={() => setConfirming(topic.key)}
                      >
                        حذف
                      </button>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}
