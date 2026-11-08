// @ts-nocheck
import { Link } from 'react-router-dom'
import './EmptyState.css'

export default function EmptyState({
  title,
  description,
  actionTo,
  actionLabel,
  pillar,
}) {
  return (
    <section
      className={`empty-state${pillar ? ` empty-state--${pillar}` : ''}`}
      role="status"
      aria-live="polite"
      tabIndex={-1}
    >
      <div className="empty-state__mark" aria-hidden="true" />
      <h3 className="empty-state__title" id={undefined}>
        {title}
      </h3>
      {description ? <p className="empty-state__copy">{description}</p> : null}
      {actionTo && actionLabel ? (
        <Link className="button button-secondary empty-state__action" to={actionTo}>
          {actionLabel}
        </Link>
      ) : null}
    </section>
  )
}
