// @ts-nocheck
import SubjectField from '../SubjectField'
import TopicField from '../TopicField'
import { formatHourCount, formatMinutes } from '../../utils/clock'

export default function StudyTarget({
  subject,
  topic,
  locked,
  onSubjectChange,
  onTopicChange,
  weekDone = 0,
  weekGoal = 0,
}) {
  const ratio = weekGoal > 0 ? Math.min(1.35, weekDone / weekGoal) : 0
  const width = Math.round(Math.min(100, ratio * 100))

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
      <TopicField
        variant="bare"
        subject={subject}
        value={topic}
        disabled={locked}
        onChange={onTopicChange}
      />
      {weekGoal > 0 ? (
        <div className="study-week-goal">
          <div className="study-week-goal-meta">
            <span>هدف این هفته</span>
            <strong>
              {formatMinutes(weekDone)}
              <span className="study-week-goal-sep">از</span>
              {formatHourCount(weekGoal)} ساعت
            </strong>
          </div>
          <div
            className={[
              'meter study-week-meter',
              weekDone >= weekGoal ? 'is-over' : '',
              weekDone > 0 && weekDone < weekGoal * 0.35 ? 'is-low' : '',
            ].filter(Boolean).join(' ')}
            role="progressbar"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={width}
            aria-label="پیشرفت هدف هفتگی درس"
          >
            <span style={{ width: `${width}%` }} />
          </div>
        </div>
      ) : null}
    </div>
  )
}
