// @ts-nocheck
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import KpiStat from '../components/ui/KpiStat'
import { useAuth } from '../hooks/useAuth'
import { NAV_GROUPS, PILLARS } from '../navigation/pillars'
import { request } from '../utils/api'
import PersonalImprovementPage from './PersonalImprovementPage'
import './PillarHubPage.css'

const HUB_COPY = {
  learn: {
    title: 'مرکز یادگیری',
    blurb: 'مطالعه، تمرین، بازبینی و شواهد تسلط — نه فقط زمان سپری‌شده.',
  },
  improve: {
    title: 'مرکز بهبود',
    blurb: 'پیشرفت شخصی نسبت به معیار خودت؛ مقایسه گروهی مدرسه اختیاری و با حریم خصوصی است.',
  },
  achieve: {
    title: 'مرکز دستاورد',
    blurb: 'پیشرفت معنادار، نشان‌ها، آواتار و رقابت سالم — بدون اجبار به استفاده اجباری.',
  },
}

export default function PillarHubPage({ pillarId }) {
  const { status } = useAuth()
  const [home, setHome] = useState(null)

  useEffect(() => {
    if (pillarId === 'improve' || status !== 'authed') return undefined
    let alive = true
    request('GET', '/learner/home', undefined, { silent: true })
      .then((payload) => {
        if (alive) setHome(payload)
      })
      .catch(() => {
        if (alive) setHome(null)
      })
    return () => {
      alive = false
    }
  }, [pillarId, status])

  if (pillarId === 'improve') {
    return <PersonalImprovementPage />
  }

  const pillar = PILLARS[pillarId]
  const copy = HUB_COPY[pillarId]
  const group = NAV_GROUPS.find((g) => g.pillar === pillarId)
  const links = (group?.items || []).filter((item) => item.to !== pillar.hub)

  const reviewsDue = home?.reviewsDue
  const weak = home?.weakMasteryCount
  const missions = home?.dailyMissions?.items || []
  const missionDone = missions.filter((m) => m.status === 'Claimed' || m.status === 'Completed').length
  const level = home?.profile?.level
  const journeyTitle = home?.journeySummary?.title

  const primary =
    pillarId === 'learn'
      ? {
          to: reviewsDue > 0 ? '/reviews' : '/journey',
          label: reviewsDue > 0 ? 'شروع بازبینی' : 'نقشه سفر',
        }
      : {
          to: '/missions',
          label: 'مأموریت‌های امروز',
        }

  return (
    <div className={`page pillar-hub pillar-hub--${pillarId}`}>
      <header className="page-header pillar-hub__header">
        <p className="pillar-hub__en">{pillar.labelEn}</p>
        <h1>{copy.title}</h1>
        <p>{copy.blurb}</p>
        <div className="kpi-stat-row pillar-hub__kpis">
          {pillarId === 'learn' ? (
            <>
              <KpiStat label="بازبینی سررسید" value={reviewsDue == null ? '—' : reviewsDue} />
              <KpiStat label="نقاط ضعیف" value={weak == null ? '—' : weak} />
              <KpiStat label="سفر" value={journeyTitle || '—'} dir="rtl" />
            </>
          ) : (
            <>
              <KpiStat label="سطح" value={level == null ? '—' : level} />
              <KpiStat label="مأموریت امروز" value={missions.length ? `${missionDone}/${missions.length}` : '—'} />
              <KpiStat label="سکه" value={home?.profile?.coins ?? '—'} />
            </>
          )}
        </div>
        <Link className="button button-primary pillar-hub__cta" to={primary.to}>
          {primary.label}
        </Link>
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
    </div>
  )
}
