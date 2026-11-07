// @ts-nocheck
import './PillarSection.css'

export default function PillarSection({
  pillar,
  eyebrow,
  title,
  children,
  action,
}) {
  return (
    <section className={`pillar-section pillar-section--${pillar || 'neutral'}`}>
      <header className="pillar-section__head">
        <div>
          {eyebrow ? <p className="pillar-section__eyebrow">{eyebrow}</p> : null}
          {title ? <h2 className="pillar-section__title">{title}</h2> : null}
        </div>
        {action || null}
      </header>
      <div className="pillar-section__body">{children}</div>
    </section>
  )
}
