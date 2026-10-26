import { useState } from 'react'
import { coverageOf, nextTopicStatus, topicStatusLabel, topicsBySubject } from '../../utils/topics'

export default function TopicBoard({ topics, onStatus, onDelete }) {
  const [confirming, setConfirming] = useState('')
  const groups = topicsBySubject(topics)

  if (groups.length === 0) {
    return <p className="card-copy">هنوز مبحثی در فهرست نیست.</p>
  }

  return (
    <div className="topic-groups">
      {groups.map((group) => {
        const coverage = coverageOf(group.topics)
        return (
          <section key={group.subject.id} className="card">
            <div className="goal-figures">
              <h2>{group.subject.name}</h2>
              <strong dir="ltr">{coverage}%</strong>
            </div>
            <div className="meter" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={coverage} aria-label={group.subject.name}>
              <span style={{ width: `${coverage}%` }} />
            </div>
            <ul className="topic-list">
              {group.topics.map((topic) => (
                <li key={topic.id}>
                  <span>{topic.name}</span>
                  <span className="topic-actions">
                    <button
                      type="button"
                      className={topic.status === 'done' ? 'button button-primary' : 'button'}
                      aria-label={`وضعیت ${topic.name}`}
                      onClick={() => onStatus(topic, nextTopicStatus(topic.status))}
                    >
                      {topicStatusLabel(topic.status)}
                    </button>
                    {confirming === topic.id ? (
                      <button type="button" className="button button-danger is-solid" onClick={() => onDelete(topic.id)}>
                        تایید حذف
                      </button>
                    ) : (
                      <button type="button" className="button button-danger" onClick={() => setConfirming(topic.id)}>
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
