import SubjectField from '../SubjectField'
import { getSubjectById } from '../../data/subjects'

export default function StudyTarget({ subject, topic, locked, onSubjectChange, onTopicChange }) {
  const topics = getSubjectById(subject)?.topics || []

  return (
    <div className="study-target">
      <h1 className="timer-title">
        <span>مطالعه</span>
        <SubjectField
          variant="title"
          value={subject}
          disabled={locked}
          onChange={onSubjectChange}
        />
      </h1>
      <input
        className="topic-input"
        aria-label="مبحث"
        value={topic}
        disabled={locked}
        dir="auto"
        placeholder="مبحث"
        maxLength={80}
        autoComplete="off"
        list="timer-topics"
        onChange={(event) => onTopicChange(event.target.value)}
      />
      <datalist id="timer-topics">
        {topics.map((name) => (
          <option key={name} value={name} />
        ))}
      </datalist>
    </div>
  )
}
