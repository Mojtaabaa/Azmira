// @ts-nocheck
import { Link } from 'react-router-dom'
import EmptyState from '../components/ui/EmptyState'
import { NAV_GROUPS, PILLARS } from '../navigation/pillars'
import './PillarHubPage.css'

const HUB_COPY = {
  learn: {
    title: 'مرکز یادگیری',
    blurb: 'مطالعه، تمرین، بازبینی و شواهد تسلط — نه فقط زمان سپری‌شده.',
    future: null,
  },
  improve: {
    title: 'مرکز بهبود',
    blurb: 'پیشرفت شخصی نسبت به معیار خودت؛ مقایسه گروهی مدرسه اختیاری و در فازهای بعد است.',
    future: 'معیارهای بهبود شخصی و مربی مبتنی بر شواهد در فاز ۱۲ روی این ستون کامل می‌شوند.',
  },
  achieve: {
    title: 'مرکز دستاورد',
    blurb: 'پیشرفت معنادار، نشان‌ها، آواتار و رقابت سالم — بدون اجبار به استفاده اجباری.',
    future: null,
  },
}

export default function PillarHubPage({ pillarId }) {
  const pillar = PILLARS[pillarId]
  const copy = HUB_COPY[pillarId]
  const group = NAV_GROUPS.find((g) => g.pillar === pillarId)
  const links = (group?.items || []).filter((item) => item.to !== pillar.hub)

  return (
    <div className={`page pillar-hub pillar-hub--${pillarId}`}>
      <header className="page-header pillar-hub__header">
        <p className="pillar-hub__en">{pillar.labelEn}</p>
        <h1>{copy.title}</h1>
        <p>{copy.blurb}</p>
      </header>

      <ul className="pillar-hub__links">
        {links.map((item) => (
          <li key={item.to}>
            <Link className="pillar-hub__link" to={item.to}>
              <strong>{item.label}</strong>
              <span>{item.short}</span>
            </Link>
          </li>
        ))}
      </ul>

      {copy.future ? (
        <EmptyState
          pillar={pillarId}
          title="آیندهٔ نزدیک"
          description={copy.future}
        />
      ) : null}
    </div>
  )
}
